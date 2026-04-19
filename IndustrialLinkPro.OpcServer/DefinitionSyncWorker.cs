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

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(runtimeModel.SyncIntervalSeconds));
        
        // 启动时先执行一次同步
        await SyncDefinitionsOnceAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await SyncDefinitionsOnceAsync(stoppingToken);
        }
    }

    private async Task SyncDefinitionsOnceAsync(CancellationToken cancellationToken)
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
            // 重建后 DynamicNodeManager 内部会触发 NodeId 到 DeviceApi 的回写动作
            if (result.DeviceTopologyChanged || result.PointsChanged)
            {
                await opcUaServerHost.RebuildAddressSpaceAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "定义同步失败 (Definition sync failed).");
        }
    }
}
