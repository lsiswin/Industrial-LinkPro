using System.Collections.Concurrent;
using System.Security.Cryptography.X509Certificates;
using IndustrialLinkPro.OpcClient.Configuration;
using IndustrialLinkPro.OpcClient.Events;
using IndustrialLinkPro.OpcClient.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;

namespace IndustrialLinkPro.OpcClient.Services;

/// <summary>
/// OPC UA 客户端服务 (后台服务 + 连接管理 + 数据订阅)
/// </summary>
public class OpcClientService : BackgroundService
{
    private readonly OpcClientOptions _options;
    private readonly ILogger<OpcClientService> _logger;
    private readonly DataCacheService _dataCache;

    private ISession? _session;
    private ApplicationConfiguration? _appConfig;
    private ConnectionStatus _currentStatus = ConnectionStatus.Disconnected;
    private readonly ConcurrentDictionary<string, MonitoredItem> _monitoredItems = new();
    private readonly List<Subscription> _subscriptions = new();
    private readonly object _lock = new();

    /// <summary>
    /// 数据变更事件
    /// </summary>
    public event EventHandler<DataChangedEventArgs>? DataChanged;

    /// <summary>
    /// 连接状态变更事件
    /// </summary>
    public event EventHandler<ConnectionStatusChangedEventArgs>? ConnectionStatusChanged;

    /// <summary>
    /// 当前连接状态
    /// </summary>
    public ConnectionStatus CurrentStatus
    {
        get => _currentStatus;
        private set
        {
            if (_currentStatus != value)
            {
                var oldStatus = _currentStatus;
                _currentStatus = value;
                OnConnectionStatusChanged(oldStatus, value);
            }
        }
    }

    public OpcClientService(
        IOptions<OpcClientOptions> options,
        ILogger<OpcClientService> logger,
        DataCacheService dataCache
    )
    {
        _options = options.Value;
        _logger = logger;
        _dataCache = dataCache;
    }

    /// <summary>
    /// 连接到 OPC UA 服务器
    /// </summary>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentStatus == ConnectionStatus.Connected)
        {
            _logger.LogInformation("已处于连接状态");
            return;
        }

        CurrentStatus = ConnectionStatus.Connecting;
        _logger.LogInformation("正在连接到 OPC UA 服务器: {EndpointUrl}", _options.EndpointUrl);

        try
        {
            // 创建应用配置
            _appConfig = await CreateApplicationConfigurationAsync();

            // 使用终端点 URL 自动获取服务端策略
            var endpoint = CoreClientUtils.SelectEndpoint(
                _appConfig,
                _options.EndpointUrl,
                false,
                15000
            );
            var configuredEndpoint = new ConfiguredEndpoint(
                null,
                endpoint,
                EndpointConfiguration.Create(_appConfig)
            );

            // 创建会话 (使用旧版本 API)
#pragma warning disable CS0618
            _session = await Session
                .Create(
                    _appConfig,
                    configuredEndpoint,
                    false,
                    _options.ApplicationName,
                    (uint)_options.SessionTimeoutMs,
                    new UserIdentity(),
                    Array.Empty<string>()
                )
                .ConfigureAwait(false);
#pragma warning restore CS0618

            CurrentStatus = ConnectionStatus.Connected;
            _logger.LogInformation("成功连接到 OPC UA 服务器");
        }
        catch (Exception ex)
        {
            CurrentStatus = ConnectionStatus.Error;
            _logger.LogError(ex, "连接到 OPC UA 服务器失败");
            throw;
        }
    }

    /// <summary>
    /// 断开连接
    /// </summary>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_session == null)
            return;

        _logger.LogInformation("正在断开 OPC UA 连接");

        try
        {
            // 移除所有订阅
            foreach (var subscription in _subscriptions)
            {
                await subscription.DeleteAsync(false).ConfigureAwait(false);
                subscription.Dispose();
            }
            _subscriptions.Clear();
            _monitoredItems.Clear();

            // 关闭会话
            await _session.CloseAsync().ConfigureAwait(false);
            _session = null;

            CurrentStatus = ConnectionStatus.Disconnected;
            _logger.LogInformation("已断开 OPC UA 连接");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "断开 OPC UA 连接时出错");
            throw;
        }
    }

    /// <summary>
    /// 清理所有现存的订阅 (安全替换订阅前调用)
    /// </summary>
    public async Task ClearSubscriptionsAsync()
    {
        if (_session == null || !_subscriptions.Any()) return;

        _logger.LogInformation("正在安全清理 {Count} 个现存的 OPC 订阅...", _subscriptions.Count);

        foreach (var subscription in _subscriptions)
        {
            try
            {
                await subscription.DeleteAsync(false).ConfigureAwait(false);
                subscription.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "清理旧订阅时发生警告，已忽略以避免阻塞主流程。");
            }
        }
        
        _subscriptions.Clear();
        _monitoredItems.Clear();
        
        _logger.LogInformation("订阅清理完成");
    }

    /// <summary>
    /// 订阅单个数据点
    /// </summary>
    public async Task SubscribeToNodeAsync(
        string nodeId,
        CancellationToken cancellationToken = default
    )
    {
        await SubscribeToNodesAsync(new[] { nodeId }, cancellationToken);
    }

    /// <summary>
    /// 订阅多个数据点
    /// </summary>
    public async Task SubscribeToNodesAsync(
        IEnumerable<string> nodeIds,
        CancellationToken cancellationToken = default
    )
    {
        if (_session == null)
            throw new InvalidOperationException("未建立 OPC UA 连接");

        var nodeIdList = nodeIds.ToList();
        _logger.LogInformation("正在订阅 {Count} 个数据点", nodeIdList.Count);

        var subscription = new Subscription(_session.DefaultSubscription)
        {
            PublishingInterval = (int)_options.PublishingIntervalMs,
            PublishingEnabled = true,
            DisplayName = $"IndustrialLinkPro_Subscription_{DateTime.UtcNow:yyyyMMddHHmmss}",
        };

        foreach (var nodeId in nodeIdList)
        {
            var monitoredItem = CreateMonitoredItem(subscription, nodeId);
            subscription.AddItem(monitoredItem);
            _monitoredItems[nodeId] = monitoredItem;
        }

        _session.AddSubscription(subscription);
        await subscription.CreateAsync().ConfigureAwait(false);
        _subscriptions.Add(subscription);

        _logger.LogInformation("成功订阅 {Count} 个数据点", nodeIdList.Count);
    }

    /// <summary>
    /// 取消订阅
    /// </summary>
    public async Task UnsubscribeAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        if (_monitoredItems.TryRemove(nodeId, out var monitoredItem))
        {
            foreach (var subscription in _subscriptions)
            {
                if (subscription.MonitoredItems.Contains(monitoredItem))
                {
                    subscription.RemoveItem(monitoredItem);
                    await subscription.ApplyChangesAsync().ConfigureAwait(false);
                    break;
                }
            }
        }

        _dataCache.Remove(nodeId);
    }

    /// <summary>
    /// 读取单个节点当前值
    /// </summary>
    public async Task<DataPointValue?> ReadNodeAsync(
        string nodeId,
        CancellationToken cancellationToken = default
    )
    {
        if (_session == null)
            throw new InvalidOperationException("未建立 OPC UA 连接");

        var node = new NodeId(nodeId);
        var value = await _session.ReadValueAsync(node).ConfigureAwait(false);

        var dataPoint = new DataPointValue
        {
            NodeId = nodeId,
            Value = value.Value,
            StatusCode = value.StatusCode,
            SourceTimestamp = value.SourceTimestamp,
            ServerTimestamp = value.ServerTimestamp,
        };

        _dataCache.Update(dataPoint);
        return dataPoint;
    }

    /// <summary>
    /// 获取缓存的数据点值
    /// </summary>
    public DataPointValue? GetCachedValue(string nodeId)
    {
        return _dataCache.Get(nodeId);
    }

    /// <summary>
    /// 获取所有缓存的数据点
    /// </summary>
    public IReadOnlyDictionary<string, DataPointValue> GetAllCachedValues()
    {
        return _dataCache.GetAll();
    }

    /// <summary>
    /// 创建应用配置
    /// </summary>
    private async Task<ApplicationConfiguration> CreateApplicationConfigurationAsync()
    {
        var config = new ApplicationConfiguration
        {
            ApplicationName = _options.ApplicationName,
            ApplicationUri = "urn:industrial-linkpro:opcclient",
            ApplicationType = ApplicationType.Client,
            SecurityConfiguration = new SecurityConfiguration
            {
                AutoAcceptUntrustedCertificates = _options.AutoAcceptUntrustedCertificates,
                RejectSHA1SignedCertificates = false,
                MinimumCertificateKeySize = 1024,
                ApplicationCertificate = new CertificateIdentifier
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = "%UserProfile%\\.IndustrialLinkProClient\\pki\\own",
                    SubjectName = "CN=My OPC UA Client",
                },
                TrustedIssuerCertificates = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = "%UserProfile%\\.IndustrialLinkProClient\\pki\\issuer",
                },
                TrustedPeerCertificates = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = "%UserProfile%\\.IndustrialLinkProClient\\pki\\trusted",
                },
                RejectedCertificateStore = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = "%UserProfile%\\.IndustrialLinkProClient\\pki\\rejected",
                },
            },
            TransportQuotas = new TransportQuotas
            {
                OperationTimeout = (int)_options.SessionTimeoutMs,
            },
            ClientConfiguration = new ClientConfiguration
            {
                DefaultSessionTimeout = (int)_options.SessionTimeoutMs,
            },
        };

        var application = new ApplicationInstance(config);

        try
        {
            await application.CheckApplicationInstanceCertificatesAsync(true, 2048);
        }
        catch (Exception ex)
            when (ex.Message.Contains("invalid") || ex.Message.Contains("certificate"))
        {
            try
            {
                var pkiDir = Environment.ExpandEnvironmentVariables(
                    "%UserProfile%\\.IndustrialLinkProClient\\pki"
                );
                if (System.IO.Directory.Exists(pkiDir))
                    System.IO.Directory.Delete(pkiDir, true);

                await application.CheckApplicationInstanceCertificatesAsync(true, 2048);
            }
            catch
            {
                // Ignore secondary cleanup failures
            }
        }

        await config.ValidateAsync(ApplicationType.Client).ConfigureAwait(false);
        return config;
    }

    /// <summary>
    /// 创建监控项
    /// </summary>
    private MonitoredItem CreateMonitoredItem(Subscription subscription, string nodeId)
    {
        var item = new MonitoredItem(subscription.DefaultItem)
        {
            StartNodeId = nodeId,
            AttributeId = Attributes.Value,
            SamplingInterval = (int)_options.SamplingIntervalMs,
            QueueSize = _options.QueueSize,
            DiscardOldest = true,
            MonitoringMode = MonitoringMode.Reporting,
        };

        item.Notification += OnMonitoredItemNotification;
        return item;
    }

    /// <summary>
    /// 监控项数据变更回调
    /// </summary>
    private void OnMonitoredItemNotification(
        MonitoredItem item,
        MonitoredItemNotificationEventArgs args
    )
    {
        foreach (var value in item.DequeueValues())
        {
            var dataPoint = new DataPointValue
            {
                NodeId = item.StartNodeId.ToString(),
                Value = value.Value,
                StatusCode = value.StatusCode,
                SourceTimestamp = value.SourceTimestamp,
                ServerTimestamp = value.ServerTimestamp,
            };

            // 更新缓存
            _dataCache.Update(dataPoint);

            // 触发事件
            try
            {
                DataChanged?.Invoke(
                    this,
                    new DataChangedEventArgs { NodeId = dataPoint.NodeId, DataPoint = dataPoint }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理数据变更事件时出错");
            }
        }
    }

    /// <summary>
    /// 连接状态变更回调
    /// </summary>
    private void OnConnectionStatusChanged(ConnectionStatus oldStatus, ConnectionStatus newStatus)
    {
        try
        {
            ConnectionStatusChanged?.Invoke(
                this,
                new ConnectionStatusChangedEventArgs
                {
                    OldStatus = oldStatus,
                    NewStatus = newStatus,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理连接状态变更事件时出错");
        }
    }

    /// <summary>
    /// 会话关闭回调
    /// </summary>
    private void OnSessionClosed(ISession session, EventArgs args)
    {
        _logger.LogWarning("OPC UA 会话已关闭");
        CurrentStatus = ConnectionStatus.Disconnected;
    }

    /// <summary>
    /// 后台服务执行
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OPC Client 服务启动");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConnectAsync(stoppingToken);

                // 等待会话关闭
                await WaitForSessionClose(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("OPC Client 服务停止");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "OPC UA 连接异常, {ReconnectDelayMs}ms 后重连",
                    _options.ReconnectDelayMs
                );
                CurrentStatus = ConnectionStatus.Reconnecting;

                try
                {
                    await Task.Delay(_options.ReconnectDelayMs, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        await DisconnectAsync(stoppingToken);
        _logger.LogInformation("OPC Client 服务已停止");
    }

    /// <summary>
    /// 等待会话关闭
    /// </summary>
    private Task WaitForSessionClose(CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<bool>();

        void OnKeepAlive(ISession sender, KeepAliveEventArgs e)
        {
            if (e.Status?.Code != StatusCodes.Good)
            {
                tcs.TrySetException(new Exception($"KeepAlive 状态异常: {e.Status}"));
            }
        }

        if (_session != null)
        {
            _session.KeepAlive += OnKeepAlive;
        }

        // 使用 CancellationToken 注册取消
        cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));

        return tcs.Task;
    }
}
