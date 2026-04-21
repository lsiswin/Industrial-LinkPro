using System.Text.RegularExpressions;
using IndustrialLinkPro.OpcServer.Configuration;
using IndustrialLinkPro.OpcServer.Contracts;
using IndustrialLinkPro.OpcServer.Runtime;
using Microsoft.Extensions.Options;
using S7.Net;
using S7.Net.Types;

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

    /// <summary>
    /// 批量读取多个点位数据的核心实现方法。通过分块读取和严格的索引映射，确保高效且准确地将 S7.Net 返回的原生 Object 列表转换为以 Guid ID 为键的结果字典。
    /// </summary>
    /// <param name="points"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task<IReadOnlyDictionary<Guid, object?>> BatchReadAsync(
        IReadOnlyCollection<DataPointDefinition> points,
        CancellationToken cancellationToken
    )
    {
        var results = new Dictionary<Guid, object?>();

        if (points == null || points.Count == 0)
            return results;

        // 1. 确保 PLC 处于连接状态
        if (!_plc.IsConnected)
        {
            await _plc.OpenAsync(cancellationToken);
        }

        // 将点位转换为列表以保证后续索引映射的一致性
        var pointList = points.ToList();
        var dataItems = pointList.Select(ParseToDataItem).ToList();

        // 2. 分块读取 (Chunking)
        // S7 协议的 PDU 通常限制在 240 或 480 字节。
        // 为了防止 ReadMultipleVars 报 "PDU Size Exceeded" 异常，建议每次请求 15~20 个变量。
        const int maxItemsPerRequest = 15;

        for (int i = 0; i < dataItems.Count; i += maxItemsPerRequest)
        {
            // 响应外部的取消请求
            cancellationToken.ThrowIfCancellationRequested();

            var chunkPoints = pointList.Skip(i).Take(maxItemsPerRequest).ToList();
            var chunkItems = dataItems.Skip(i).Take(maxItemsPerRequest).ToList();

            try
            {
                // 使用 S7.Net 提供的多变量批量读取接口，极大提高通讯效率
                var chunkValues = await _plc.ReadMultipleVarsAsync(chunkItems);

                // 3. 将读取结果的 Object 列表严格映射回原始的 Guid ID
                for (int j = 0; j < chunkPoints.Count; j++)
                {
                    results[chunkPoints[j].Id] = chunkValues[j].Value;
                }
            }
            catch (Exception ex)
            {
                // 建议接入系统日志：Log.Error($"批量读取分块发生异常，起始偏移: {i}", ex);
                // 这里选择抛出异常，或者根据业务需求给这批数据赋 null
                throw new InvalidOperationException(
                    $"S7 批量读取失败，设备位号 {chunkPoints.FirstOrDefault()?.Address} 附近发生错误。",
                    ex
                );
            }
        }

        return results;
    }

    /// <summary>
    /// 将DataPointDefinition的 Address 和 DataType 解析为 S7.Net 原生的 DataItem 结构
    /// </summary>
    private DataItem ParseToDataItem(DataPointDefinition point)
    {
        var dataItem = new DataItem
        {
            VarType = MapDataType(point.DataType),
            Count = 1, // 对于基础类型，Count 统一为 1
        };

        // 1. 解析 DB 块地址 (例如 "DB1.DBD10", "DB2.DBX3.1")
        var dbMatch = Regex.Match(
            point.Address,
            @"^DB(?<db>\d+)\.DB[BWDX]?(?<byte>\d+)(\.(?<bit>\d+))?$",
            RegexOptions.IgnoreCase
        );
        if (dbMatch.Success)
        {
            dataItem.DataType = DataType.DataBlock;
            dataItem.DB = int.Parse(dbMatch.Groups["db"].Value);
            dataItem.StartByteAdr = int.Parse(dbMatch.Groups["byte"].Value);
            if (dbMatch.Groups["bit"].Success)
            {
                dataItem.BitAdr = byte.Parse(dbMatch.Groups["bit"].Value);
            }
            return dataItem;
        }

        // 2. 解析 M/I/Q 等基本区地址 (例如 "M10.0", "MD20", "I0.0", "Q1.1")
        var mioMatch = Regex.Match(
            point.Address,
            @"^(?<area>[MIQ])[BWDX]?(?<byte>\d+)(\.(?<bit>\d+))?$",
            RegexOptions.IgnoreCase
        );
        if (mioMatch.Success)
        {
            string area = mioMatch.Groups["area"].Value.ToUpper();
            dataItem.DataType = area switch
            {
                "M" => DataType.Memory,
                "I" => DataType.Input,
                "Q" => DataType.Output,
                _ => DataType.Memory,
            };
            dataItem.DB = 0; // 非 DB 块，DB 号为 0
            dataItem.StartByteAdr = int.Parse(mioMatch.Groups["byte"].Value);
            if (mioMatch.Groups["bit"].Success)
            {
                dataItem.BitAdr = byte.Parse(mioMatch.Groups["bit"].Value);
            }
            return dataItem;
        }

        throw new ArgumentException($"无法解析的 PLC 地址格式: {point.Address} (ID: {point.Id})");
    }

    /// <summary>
    /// 字符串数据类型映射为 S7.Net VarType 枚举
    /// </summary>
    private VarType MapDataType(string dataType)
    {
        return dataType.ToLowerInvariant() switch
        {
            "bool" or "bit" => VarType.Bit,
            "byte" => VarType.Byte,
            "int" or "short" or "int16" => VarType.Int,
            "word" or "ushort" or "uint16" => VarType.Word,
            "dint" or "int32" => VarType.DInt,
            "dword" or "uint32" => VarType.DWord,
            "real" or "float" => VarType.Real,
            "lreal" or "double" => VarType.LReal,
            "string" => VarType.String,
            _ => throw new NotSupportedException($"不支持的数据类型: {dataType}"),
        };
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
