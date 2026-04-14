using IndustrialLinkPro.OpcServer.Runtime;
using Opc.Ua;
using Opc.Ua.Server;

namespace IndustrialLinkPro.OpcServer.OpcUa;

/// <summary>
/// 标准 OPC UA 的内部承载服务器实例，它派生自 OPC 官方的 StandardServer
/// 提供与应用程序本身对接的 MasterNodeManager 从而支撑对外服务的实际运行环境
/// </summary>
internal sealed class DynamicOpcUaServer(IRuntimeNodeRegistry registry, string namespaceUri) : StandardServer
{
    private readonly IRuntimeNodeRegistry _registry = registry;
    private readonly string _namespaceUri = namespaceUri;
    
    // 保存其对应的动态节点管理器实例引用
    private DynamicNodeManager? _nodeManager;

    /// <summary>
    /// 重写主节点管理器注册方法，这是在 OPC UA 引擎加载阶段挂载核心组件的必须工序
    /// </summary>
    protected override MasterNodeManager CreateMasterNodeManager(
        IServerInternal server,
        ApplicationConfiguration configuration)
    {
        // 绑定自己体系里的节点管理器和内部运行时实例
        _nodeManager = new DynamicNodeManager(server, configuration, _registry, _namespaceUri);
        return new MasterNodeManager(server, configuration, null, _nodeManager);
    }

    /// <summary>
    /// 触发整个底层运行数据源结构的树状再建。这通常在配置文件产生新增减变动时请求
    /// </summary>
    public void RebuildAddressSpace()
    {
        _nodeManager?.Rebuild();
    }

    /// <summary>
    /// 将从控制器驱动端抓取来的点位数据透传给动态节点管理器要求变更 OPC 模型状态
    /// </summary>
    public void UpdatePointValue(PointRuntime point)
    {
        _nodeManager?.UpdatePointValue(point);
    }
}
