using System.Collections.Concurrent;
using IndustrialLinkPro.OpcServer.Contracts;

namespace IndustrialLinkPro.OpcServer.Runtime;

/// <summary>
/// 核心的运行时节点注册表实现类。在内存中维护设备和点位的最新状态、网络拓扑结构和配置定义，
/// 它起到了底层驱动数据采集与上层 OPC UA 服务呈现之间的高效数据桥梁作用。
/// </summary>
public sealed class RuntimeModel(IConfiguration configuration) : IRuntimeNodeRegistry
{
    // 利用高并发字典存储设备运行时、点位运行时及其静态定义
    private readonly ConcurrentDictionary<Guid, DeviceRuntime> _devices = new();
    private readonly ConcurrentDictionary<Guid, PointRuntime> _points = new();
    private readonly ConcurrentDictionary<Guid, DataPointDefinition> _pointDefinitions = new();

    /// <inheritdoc/>
    public long TopologyVersion { get; private set; } = 1;

    /// <inheritdoc/>
    public int SyncIntervalSeconds => configuration.GetValue<int?>("SyncIntervalSeconds") ?? 60;

    /// <inheritdoc/>
    public int DefaultScanIntervalMs =>
        configuration.GetValue<int?>("DefaultScanIntervalMs") ?? 1000;

    /// <inheritdoc/>
    public event Action<PointRuntime>? PointValueChanged;

    /// <inheritdoc/>
    public event Action<PointRuntime>? PointAdded;

    /// <inheritdoc/>
    public ApplyDefinitionsResult ApplyDefinitions(IReadOnlyCollection<DeviceDefinition> devices)
    {
        var deviceTopologyChanged = false;
        var pointsChanged = false;
        var incomingDeviceIds = devices.Select(x => x.Id).ToHashSet();
        var incomingPointIds = devices.SelectMany(x => x.DataPoints).Select(x => x.Id).ToHashSet();

        // 移除已经不在最新配置中的旧设备实体
        foreach (var removedDeviceId in _devices.Keys.Except(incomingDeviceIds))
        {
            deviceTopologyChanged =
                _devices.TryRemove(removedDeviceId, out _) || deviceTopologyChanged;
        }

        // 移除已经不在最新配置中的旧点位实体
        foreach (var removedPointId in _points.Keys.Except(incomingPointIds))
        {
            pointsChanged = _points.TryRemove(removedPointId, out _) || pointsChanged;
            _pointDefinitions.TryRemove(removedPointId, out _);
        }

        // 遍历更新现有的设备实体以及新加入的设备
        foreach (var device in devices)
        {
            var runtime = _devices.AddOrUpdate(
                device.Id,
                _ =>
                {
                    // 发现新设备，标记设备拓扑发生了变化
                    deviceTopologyChanged = true;
                    return new DeviceRuntime
                    {
                        DeviceId = device.Id,
                        Name = device.Name,
                        DeviceType = device.Type,
                        ProtocolType = device.ProtocolType,
                        ConnectionString = device.ConnectionString,
                        Status = device.Status,
                        LastSeenUtc = DateTimeOffset.UtcNow,
                    };
                },
                (_, existing) =>
                {
                    // 若设备已存在，比对其标识信息是否改变来判定是否影响 OPC UA 拓扑的展示
                    var existingSnapshot = (
                        existing.Name,
                        existing.ProtocolType,
                        existing.ConnectionString
                    );
                    existing.Name = device.Name;
                    existing.DeviceType = device.Type;
                    existing.ProtocolType = device.ProtocolType;
                    existing.ConnectionString = device.ConnectionString;
                    existing.Status = device.Status;
                    existing.LastSeenUtc = DateTimeOffset.UtcNow;
                    deviceTopologyChanged |=
                        existingSnapshot
                        != (device.Name, device.ProtocolType, device.ConnectionString);
                    return existing;
                }
            );

            // 遍历设备的底层点位结构进行映射或创建
            foreach (var point in device.DataPoints)
            {
                _pointDefinitions[point.Id] = point;

                _points.AddOrUpdate(
                    point.Id,
                    _ =>
                    {
                        // 发现新点位，标记点位发生了变化
                        pointsChanged = true;
                        var newPoint = new PointRuntime
                        {
                            PointId = point.Id,
                            DeviceId = point.DeviceId,
                            Address = point.Address,
                            Name = point.Name,
                            DataType = point.DataType,
                            TimestampUtc = DateTimeOffset.UtcNow,
                        };
                        // 触发新点位添加事件
                        PointAdded?.Invoke(newPoint);
                        return newPoint;
                    },
                    (_, existing) =>
                    {
                        // 若点位已存在，判断关键字段改变情况
                        var existingSnapshot = (existing.Address, existing.Name, existing.DataType);
                        existing.Address = point.Address;
                        existing.Name = point.Name;
                        existing.DataType = point.DataType;
                        pointsChanged |=
                            existingSnapshot != (point.Address, point.Name, point.DataType);
                        return existing;
                    }
                );
            }

            runtime.LastSeenUtc = DateTimeOffset.UtcNow;
        }

        var result = new ApplyDefinitionsResult(deviceTopologyChanged, pointsChanged);
        if (deviceTopologyChanged || pointsChanged)
        {
            TopologyVersion++;
        }
        return result;
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<DeviceRuntime> GetDeviceRuntimes() =>
        _devices.Values.OrderBy(x => x.Name).ToArray();

    /// <inheritdoc/>
    public IReadOnlyCollection<PointRuntime> GetPointRuntimes() =>
        _points.Values.OrderBy(x => x.Name).ToArray();

    /// <inheritdoc/>
    public IReadOnlyCollection<DataPointDefinition> GetPointDefinitions(Guid deviceId)
    {
        return _pointDefinitions
            .Values.Where(x => x.DeviceId == deviceId)
            .OrderBy(x => x.Name)
            .ToArray();
    }

    /// <summary>
    /// 更新点位值，并返回更新状态
    /// </summary>
    /// <returns>IsSuccess: 是否找到该点位; IsChanged: 数据或质量是否发生实质性改变; OldValue: 修改前的旧值</returns>
    public (bool IsSuccess, bool IsChanged, object? OldValue) UpdatePointValue(
        Guid deviceId,
        Guid pointId,
        object? value,
        string quality
    )
    {
        // 1. 数据采集说明网络畅通，优先重置设备在线与活动时间
        if (_devices.TryGetValue(deviceId, out var device))
        {
            device.Status = DeviceStatus.Online;
            device.LastSeenUtc = DateTimeOffset.UtcNow;
            device.LastError = null;
        }

        // 2. 更新点位并提取状态
        if (_points.TryGetValue(pointId, out var point))
        {
            object? oldValue = point.Value;

            // 使用 object.Equals 安全地比较装箱后的值类型或引用类型
            bool isChanged = !object.Equals(oldValue, value) || point.Quality != quality;

            // 无论是否变化，由于执行了扫描，时间戳应当更新（代表该时刻确认过此值）
            point.Value = value;
            point.Quality = quality;
            point.TimestampUtc = DateTimeOffset.UtcNow;

            // 核心优化：仅在数据或质量发生实质变化时，才触发事件通知订阅方（降低 OPC UA Server 的下发压力）
            if (isChanged)
            {
                PointValueChanged?.Invoke(point);
            }

            // 返回包含详细状态的元组
            return (true, isChanged, oldValue);
        }

        // 未找到对应点位
        return (false, false, null);
    }

    /// <inheritdoc/>
    public void MarkDeviceOnline(Guid deviceId)
    {
        if (_devices.TryGetValue(deviceId, out var device))
        {
            device.Status = DeviceStatus.Online;
            device.LastSeenUtc = DateTimeOffset.UtcNow;
            device.LastError = null;
        }
    }

    /// <inheritdoc/>
    public void MarkDeviceFault(Guid deviceId, string error)
    {
        if (_devices.TryGetValue(deviceId, out var device))
        {
            // 标记设备发生了故障及报错信息用于排查
            device.Status = DeviceStatus.Fault;
            device.LastError = error;
            device.LastSeenUtc = DateTimeOffset.UtcNow;
        }

        // 设备宕机则其下面的所有采集点位数据质量必然不可信任，一并标识为 "Bad"
        foreach (var point in _points.Values.Where(x => x.DeviceId == deviceId))
        {
            point.Quality = "Bad";
            point.TimestampUtc = DateTimeOffset.UtcNow;
        }
    }
}
