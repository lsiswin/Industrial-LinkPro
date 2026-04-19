using IndustrialLinkPro.OpcClient.Models;

namespace IndustrialLinkPro.OpcClient.Events;

/// <summary>
/// 数据变更事件参数
/// </summary>
public class DataChangedEventArgs : EventArgs
{
    /// <summary>
    /// 节点 ID
    /// </summary>
    public string NodeId { get; init; } = string.Empty;

    /// <summary>
    /// 数据值
    /// </summary>
    public DataPointValue DataPoint { get; init; } = new();
}

/// <summary>
/// 连接状态变更事件参数
/// </summary>
public class ConnectionStatusChangedEventArgs : EventArgs
{
    /// <summary>
    /// 旧状态
    /// </summary>
    public ConnectionStatus OldStatus { get; init; }

    /// <summary>
    /// 新状态
    /// </summary>
    public ConnectionStatus NewStatus { get; init; }

    /// <summary>
    /// 状态消息(可选)
    /// </summary>
    public string? Message { get; init; }
}
