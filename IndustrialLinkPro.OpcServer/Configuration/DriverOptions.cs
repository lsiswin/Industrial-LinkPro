namespace IndustrialLinkPro.OpcServer.Configuration;

/// <summary>
/// 设备驱动的全局配置选项
/// </summary>
public sealed class DriverOptions
{
    /// <summary>
    /// 配置文件中的节点名称
    /// </summary>
    public const string SectionName = "Drivers";

    /// <summary>
    /// 西门子 S7 协议设备的默认驱动配置
    /// </summary>
    public S7Options S7 { get; set; } = new();

    /// <summary>
    /// Modbus TCP 协议设备的默认驱动配置
    /// </summary>
    public ModbusTcpOptions ModbusTcp { get; set; } = new();
}

/// <summary>
/// 西门子 S7 协议的驱动配置选项
/// </summary>
public sealed class S7Options
{
    /// <summary>
    /// 建立连接的超时时间（毫秒）
    /// </summary>
    public int ConnectTimeoutMs { get; set; } = 5000;
}

/// <summary>
/// Modbus TCP 协议的驱动配置选项
/// </summary>
public sealed class ModbusTcpOptions
{
    /// <summary>
    /// 建立连接的超时时间（毫秒）
    /// </summary>
    public int ConnectTimeoutMs { get; set; } = 5000;
}
