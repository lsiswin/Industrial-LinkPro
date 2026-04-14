namespace IndustrialLinkPro.OpcServer.OpcUa;

/// <summary>
/// OPC UA 核心宿主接口，负责管理 OPC UA Server 的生命周期与配置
/// </summary>
public interface IOpcUaServerHost
{
    /// <summary>
    /// 启动 OPC UA Server
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 停止 OPC UA Server
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 重建 OPC UA 的应用地址空间拓扑
    /// </summary>
    Task RebuildAddressSpaceAsync(CancellationToken cancellationToken);
}
