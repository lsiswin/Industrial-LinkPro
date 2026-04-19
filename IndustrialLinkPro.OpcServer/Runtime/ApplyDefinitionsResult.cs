namespace IndustrialLinkPro.OpcServer.Runtime;

/// <summary>
/// 当设备和点位的最新配置同步到运行时内存时，反馈执行结果记录的模型
/// </summary>
/// <param name="DeviceTopologyChanged">标识对比上一轮配置环境，设备的网络拓扑（设备增删、设备属性变更）是否有变动</param>
/// <param name="PointsChanged">标识点位是否有新增或删除</param>
public sealed record ApplyDefinitionsResult(bool DeviceTopologyChanged, bool PointsChanged);
