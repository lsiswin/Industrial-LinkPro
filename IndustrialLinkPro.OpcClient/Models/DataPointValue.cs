using Opc.Ua;

namespace IndustrialLinkPro.OpcClient.Models;

/// <summary>
/// OPC UA 数据点值
/// </summary>
public class DataPointValue
{
    /// <summary>
    /// 节点 ID
    /// </summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>
    /// 数据值
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// 状态码
    /// </summary>
    public StatusCode StatusCode { get; set; }

    /// <summary>
    /// 源时间戳
    /// </summary>
    public DateTime SourceTimestamp { get; set; }

    /// <summary>
    /// 服务器时间戳
    /// </summary>
    public DateTime ServerTimestamp { get; set; }

    /// <summary>
    /// 是否良好状态
    /// </summary>
    public bool IsGood => StatusCode.IsGood(StatusCode);

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
