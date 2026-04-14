using IndustrialLinkPro.OpcServer.Contracts;

namespace IndustrialLinkPro.OpcServer.Runtime;

/// <summary>
/// 缓存在本地内存中的设备运行状态模型，封装了设备定义以及动态更新的状态指标
/// </summary>
public sealed class DeviceRuntime
{
    /// <summary>
    /// 设备的全局唯一 ID
    /// </summary>
    public Guid DeviceId { get; init; }

    /// <summary>
    /// 设备的友好名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 设备的大类类型（如 Sensor, Plc）
    /// </summary>
    public DeviceType DeviceType { get; set; }

    /// <summary>
    /// 设备的底层通信协议
    /// </summary>
    public ProtocolType ProtocolType { get; set; }

    /// <summary>
    /// 设备的连接参数字符串
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// 该设备当前的采集连接状态（离线、在线、故障等）
    /// </summary>
    public DeviceStatus Status { get; set; }

    /// <summary>
    /// 最近一次通信发生错误的日志或信息，如果是 null 则代表正常
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// 最后一次健康心跳或者配置确认的 UTC 时间戳
    /// </summary>
    public DateTimeOffset LastSeenUtc { get; set; }
}
