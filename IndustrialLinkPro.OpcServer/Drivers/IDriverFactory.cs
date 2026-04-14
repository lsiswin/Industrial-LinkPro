using IndustrialLinkPro.OpcServer.Runtime;

namespace IndustrialLinkPro.OpcServer.Drivers;

/// <summary>
/// 设备通信驱动工厂接口，根据设备的网络参数与通讯协议负责创建并派发相应的驱动实例
/// </summary>
public interface IDriverFactory
{
    /// <summary>
    /// 获取设备对应的驱动缓存实例；如果不存该实例或设备协议发生更换，则会自动新建一个对应的底层驱动实例
    /// </summary>
    IDeviceDriver GetOrCreate(DeviceRuntime deviceRuntime);
}
