using IndustrialLinkPro.OpcClient.Configuration;
using IndustrialLinkPro.OpcClient.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IndustrialLinkPro.OpcClient.Services;

/// <summary>
/// 健康监控服务
/// 定期检查连接状态和数据更新延迟
/// </summary>
public class HealthMonitorService : BackgroundService
{
    private readonly OpcClientOptions _options;
    private readonly ILogger<HealthMonitorService> _logger;
    private readonly OpcClientService _opcClient;
    private readonly DataCacheService _dataCache;

    /// <summary>
    /// 健康状态变更事件
    /// </summary>
    public event EventHandler<HealthStatusEventArgs>? HealthStatusChanged;

    public HealthMonitorService(
        IOptions<OpcClientOptions> options,
        ILogger<HealthMonitorService> logger,
        OpcClientService opcClient,
        DataCacheService dataCache)
    {
        _options = options.Value;
        _logger = logger;
        _opcClient = opcClient;
        _dataCache = dataCache;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("健康监控服务启动");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckHealthAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "健康检查时出错");
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.HealthCheckIntervalSeconds), stoppingToken);
        }

        _logger.LogInformation("健康监控服务停止");
    }

    /// <summary>
    /// 执行健康检查
    /// </summary>
    private async Task CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var status = _opcClient.CurrentStatus;
        var isHealthy = status == ConnectionStatus.Connected;
        var cachedCount = _dataCache.Count;
        var staleDataPoints = GetStaleDataPoints(TimeSpan.FromSeconds(30));

        var healthStatus = new HealthStatusEventArgs
        {
            IsHealthy = isHealthy,
            ConnectionStatus = status,
            CachedDataPointCount = cachedCount,
            StaleDataPointCount = staleDataPoints.Count,
            StaleDataPoints = staleDataPoints,
            CheckTime = DateTime.UtcNow
        };

        HealthStatusChanged?.Invoke(this, healthStatus);

        if (!isHealthy)
        {
            _logger.LogWarning(
                "OPC Client 连接状态异常: {Status}, 缓存数据点: {Count}",
                status, cachedCount);
        }

        if (staleDataPoints.Count > 0)
        {
            _logger.LogWarning(
                "发现 {Count} 个超时数据点 (30秒未更新)",
                staleDataPoints.Count);
        }

        // 如果连接断开且不在重连中,尝试重连
        if (status is ConnectionStatus.Disconnected or ConnectionStatus.Error)
        {
            _logger.LogInformation("检测到连接断开,尝试重连...");
            try
            {
                await _opcClient.ConnectAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "重连失败");
            }
        }
    }

    /// <summary>
    /// 获取超时未更新的数据点
    /// </summary>
    private List<string> GetStaleDataPoints(TimeSpan timeout)
    {
        var stalePoints = new List<string>();
        var now = DateTime.UtcNow;

        foreach (var (nodeId, dataPoint) in _dataCache.GetAll())
        {
            if (now - dataPoint.LastUpdated > timeout)
            {
                stalePoints.Add(nodeId);
            }
        }

        return stalePoints;
    }
}

/// <summary>
/// 健康状态事件参数
/// </summary>
public class HealthStatusEventArgs : EventArgs
{
    /// <summary>
    /// 是否健康
    /// </summary>
    public bool IsHealthy { get; init; }

    /// <summary>
    /// 连接状态
    /// </summary>
    public ConnectionStatus ConnectionStatus { get; init; }

    /// <summary>
    /// 缓存的数据点数量
    /// </summary>
    public int CachedDataPointCount { get; init; }

    /// <summary>
    /// 超时的数据点数量
    /// </summary>
    public int StaleDataPointCount { get; init; }

    /// <summary>
    /// 超时的数据点列表
    /// </summary>
    public List<string> StaleDataPoints { get; init; } = new();

    /// <summary>
    /// 检查时间
    /// </summary>
    public DateTime CheckTime { get; init; }
}
