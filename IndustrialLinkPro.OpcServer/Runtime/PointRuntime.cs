namespace IndustrialLinkPro.OpcServer.Runtime;

/// <summary>
/// 部署在内存中具体采集变量（数据点位）的实时运行状态模型
/// </summary>
public sealed class PointRuntime
{
    /// <summary>
    /// 点位的全局唯一 ID
    /// </summary>
    public Guid PointId { get; init; }

    /// <summary>
    /// 该点位所对应的设备 ID
    /// </summary>
    public Guid DeviceId { get; init; }

    /// <summary>
    /// PLC 或相关设备内部寄存器的偏值地址
    /// </summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>
    /// 变量点位的业务语义名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 映射的目标系统数据类型 (bool, float, int 等)
    /// </summary>
    public string DataType { get; set; } = string.Empty;

    /// <summary>
    /// 最新一次从底层驱动读取回来的数值
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// 点位数值的 OPC 质量代码标识（例如 "Good", "Bad", "Uncertain"）
    /// </summary>
    public string Quality { get; set; } = "Uncertain";

    /// <summary>
    /// 本次数值被更新的确切 UTC 时间戳
    /// </summary>
    public DateTimeOffset TimestampUtc { get; set; }
}
