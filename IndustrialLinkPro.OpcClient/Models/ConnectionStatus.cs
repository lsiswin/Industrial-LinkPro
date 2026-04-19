namespace IndustrialLinkPro.OpcClient.Models;

/// <summary>
/// OPC UA 连接状态
/// </summary>
public enum ConnectionStatus
{
    /// <summary>
    /// 未连接
    /// </summary>
    Disconnected,

    /// <summary>
    /// 连接中
    /// </summary>
    Connecting,

    /// <summary>
    /// 已连接
    /// </summary>
    Connected,

    /// <summary>
    /// 连接失败
    /// </summary>
    Error,

    /// <summary>
    /// 正在重连
    /// </summary>
    Reconnecting
}
