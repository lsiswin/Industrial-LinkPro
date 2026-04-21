using IndustrialLinkPro.OpcServer.Clients;
using IndustrialLinkPro.OpcServer.Contracts;
using IndustrialLinkPro.OpcServer.Runtime;
using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Server;

namespace IndustrialLinkPro.OpcServer.OpcUa;

/// <summary>
/// 动态节点管理器，负责构建、维护并更新 OPC UA 地址空间中的设备及采节点树结构。
/// 它允许在不重启服务器的情况下动态响应底层网络设备的增删拓扑并推送数值变动。
/// </summary>
internal sealed class DynamicNodeManager : CustomNodeManager2
{
    private readonly IRuntimeNodeRegistry _registry;
    private readonly DeviceApiClient? _deviceApiClient;
    private readonly ILogger<DynamicNodeManager>? _logger;
    private readonly object _syncRoot = new();

    // 根节点：所有设备节点挂载其下
    private FolderState? _devicesFolder;

    // 映射查找表：缓存着运行时点位 ID 以及其实际绑定分配在地址空间中的被动 OPC 变量状态
    private readonly Dictionary<Guid, BaseDataVariableState> _pointVariables = new();

    // 缓存最后一次成功回写的 NodeId，防止重复提交
    private readonly Dictionary<Guid, string> _lastWrittenNodeIds = new();

    /// <summary>
    /// 初始化动态节点管理器
    /// </summary>
    public DynamicNodeManager(
        IServerInternal server,
        ApplicationConfiguration configuration,
        IRuntimeNodeRegistry registry,
        string namespaceUri,
        DeviceApiClient? deviceApiClient = null,
        ILogger<DynamicNodeManager>? logger = null)
        : base(server, configuration, namespaceUri)
    {
        _registry = registry;
        _deviceApiClient = deviceApiClient;
        _logger = logger;
        // 把本节点的产生器工厂方法赋到上下文中
        SystemContext.NodeIdFactory = this;
    }

    /// <summary>
    /// 当地址空间需要新增子节点分配新 NodeId 时执行此扩展覆盖。
    /// （在此我们可以拦截并处理预期的系统或字符串节点键生成方式）
    /// </summary>
    public override NodeId New(ISystemContext context, NodeState node)
    {
        return node.NodeId ?? new NodeId(node.BrowseName.Name, NamespaceIndex);
    }

    /// <summary>
    /// 返回任何预先硬编码编译好的静态节点，这里由于是动态生成拓扑直接返回空集合
    /// </summary>
    protected override NodeStateCollection LoadPredefinedNodes(ISystemContext context)
    {
        return [];
    }

    /// <summary>
    /// 初次在 OPC UA 服务器内创建根节点及地址架构逻辑触发点
    /// </summary>
    public override void CreateAddressSpace(IDictionary<NodeId, IList<IReference>> externalReferences)
    {
        lock (_syncRoot)
        {
            // 构造出 /Objects/Devices 这个主文件夹节点
            var root = CreateFolder(null, "Devices", "Devices");
            _devicesFolder = root;

            if (!externalReferences.TryGetValue(ObjectIds.ObjectsFolder, out var references))
            {
                references = [];
                externalReferences[ObjectIds.ObjectsFolder] = references;
            }

            // 让 ObjectsFolder 引用 (通过 Organizes 关系) 我们新创建的这批根设备包
            root.AddReference(ReferenceTypeIds.Organizes, true, ObjectIds.ObjectsFolder);
            references.Add(new NodeStateReference(ReferenceTypeIds.Organizes, false, root.NodeId));

            // 基于底层运行表进行深度的具体设备和采集点结构实例化
            RebuildInternal();
        }
    }

    /// <summary>
    /// 提供给外部服务当拓扑发生添加或删除变动时调用的重建机制暴露接口
    /// </summary>
    public void Rebuild()
    {
        lock (_syncRoot)
        {
            RebuildInternal();
        }
    }

    /// <summary>
    /// 实际上将外部定义的设备、属性以及下级传感数据挂点转换加载到 OPC 地址空间引擎中的业务核心逻辑
    /// </summary>
    private void RebuildInternal()
    {
        if (_devicesFolder is null)
        {
            return;
        }

        // 清空原有的设备树，全部重置以便加载最新的拓扑
        _pointVariables.Clear();

        // 依次遍历所有的活动设备并映射至文件夹节点
        foreach (var device in _registry.GetDeviceRuntimes())
        {
            var deviceFolder = CreateFolder(_devicesFolder, device.Name, $"Devices/{device.DeviceId}");
            var metadataFolder = CreateFolder(deviceFolder, "Metadata", $"Devices/{device.DeviceId}/Metadata");
            var dataPointsFolder = CreateFolder(deviceFolder, "DataPoints", $"Devices/{device.DeviceId}/DataPoints");

            // 将设备的固定参数封装到只读的通用系统属性节点中展示
            CreateProperty(metadataFolder, "DeviceId", device.DeviceId.ToString());
            CreateProperty(metadataFolder, "DeviceType", device.DeviceType.ToString());
            CreateProperty(metadataFolder, "ProtocolType", device.ProtocolType.ToString());
            CreateProperty(metadataFolder, "ConnectionString", device.ConnectionString);
            CreateProperty(metadataFolder, "Status", device.Status.ToString());

            // 逐个遍历挂载各下属的点位参数节点，并计入字典以备极速索引与赋值更新
            var points = _registry.GetPointDefinitions(device.DeviceId);
            foreach (var pointDef in points)
            {
                var pointRuntime = _registry.GetPointRuntimes().FirstOrDefault(x => x.PointId == pointDef.Id);
                if (pointRuntime != null)
                {
                    CreateVariable(dataPointsFolder, pointRuntime);

                    // 【核心优化】如果 API 端已经有这个 NodeId，则预填充缓存，防止启动后的全量回写
                    if (!string.IsNullOrEmpty(pointDef.NodeId) && !_lastWrittenNodeIds.ContainsKey(pointDef.Id))
                    {
                        _lastWrittenNodeIds[pointDef.Id] = pointDef.NodeId;
                    }
                }
            }
        }

        // 创建完节点后,异步更新 NodeId 到 DeviceApi
        // ---------- 优化：只回写发生变化的 NodeId ----------
        if (_deviceApiClient is not null)
        {
            // 清理掉已经不存在的点位的缓存
            var currentPointIds = _pointVariables.Keys.ToHashSet();
            var cachedIdsToRemove = _lastWrittenNodeIds.Keys.Except(currentPointIds).ToList();
            foreach (var id in cachedIdsToRemove) _lastWrittenNodeIds.Remove(id);

            // 收集只有 NodeId 发生变化的信息，用于批量更新
            var updateRequests = new List<UpdateNodeIdRequest>();
            foreach (var kvp in _pointVariables)
            {
                var pointId = kvp.Key;
                var variable = kvp.Value;

                if (variable.NodeId != null && !variable.NodeId.IsNullNodeId)
                {
                    var currentNodeIdStr = variable.NodeId.ToString();
                    
                    // 检查是否与上次成功回写的一致
                    if (!_lastWrittenNodeIds.TryGetValue(pointId, out var lastNodeId) || lastNodeId != currentNodeIdStr)
                    {
                        updateRequests.Add(new UpdateNodeIdRequest
                        {
                            DataPointId = pointId,
                            NodeId = currentNodeIdStr,
                            NamespaceIndex = variable.NodeId.NamespaceIndex
                        });
                    }
                }
            }

            if (updateRequests.Count > 0)
            {
                _logger?.LogInformation("检测到 {Count} 个点位的 NodeId 需要回写至 DeviceApi", updateRequests.Count);

                // 采用即发即忘的方式异步发送，不阻塞当前重建过程
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // 获取访问令牌（如果配置了认证）
                        string? accessToken = null;
                        try
                        {
                            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                            accessToken = await _deviceApiClient.LoginAsync(cts.Token).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "登录 DeviceApi 失败，将尝试无认证方式更新 NodeId");
                        }

                        // 执行批量更新
                        using var ctstoken = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                        var success = await _deviceApiClient.BatchUpdateNodeIdsAsync(
                            updateRequests,
                            accessToken,
                            ctstoken.Token).ConfigureAwait(false);

                        if (success)
                        {
                            _logger?.LogInformation("成功向 DeviceApi 回写了 {Count} 个数据点的 NodeId", updateRequests.Count);
                            
                            // 更新本地成功回写的缓存
                            lock (_syncRoot)
                            {
                                foreach (var req in updateRequests)
                                {
                                    _lastWrittenNodeIds[req.DataPointId] = req.NodeId;
                                }
                            }
                        }
                        else
                        {
                            _logger?.LogWarning("向 DeviceApi 回写 NodeId 失败，请检查网络或 API 状态");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "异步更新 NodeId 至 DeviceApi 时发生异常");
                    }
                });
            }
            else
            {
                _logger?.LogInformation("当前地址空间拓扑未发生 NodeId 变更，跳过回写动作");
            }
        }
        else
        {
            _logger?.LogWarning("DeviceApiClient 未注入，NodeId 回写功能被跳过");
        }
    }

    /// <summary>
    /// 直接响应具体发生改变的点位以更新节点展示的值与状态码（取代耗时遍历）
    /// </summary>
    public void UpdatePointValue(PointRuntime point)
    {
        lock (_syncRoot)
        {
            if (_pointVariables.TryGetValue(point.PointId, out var variable))
            {
                // 设置当前寄存器采集最新值并标记质量、打上时间戳
                variable.Value = point.Value;
                variable.StatusCode = point.Quality == "Good" ? StatusCodes.Good : StatusCodes.Bad;
                variable.Timestamp = point.TimestampUtc.UtcDateTime;
                
                // 强制触发数值变化的通知，OPC UA 客户端 (Subscriptions) 将接收到变化的属性推流包
                variable.ClearChangeMasks(SystemContext, false);
            }
        }
    }

    /// <summary>
    /// 辅助方法：便捷创建一个表示普通目录的 FolderState 并且注册绑定
    /// </summary>
    private FolderState CreateFolder(NodeState? parent, string browseName, string nodeKey)
    {
        var folder = new FolderState(parent)
        {
            SymbolicName = browseName,
            ReferenceTypeId = ReferenceTypeIds.Organizes,
            TypeDefinitionId = ObjectTypeIds.FolderType, // 节点类型为只读的 Folder
            NodeId = new NodeId(nodeKey, NamespaceIndex),
            BrowseName = new QualifiedName(browseName, NamespaceIndex),
            DisplayName = browseName,
            WriteMask = AttributeWriteMask.None, // 禁止在外部对本节点执行属性上的改动
            UserWriteMask = AttributeWriteMask.None,
            EventNotifier = EventNotifiers.None
        };

        if (parent is not null)
        {
            parent.AddChild(folder);
        }

        // 把构造出的模型正式抛给 OPC 引擎
        AddPredefinedNode(SystemContext, folder);
        return folder;
    }

    /// <summary>
    /// 辅助方法：为指派的父节点绑定一个带具体内容的文本形只读信息属性
    /// </summary>
    private void CreateProperty(FolderState parent, string name, string value)
    {
        var property = new BaseDataVariableState(parent)
        {
            NodeId = new NodeId($"{parent.NodeId}/{name}", NamespaceIndex),
            BrowseName = new QualifiedName(name, NamespaceIndex),
            DisplayName = name,
            DataType = DataTypeIds.String,
            TypeDefinitionId = VariableTypeIds.BaseDataVariableType,
            ValueRank = ValueRanks.Scalar,
            AccessLevel = AccessLevels.CurrentRead,
            UserAccessLevel = AccessLevels.CurrentRead,
            Value = value,
            StatusCode = StatusCodes.Good,
            Timestamp = DateTime.UtcNow
        };

        parent.AddChild(property);
        AddPredefinedNode(SystemContext, property);
    }

    /// <summary>
    /// 辅助方法：生成用来呈现现场底层控制设备具体遥测采集点变量数据的独立模型对象
    /// </summary>
    private void CreateVariable(FolderState parent, PointRuntime point)
    {
        var variable = new BaseDataVariableState(parent)
        {
            NodeId = new NodeId($"Devices/{point.DeviceId}/Points/{point.PointId}", NamespaceIndex),
            BrowseName = new QualifiedName(point.Name, NamespaceIndex),
            DisplayName = point.Name,
            DataType = ResolveDataType(point.DataType),
            TypeDefinitionId = VariableTypeIds.BaseDataVariableType,
            ValueRank = ValueRanks.Scalar,
            AccessLevel = AccessLevels.CurrentRead,
            UserAccessLevel = AccessLevels.CurrentRead,
            Value = point.Value,
            StatusCode = point.Quality == "Good" ? StatusCodes.Good : StatusCodes.Bad,
            Timestamp = point.TimestampUtc.UtcDateTime
        };

        parent.AddChild(variable);
        AddPredefinedNode(SystemContext, variable);
        // 保存索引从而应对高频率、低延迟的实时值更新策略
        _pointVariables[point.PointId] = variable;
    }

    /// <summary>
    /// 解释及转换底层驱动填报的数据类型至统一的 标准 OPC UA 预留节点类型 NodeId
    /// </summary>
    private static NodeId ResolveDataType(string dataType)
    {
        return dataType.ToLowerInvariant() switch
        {
            "bool" => DataTypeIds.Boolean,
            "int" => DataTypeIds.Int32,
            "float" => DataTypeIds.Float,
            "double" => DataTypeIds.Double,
            _ => DataTypeIds.String // 未匹配或文本默认映射为 String 类型
        };
    }
}
