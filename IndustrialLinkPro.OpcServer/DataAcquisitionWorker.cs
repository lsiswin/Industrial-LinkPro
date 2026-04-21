using System.Net.Sockets;
using IndustrialLinkPro.OpcServer.Drivers;
using IndustrialLinkPro.OpcServer.Runtime;

namespace IndustrialLinkPro.OpcServer;

/// <summary>
/// 数据采集后台服务，负责按照配置的扫描周期循环读取设备的点位数据。
/// </summary>
public sealed class DataAcquisitionWorker(
    ILogger<DataAcquisitionWorker> logger,
    IRuntimeNodeRegistry runtimeModel,
    IDriverFactory driverFactory
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "IndustrialLinkPro 数据采集服务启动 (Data Acquisition Worker starting)."
        );

        using var timer = new PeriodicTimer(
            TimeSpan.FromMilliseconds(runtimeModel.DefaultScanIntervalMs)
        );

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var devices = runtimeModel.GetDeviceRuntimes();

            // 遍历每个设备进行数据采集
            foreach (var device in devices)
            {
                stoppingToken.ThrowIfCancellationRequested();

                var driver = driverFactory.GetOrCreate(device);
                var points = runtimeModel.GetPointDefinitions(device.DeviceId);

                try
                {
                    if (!driver.IsConnected)
                    {
                        await driver.ConnectAsync(stoppingToken);
                    }

                    // 批量读取该设备下的所有点位数据
                    var values = await driver.BatchReadAsync(points, stoppingToken);
                    runtimeModel.MarkDeviceOnline(device.DeviceId);

                    foreach (var entry in values)
                    {
                        // 更新点位值，内部将触发 PointValueChanged 事件以推送给 OPC UA
                        var result = runtimeModel.UpdatePointValue(
                            device.DeviceId,
                            entry.Key,
                            entry.Value,
                            "Good"
                        );
                        if (result.IsSuccess)
                        {
                            if (result.IsChanged)
                            {
                                // 数据变化时，打印明显的变更日志
                                logger.LogInformation(
                                    "点位数据已更新 (Data changed) for device {DeviceId}, point {PointId}: [ {OldValue} ] -> [ {NewValue} ]",
                                    device.DeviceId,
                                    entry.Key,
                                    result.OldValue ?? "NULL",
                                    entry.Value ?? "NULL"
                                );
                            }
                            else
                            {
                                // 数据未变化时，降级为 Debug 日志，防止刷屏影响排错
                                logger.LogDebug(
                                    "采集成功但数据未变 (Data unchanged) for device {DeviceId}, point {PointId}: {Value}",
                                    device.DeviceId,
                                    entry.Key,
                                    entry.Value
                                );
                            }
                        }
                        else
                        {
                            logger.LogWarning(
                                "尝试更新不存在的点位 (Point not found): {PointId} on device {DeviceId}",
                                entry.Key,
                                device.DeviceId
                            );
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // 忽略任务取消异常
                }
                catch (SocketException ex)
                {
                    logger.LogWarning(
                        ex,
                        "设备网络通信异常 (Network issue) for device {DeviceId}: {Message}",
                        device.DeviceId,
                        ex.Message
                    );
                    runtimeModel.MarkDeviceFault(device.DeviceId, ex.Message);
                }
                catch (IOException ex)
                {
                    logger.LogWarning(
                        ex,
                        "设备 IO 异常 (IO issue) for device {DeviceId}: {Message}",
                        device.DeviceId,
                        ex.Message
                    );
                    runtimeModel.MarkDeviceFault(device.DeviceId, ex.Message);
                }
                catch (Exception ex)
                {
                    // 对不可预知的其他采集异常记录日志，避免整个循环崩溃
                    logger.LogWarning(
                        ex,
                        "采集失败 (Acquisition failed) for device {DeviceId}: {Message}",
                        device.DeviceId,
                        ex.Message
                    );
                    runtimeModel.MarkDeviceFault(device.DeviceId, ex.Message);
                }
            }
        }
    }
}
