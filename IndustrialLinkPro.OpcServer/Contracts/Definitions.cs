using System.Text.Json.Serialization;

namespace IndustrialLinkPro.OpcServer.Contracts;

/// <summary>
/// 设备分类的枚举定义
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DeviceType
{
    /// <summary>
    /// 传感器类型设备
    /// </summary>
    Sensor = 1,
    
    /// <summary>
    /// PLC（可编程逻辑控制器）类型设备
    /// </summary>
    Plc = 2,
}

/// <summary>
/// 支持通信的工业协议类型枚举
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProtocolType
{
    /// <summary>
    /// 西门子 S7 协议
    /// </summary>
    S7 = 1,
    
    /// <summary>
    /// Modbus 的 TCP 变种协议
    /// </summary>
    ModbusTcp = 2,
}

/// <summary>
/// 设备当前的连接及运行状态
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DeviceStatus
{
    /// <summary>
    /// 离线 / 连接已断开
    /// </summary>
    Offline = 0,
    
    /// <summary>
    /// 在线 / 通信正常
    /// </summary>
    Online = 1,
    
    /// <summary>
    /// 发生故障 / 通信错误
    /// </summary>
    Fault = 2,
    
    /// <summary>
    /// 正在建立连接中
    /// </summary>
    Connecting = 3,
}

/// <summary>
/// 设备的配置和定义模型，通常由后端接口或数据库提供
/// </summary>
public sealed class DeviceDefinition
{
    /// <summary>
    /// 设备的全局唯一标识符
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 易读的设备名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 设备的大类类型
    /// </summary>
    public DeviceType Type { get; set; }

    /// <summary>
    /// 该设备采用的通信协议
    /// </summary>
    public ProtocolType ProtocolType { get; set; }

    /// <summary>
    /// 设备的最新运行状态
    /// </summary>
    public DeviceStatus Status { get; set; }

    /// <summary>
    /// 设备连接字符串，格式通常类似于字典对如 "host=192.168.1.100;port=502"
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// 挂载在该设备下所有需要采集的数据点定义列表
    /// </summary>
    public List<DataPointDefinition> DataPoints { get; set; } = [];
}

/// <summary>
/// 具体数据点（变量点）的模型定义
/// </summary>
public sealed class DataPointDefinition
{
    /// <summary>
    /// 数据点的唯一标识符
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 所属的设备 ID
    /// </summary>
    public Guid DeviceId { get; set; }

    /// <summary>
    /// 该点位在下位机系统中的寄存器地址或偏移量映射，如 "DB1.DBD10", "40001" 等
    /// </summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>
    /// 该点位的显示名称或用途描述
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 点位的数据类型映射标识，如 "bool", "float", "int" 等
    /// </summary>
    public string DataType { get; set; } = string.Empty;

    /// <summary>
    /// 点位配置信息的最新修改时间，用于增量判定
    /// </summary>
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

/// <summary>
/// 请求外部 API 的认证响应数据模型
/// </summary>
public sealed class AuthResponse
{
    /// <summary>
    /// 获取到的访问令牌，用于后续的 API 请求头
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// 令牌失效的时间
    /// </summary>
    public DateTimeOffset ExpiresAtUtc { get; set; }
}
