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
                "定义同步完成 (Definition sync completed). 设备数: {DeviceCount}, 点位数: {PointCount}, 拓扑是否改变: {TopologyChanged}",
                runtimeModel.GetDeviceRuntimes().Count,
                runtimeModel.GetPointRuntimes().Count,
                result.TopologyChanged);

            // 如果 OPC UA 地址空间的拓扑结构发生变化，通知 OPC UA 服务重建地址空间
            if (result.TopologyChanged)
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
