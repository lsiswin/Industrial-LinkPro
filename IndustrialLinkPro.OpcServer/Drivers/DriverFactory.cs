using System.Collections.Concurrent;
using IndustrialLinkPro.OpcServer.Runtime;

namespace IndustrialLinkPro.OpcServer.Drivers;

/// <summary>
/// 默认的设备通信驱动工厂，使用并发字典缓存不同设备的实例，并支持基于服务容器的依赖注入创建
/// </summary>
public sealed class DriverFactory(IServiceProvider serviceProvider) : IDriverFactory
{
    private readonly ConcurrentDictionary<Guid, IDeviceDriver> _drivers = new();

    /// <inheritdoc/>
    public IDeviceDriver GetOrCreate(DeviceRuntime deviceRuntime)
    {
        return _drivers.GetOrAdd(deviceRuntime.DeviceId, _ =>
        {
            // 通过获取到的设备定义自动匹配并反射创建相应的驱动实现类
            return deviceRuntime.ProtocolType switch
            {
                Contracts.ProtocolType.S7 => ActivatorUtilities.CreateInstance<S7DeviceDriver>(serviceProvider, deviceRuntime),
                Contracts.ProtocolType.ModbusTcp => ActivatorUtilities.CreateInstance<ModbusTcpDeviceDriver>(serviceProvider, deviceRuntime),
                _ => throw new NotSupportedException($"Unsupported protocol type: {deviceRuntime.ProtocolType}")
            };
        });
    }
}
