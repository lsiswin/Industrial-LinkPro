using IndustrialLinkPro.OpcServer.Configuration;
using IndustrialLinkPro.OpcServer.Contracts;
using IndustrialLinkPro.OpcServer.Runtime;
using Microsoft.Extensions.Options;
using S7.Net;

namespace IndustrialLinkPro.OpcServer.Drivers;

/// <summary>
/// 负责与西门子 PLC 系统利用其私有 S7 通信协议进行通信的实现类。当前支持 S7-1500 等主流设备。
/// </summary>
public sealed class S7DeviceDriver(
    DeviceRuntime deviceRuntime,
    ILogger<S7DeviceDriver> logger,
    IOptions<DriverOptions> options
) : IDeviceDriver
{
    private readonly DeviceRuntime _deviceRuntime = deviceRuntime;
    private readonly ILogger<S7DeviceDriver> _logger = logger;
    private readonly S7Options _options = options.Value.S7;
    private Plc? _plc;
    private DateTimeOffset? _lastSuccessUtc;
    private string? _lastError;

    /// <inheritdoc/>
    public Guid DeviceId => _deviceRuntime.DeviceId;

    /// <inheritdoc/>
    public bool IsConnected => _plc?.IsConnected == true;

    /// <inheritdoc/>
    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (IsConnected)
        {
            return;
        }

        // 解析连接字符串获取 IP 、机架(Rack)号、插槽(Slot)号配置
        var config = ConnectionStringParser.Parse(_deviceRuntime.ConnectionString);
        var host = config["host"];
        var rack = short.Parse(config.GetValueOrDefault("rack", "0"));
        var slot = short.Parse(config.GetValueOrDefault("slot", "1"));

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_options.ConnectTimeoutMs);

        // 初始化底层 S7 Plc 实例。
        _plc = new Plc(CpuType.S71500, host, rack, slot);

        // 鉴于旧版 S7.Net 的 Open 不支持异步令牌，将其包装到线程池异步任务中
        await Task.Run(() => _plc.Open(), timeoutCts.Token);
        _logger.LogInformation(
            "S7 驱动成功连接到目标地址 {Host}，所服务设备 ID: {DeviceId}",
            host,
            DeviceId
        );
    }

    /// <inheritdoc/>
    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        if (_plc is null)
        {
            return;
        }

        await Task.Run(() => _plc.Close(), cancellationToken);
        _logger.LogInformation("S7 驱动针对设备 {DeviceId} 已断开连接", DeviceId);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<Guid, object?>> BatchReadAsync(
        IReadOnlyCollection<DataPointDefinition> points,
        CancellationToken cancellationToken
    )
    {
        if (_plc is null || !_plc.IsConnected)
        {
            throw new InvalidOperationException(
                "S7 通信驱动连接丢失，无法执行采集 (S7 driver is not connected)."
            );
        }

        var result = new Dictionary<Guid, object?>();
        foreach (var point in points)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 基于数据点配置字典中的原始下位机地址读取对应的值。
            // 未来可修改为真正的多项组合读取 (ReadMultipleVariables) 优化耗时
            var value = await Task.Run(() => _plc.Read(point.Address), cancellationToken);
            result[point.Id] = ConvertS7Value(value, point.DataType);
        }

        _lastSuccessUtc = DateTimeOffset.UtcNow;
        _lastError = null;
        return result;
    }

    /// <inheritdoc/>
    public Task WriteAsync(
        DataPointDefinition point,
        object? value,
        CancellationToken cancellationToken
    )
    {
        throw new NotSupportedException(
            "V1版本未开放通过 OPC UA 控制写入 PLC 功能 (OPC UA write-through is not enabled in the first version)."
        );
    }

    /// <inheritdoc/>
    public DriverHealthSnapshot GetHealthSnapshot()
    {
        return new DriverHealthSnapshot(
            IsConnected,
            IsConnected ? "Connected" : "Disconnected",
            _lastSuccessUtc,
            _lastError
        );
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await DisconnectAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
        }
        finally
        {
            _plc?.Close();
        }
    }

    /// <summary>
    /// 将 S7 底层返回的原生 object 类型转换为统一的标准类型进行向上层 OPC UA 的组装。
    /// </summary>
    private static object? ConvertS7Value(object? value, string dataType)
    {
        if (value is null)
        {
            return null;
        }

        // 根据预定义的转换规则进行强制拆箱转型
        return dataType.ToLowerInvariant() switch
        {
            "bool" => Convert.ToBoolean(value),
            "int" => Convert.ToInt32(value),
            "double" => Convert.ToDouble(value),
            "float" => Convert.ToSingle(value),
            "string" => Convert.ToString(value),
            _ => value,
        };
    }
}
