using System.Net.Sockets;
using IndustrialLinkPro.OpcServer.Configuration;
using IndustrialLinkPro.OpcServer.Contracts;
using IndustrialLinkPro.OpcServer.Runtime;
using Microsoft.Extensions.Options;
using NModbus;

namespace IndustrialLinkPro.OpcServer.Drivers;

/// <summary>
/// 负责与第三方基于 Modbus TCP (以太网) 协议的工业设备进行通信的底层驱动实现
/// </summary>
public sealed class ModbusTcpDeviceDriver(
    DeviceRuntime deviceRuntime,
    ILogger<ModbusTcpDeviceDriver> logger,
    IOptions<DriverOptions> options
) : IDeviceDriver
{
    private readonly DeviceRuntime _deviceRuntime = deviceRuntime;
    private readonly ILogger<ModbusTcpDeviceDriver> _logger = logger;
    private readonly ModbusTcpOptions _options = options.Value.ModbusTcp;
    private readonly ModbusFactory _modbusFactory = new();
    private TcpClient? _tcpClient;
    private IModbusMaster? _master;
    private byte _unitId = 1;
    private DateTimeOffset? _lastSuccessUtc;
    private string? _lastError;

    /// <inheritdoc/>
    public Guid DeviceId => _deviceRuntime.DeviceId;

    /// <inheritdoc/>
    public bool IsConnected => _tcpClient?.Connected == true && _master is not null;

    /// <inheritdoc/>
    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (IsConnected)
        {
            return;
        }

        // 解析连接字符串提取目标主机和端口，格式例如 "host=192.168.1.10;port=502;unitId=1"
        var config = ConnectionStringParser.Parse(_deviceRuntime.ConnectionString);
        var host = config["host"];
        var port = int.Parse(config.GetValueOrDefault("port", "502"));
        _unitId = byte.Parse(config.GetValueOrDefault("unitId", "1"));

        _tcpClient = new TcpClient();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_options.ConnectTimeoutMs);
        
        // 尝试非阻塞发起 TCP Socket 连接
        await _tcpClient.ConnectAsync(host, port, timeoutCts.Token);
        _master = _modbusFactory.CreateMaster(_tcpClient);
        _logger.LogInformation("ModbusTcp 驱动成功连接到目标 {Host}:{Port}，所服务设备 ID: {DeviceId}", host, port, DeviceId);
    }

    /// <inheritdoc/>
    public Task DisconnectAsync(CancellationToken cancellationToken)
    {
        _master?.Dispose();
        _master = null;
        _tcpClient?.Dispose();
        _tcpClient = null;
        _logger.LogInformation("ModbusTcp 驱动针对设备 {DeviceId} 已断开连接", DeviceId);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<Guid, object?>> BatchReadAsync(
        IReadOnlyCollection<DataPointDefinition> points,
        CancellationToken cancellationToken)
    {
        if (_master is null)
        {
            throw new InvalidOperationException("ModbusTcp 当前未连接，无法执行读取操作 (driver is not connected).");
        }

        var result = new Dictionary<Guid, object?>();
        foreach (var point in points)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // 在实际工业业务中，常常通过配置或点位的名字来判定该读取 HoldingRegisters、InputRegisters 或 Coils.
            // 本项目初步实现默认采用 ReadHoldingRegisters 进行寄存器读取。
            var registerAddress = ushort.Parse(point.Address);
            var registersToRead = GetRegisterCount(point.DataType);
            var registers = await _master.ReadHoldingRegistersAsync(_unitId, registerAddress, registersToRead);
            
            // 尝试按照指定数据类型解析响应的字节
            result[point.Id] = ConvertModbusValue(registers, point.DataType);
        }

        _lastSuccessUtc = DateTimeOffset.UtcNow;
        _lastError = null;
        return result;
    }

    /// <inheritdoc/>
    public Task WriteAsync(DataPointDefinition point, object? value, CancellationToken cancellationToken)
    {
        throw new NotSupportedException("V1版本未开放通过 OPC UA 直接向 Modbus 设备写入数据功能 (OPC UA write-through is not enabled in the first version).");
    }

    /// <inheritdoc/>
    public DriverHealthSnapshot GetHealthSnapshot()
    {
        return new DriverHealthSnapshot(IsConnected, IsConnected ? "Connected" : "Disconnected", _lastSuccessUtc, _lastError);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync(CancellationToken.None);
    }

    /// <summary>
    /// 根据配置的映射数据类型，计算该类型在 Modbus 中占据的 16 位寄存器数量
    /// </summary>
    private static ushort GetRegisterCount(string dataType)
    {
        return dataType.ToLowerInvariant() switch
        {
            "bool" => 1,
            "int" => 1,
            "float" => 2,     // IEEE-754 单精度浮点占 32 位，计 2 个寄存器
            "double" => 4,    // IEEE-754 双精度浮点占 64 位，计 4 个寄存器
            _ => 1
        };
    }

    /// <summary>
    /// 将从 Modbus 中读取到的原生 16 位寄存器数组 (ushort[]) 转换成目标数据类型
    /// </summary>
    private static object ConvertModbusValue(ushort[] registers, string dataType)
    {
        return dataType.ToLowerInvariant() switch
        {
            "bool" => registers[0] != 0,
            "int" => (short)registers[0],
            "float" => BitConverter.ToSingle(ToBytes(registers), 0),
            "double" => BitConverter.ToDouble(ToBytes(registers), 0),
            "string" => string.Join(",", registers),
            _ => registers[0]
        };
    }

    /// <summary>
    /// 辅助字节操作，用于提取多寄存器的字节串联，注意此处的实现使用系统原生字节序进行翻转（由于 Modbus 通常为大端序）
    /// </summary>
    private static byte[] ToBytes(ushort[] registers)
    {
        var bytes = new byte[registers.Length * 2];
        for (var i = 0; i < registers.Length; i++)
        {
            bytes[i * 2] = (byte)(registers[i] >> 8);
            bytes[i * 2 + 1] = (byte)(registers[i] & 0xFF);
        }

        // 处理 .NET 层面本地执行的字节序如果跟 PLC 端不同，则予以翻转
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }

        return bytes;
    }
}
