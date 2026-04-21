using IndustrialLinkPro.OpcServer.OpcUa;
using IndustrialLinkPro.OpcServer.Runtime;
using IndustrialLinkPro.OpcServer.Services;

namespace IndustrialLinkPro.OpcServer;

/// <summary>
/// 设备定义同步后台服务，负责定期从配置源获取设备和点位信息，并更新到运行模型和 OPC UA 地址空间。
/// </summary>
public sealed class DefinitionSyncWorker(
    ILogger<DefinitionSyncWorker> logger,
    IDeviceDefinitionProvider definitionProvider,
    IRuntimeNodeRegistry runtimeModel,
    IOpcUaServerHost opcUaServerHost
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("IndustrialLinkPro 定义同步服务启动 (Definition Sync Worker starting).");

        // 启动时持续尝试同步，直到成功获取到定义，实现自动重连逻辑
        bool firstSyncSuccess = false;
        while (!firstSyncSuccess && !stoppingToken.IsCancellationRequested)
        {
            firstSyncSuccess = await SyncDefinitionsOnceAsync(stoppingToken);
            
            if (!firstSyncSuccess)
            {
                const int retryDelaySeconds = 5;
                logger.LogWarning("初始定义同步失败，将在 {Seconds} 秒后重试... (Initial sync failed, retrying in {Seconds}s...)", retryDelaySeconds, retryDelaySeconds);
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        if (stoppingToken.IsCancellationRequested) return;

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(runtimeModel.SyncIntervalSeconds));
        
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await SyncDefinitionsOnceAsync(stoppingToken);
        }
    }

    private async Task<bool> SyncDefinitionsOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            // 从接口或数据库获取最新的设备定义
            var devices = await definitionProvider.GetDefinitionsAsync(cancellationToken);
            
            // 应用设备定义到运行时的节点注册表中
            var result = runtimeModel.ApplyDefinitions(devices);

            logger.LogInformation(
                "定义同步完成 (Definition sync completed). 设备数: {DeviceCount}, 点位数: {PointCount}, 设备拓扑是否改变: {DeviceTopologyChanged}, 点位是否改变: {PointsChanged}",
                runtimeModel.GetDeviceRuntimes().Count,
                runtimeModel.GetPointRuntimes().Count,
                result.DeviceTopologyChanged,
                result.PointsChanged);

            // 注意：当设备拓扑或者数据点位发生变化时，需要完全重建地址空间
            if (result.DeviceTopologyChanged || result.PointsChanged)
            {
                await opcUaServerHost.RebuildAddressSpaceAsync(cancellationToken);
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "定义同步失败 (Definition sync failed). 请检查 API 服务状态及其网络连接。");
            return false;
        }
    }
}
