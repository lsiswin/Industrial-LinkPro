using IndustrialLinkPro.OpcServer.Contracts;

namespace IndustrialLinkPro.OpcServer.Drivers;

/// <summary>
/// 标准化的底层设备通信驱动接口，所有支持的工业协议（如 S7, Modbus 等）都需要实现此接口
/// </summary>
public interface IDeviceDriver : IAsyncDisposable
{
    /// <summary>
    /// 获取当前驱动程序实例绑定的设备唯一 ID
    /// </summary>
    Guid DeviceId { get; }

    /// <summary>
    /// 获取当前是否与设备已建立底层网络连接
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// 异步建立与目标工业设备的连接
    /// </summary>
    /// <param name="cancellationToken">用于取消连接请求的令牌</param>
    Task ConnectAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 主动断开与目标设备的连接并清理相关网络资源
    /// </summary>
    Task DisconnectAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 批量从设备中采集给出点位集合的数据
    /// </summary>
    /// <param name="points">需要读取的数据点定义集合</param>
    /// <param name="cancellationToken">取消请求的令牌</param>
    /// <returns>返回包含点位 ID 以及对应值的字典。如果某项不支持或缺失可能返回 null</returns>
    Task<IReadOnlyDictionary<Guid, object?>> BatchReadAsync(
        IReadOnlyCollection<DataPointDefinition> points,
        CancellationToken cancellationToken);

    /// <summary>
    /// 将指定的数据值写入设备的对应点位中（下发控制指令）
    /// </summary>
    Task WriteAsync(DataPointDefinition point, object? value, CancellationToken cancellationToken);

    /// <summary>
    /// 获取该驱动实例当前的监控健康指标及诊断快照
    /// </summary>
    DriverHealthSnapshot GetHealthSnapshot();
}
