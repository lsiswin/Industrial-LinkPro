using IndustrialLinkPro.OpcServer.Clients;
using IndustrialLinkPro.OpcServer.Configuration;
using IndustrialLinkPro.OpcServer.Runtime;
using Microsoft.Extensions.Options;
using Opc.Ua;
using Opc.Ua.Configuration;
using Opc.Ua.Server;

namespace IndustrialLinkPro.OpcServer.OpcUa;

/// <summary>
/// OPC UA 服务器的生命周期宿主服务，负责按配置构建与启停真正的 UA 服务器对象 `DynamicOpcUaServer`。
/// </summary>
public sealed class OpcUaServerHost(
    ILogger<OpcUaServerHost> logger,
    ILoggerFactory loggerFactory,
    IRuntimeNodeRegistry runtimeNodeRegistry,
    IOptions<OpcServerOptions> options,
    DeviceApiClient deviceApiClient
) : IOpcUaServerHost, IHostedService
{
    private readonly ILogger<OpcUaServerHost> _logger = logger;
    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private readonly IRuntimeNodeRegistry _runtimeNodeRegistry = runtimeNodeRegistry;
    private readonly OpcServerOptions _options = options.Value;
    private readonly DeviceApiClient _deviceApiClient = deviceApiClient;
    private ApplicationInstance? _application;
    private DynamicOpcUaServer? _server;

    /// <summary>
    /// 伴随宿主应用程序作为 IHostedService 启动，创建并运行 OPC UA Server
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_server is not null)
        {
            return;
        }

        _application = new ApplicationInstance()
        {
            ApplicationName = _options.ApplicationName,
            ApplicationType = ApplicationType.Server,
            ConfigSectionName = "OpcUa",
        };

        var configuration = await BuildConfigurationAsync();
        _application.ApplicationConfiguration = configuration;

        // 【新增代码】检查证书，如果不存在则自动生成
        bool certOk = await _application.CheckApplicationInstanceCertificatesAsync(false, 0);
        if (!certOk)
        {
            _logger.LogWarning("OPC UA 服务器应用证书缺失或无法生成，连接可能会失败。");
        }

        _server = new DynamicOpcUaServer(
            _runtimeNodeRegistry, 
            _options.NamespaceUri, 
            _deviceApiClient, 
            _loggerFactory.CreateLogger<DynamicNodeManager>());
        await _application.StartAsync(_server);

        // 挂载点位变更事件
        _runtimeNodeRegistry.PointValueChanged += OnPointValueChanged;

        _logger.LogInformation("OPC UA server started at {EndpointUrl}", _options.EndpointUrl);
    }

    /// <summary>
    /// 停止 OPC UA Server 运行并注销事件订阅
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_server is null)
        {
            return;
        }

        // 移除订阅
        _runtimeNodeRegistry.PointValueChanged -= OnPointValueChanged;

        await Task.Run(() => _server.StopAsync(), cancellationToken);
        _server = null;
        _logger.LogInformation("OPC UA server stopped.");
    }

    // 事件响应方法
    private void OnPointValueChanged(PointRuntime point)
    {
        _server?.UpdatePointValue(point);
    }

    /// <summary>
    /// 当外部配置同步影响到拓扑结构时，调用重建方法刷新整个地址空间树
    /// </summary>
    public async Task RebuildAddressSpaceAsync(CancellationToken cancellationToken)
    {
        if (_server is null)
        {
            return;
        }

        await Task.Run(() => _server.RebuildAddressSpace(), cancellationToken);
        _logger.LogInformation("OPC UA address space rebuilt.");
    }

    private Task<ApplicationConfiguration> BuildConfigurationAsync()
    {
        return Task.FromResult(
            new ApplicationConfiguration
            {
                ApplicationName = _options.ApplicationName,
                ApplicationUri = _options.BaseAddress,
                ApplicationType = ApplicationType.Server,
                SecurityConfiguration = new SecurityConfiguration
                {
                    ApplicationCertificate = new CertificateIdentifier
                    {
                        // 改为 Directory 模式，存放在程序运行目录下的 pki/own 文件夹
                        StoreType = "Directory",
                        StorePath = Path.Combine(AppContext.BaseDirectory, "pki", "own"),
                        SubjectName = $"CN={_options.ApplicationName}",
                    },
                    TrustedPeerCertificates = new CertificateTrustList
                    {
                        StoreType = "Directory",
                        StorePath = Path.Combine(AppContext.BaseDirectory, "pki", "trusted"),
                    },
                    RejectedCertificateStore = new CertificateTrustList
                    {
                        StoreType = "Directory",
                        StorePath = Path.Combine(AppContext.BaseDirectory, "pki", "rejected"),
                    },
                    AutoAcceptUntrustedCertificates = true,
                },
                ServerConfiguration = new ServerConfiguration
                {
                    BaseAddresses = { _options.EndpointUrl },
                    SecurityPolicies = new ServerSecurityPolicyCollection
                    {
                        new()
                        {
                            SecurityMode = MessageSecurityMode.None,
                            SecurityPolicyUri = SecurityPolicies.None,
                        },
                    },
                    DiagnosticsEnabled = true,
                },
                TransportQuotas = new TransportQuotas { OperationTimeout = 15000 },
                TraceConfiguration = new TraceConfiguration(),
            }
        );
    }
}
