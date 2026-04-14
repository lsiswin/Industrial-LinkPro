namespace IndustrialLinkPro.OpcServer.Drivers;

/// <summary>
/// 设备驱动健康状态的只读快照记录
/// </summary>
/// <param name="IsConnected">当前网络底层是否处于物理连接状态</param>
/// <param name="Status">友好易读的状态信息（例如 "Connected", "Disconnected"）</param>
/// <param name="LastSuccessUtc">最后一次成功通信/读取的 UTC 时间</param>
/// <param name="LastError">如果最近发生错误，此处提供异常消息摘要</param>
public sealed record DriverHealthSnapshot(
    bool IsConnected,
    string Status,
    DateTimeOffset? LastSuccessUtc,
    string? LastError
);
