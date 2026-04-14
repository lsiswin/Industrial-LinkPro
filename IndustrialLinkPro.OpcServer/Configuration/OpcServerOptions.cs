namespace IndustrialLinkPro.OpcServer.Configuration;

/// <summary>
/// OPC UA 服务器的启动配置选项
/// </summary>
public sealed class OpcServerOptions
{
    /// <summary>
    /// 配置文件中的节点名称
    /// </summary>
    public const string SectionName = "OpcUa";

    /// <summary>
    /// 注册在 OPC UA 中的应用程序名称
    /// </summary>
    public string ApplicationName { get; set; } = "IndustrialLinkPro OPC Server";

    /// <summary>
    /// OPC UA 服务器通过 TCP 协议暴露的端点监听地址
    /// </summary>
    public string EndpointUrl { get; set; } = "opc.tcp://0.0.0.0:4840/IndustrialLinkPro";

    /// <summary>
    /// OPC UA 服务器的基础 URI 标识
    /// </summary>
    public string BaseAddress { get; set; } = "urn:IndustrialLinkPro:OpcServer";

    /// <summary>
    /// 用于存放 OPC UA 数字证书的默认存储库路径
    /// </summary>
    public string CertificateStore { get; set; } = "CertificateStores/MachineDefault";

    /// <summary>
    /// 节点地址空间 (Address Space) 自定义的 Namespace URI，用于区分本系统自动生成的节点
    /// </summary>
    public string NamespaceUri { get; set; } = "urn:industrial-linkpro:opcserver";
}
