using System.Collections.Concurrent;
using IndustrialLinkPro.OpcClient.Models;

namespace IndustrialLinkPro.OpcClient.Services;

/// <summary>
/// 数据缓存服务
/// 用于缓存 OPC UA 数据点的最新值
/// </summary>
public class DataCacheService
{
    private readonly ConcurrentDictionary<string, DataPointValue> _cache = new();

    /// <summary>
    /// 更新数据点缓存
    /// </summary>
    public void Update(DataPointValue dataPoint)
    {
        _cache.AddOrUpdate(
            dataPoint.NodeId,
            dataPoint,
            (key, existing) => dataPoint);
    }

    /// <summary>
    /// 获取单个数据点的缓存值
    /// </summary>
    public DataPointValue? Get(string nodeId)
    {
        return _cache.GetValueOrDefault(nodeId);
    }

    /// <summary>
    /// 获取所有缓存的数据点
    /// </summary>
    public IReadOnlyDictionary<string, DataPointValue> GetAll()
    {
        return _cache.ToDictionary(x => x.Key, x => x.Value);
    }

    /// <summary>
    /// 移除指定数据点
    /// </summary>
    public void Remove(string nodeId)
    {
        _cache.TryRemove(nodeId, out _);
    }

    /// <summary>
    /// 清空所有缓存
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
    }

    /// <summary>
    /// 获取缓存的数据点数量
    /// </summary>
    public int Count => _cache.Count;

    /// <summary>
    /// 检查指定数据点是否在缓存中
    /// </summary>
    public bool Contains(string nodeId)
    {
        return _cache.ContainsKey(nodeId);
    }
}
