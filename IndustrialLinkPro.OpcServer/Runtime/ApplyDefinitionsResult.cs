namespace IndustrialLinkPro.OpcServer.Runtime;

/// <summary>
/// 当设备和点位的最新配置同步到运行时内存时，反馈执行结果记录的模型
/// </summary>
/// <param name="TopologyChanged">标识对比上一轮配置环境，设备的网络拓扑、点位新增或删除等是否有变动</param>
public sealed record ApplyDefinitionsResult(bool TopologyChanged);
