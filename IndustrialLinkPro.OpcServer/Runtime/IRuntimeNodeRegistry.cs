using IndustrialLinkPro.OpcServer.Contracts;

namespace IndustrialLinkPro.OpcServer.Runtime;

public interface IRuntimeNodeRegistry
{
    /// <summary>
    /// 同步周期（秒）
    /// </summary>
    int SyncIntervalSeconds { get; }

    /// <summary>
    /// 默认扫描周期（毫秒）
    /// </summary>
    int DefaultScanIntervalMs { get; }

    /// <summary>
    /// 点位值变化事件,通知 OPC UA 服务等订阅者更新节点数据
    /// </summary>
    event Action<PointRuntime>? PointValueChanged;
    
    /// <summary>
    /// 新点位添加事件,通知 OPC UA 服务动态创建新的点位节点
    /// </summary>
    event Action<PointRuntime>? PointAdded;

    /// <summary>
    /// 应用从配置源读取的最新设备定义
    /// </summary>
    ApplyDefinitionsResult ApplyDefinitions(IReadOnlyCollection<DeviceDefinition> devices);

    /// <summary>
    /// 获取所有设备的运行状态集合
    /// </summary>
    IReadOnlyCollection<DeviceRuntime> GetDeviceRuntimes();

    /// <summary>
    /// 获取所有点位的运行状态集合
    /// </summary>
    IReadOnlyCollection<PointRuntime> GetPointRuntimes();

    /// <summary>
    /// 针对指定设备获取所有点位的定义结构
    /// </summary>
    IReadOnlyCollection<DataPointDefinition> GetPointDefinitions(Guid deviceId);

    /// <summary>
    /// 更新点位值并触发事件
    /// </summary>
    void UpdatePointValue(Guid deviceId, Guid pointId, object? value, string quality);

    /// <summary>
    /// 标识设备在线，并清除错误状态
    /// </summary>
    void MarkDeviceOnline(Guid deviceId);

    /// <summary>
    /// 标识设备故障，并级联将设备下面的点位标记为 Bad
    /// </summary>
    void MarkDeviceFault(Guid deviceId, string error);
}
