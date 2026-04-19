namespace IndustrialLinkPro.OpcClient.Configuration;

/// <summary>
/// OPC Client 配置选项
/// </summary>
public class OpcClientOptions
{
    public const string SectionName = "OpcClient";

    /// <summary>
    /// OPC UA 服务器端点地址
    /// 例如: opc.tcp://localhost:4842/IndustrialLinkPro
    /// </summary>
    public string EndpointUrl { get; set; } = "opc.tcp://localhost:4842/IndustrialLinkPro";

    /// <summary>
    /// 客户端应用名称
    /// </summary>
    public string ApplicationName { get; set; } = "IndustrialLinkPro OPC Client";

    /// <summary>
    /// 会话超时时间(毫秒)
    /// </summary>
    public uint SessionTimeoutMs { get; set; } = 60000;

    /// <summary>
    /// 订阅发布间隔(毫秒)
    /// </summary>
    public double PublishingIntervalMs { get; set; } = 1000;

    /// <summary>
    /// 采样间隔(毫秒)
    /// </summary>
    public double SamplingIntervalMs { get; set; } = 500;

    /// <summary>
    /// 队列大小
    /// </summary>
    public uint QueueSize { get; set; } = 10;

    /// <summary>
    /// 是否自动接受不受信任的证书
    /// </summary>
    public bool AutoAcceptUntrustedCertificates { get; set; } = true;

    /// <summary>
    /// 重连延迟(毫秒)
    /// </summary>
    public int ReconnectDelayMs { get; set; } = 5000;

    /// <summary>
    /// 健康检查间隔(秒)
    /// </summary>
    public int HealthCheckIntervalSeconds { get; set; } = 30;
}
