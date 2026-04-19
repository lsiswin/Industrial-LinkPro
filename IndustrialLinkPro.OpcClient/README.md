# IndustrialLinkPro.OpcClient

OPC UA 客户端服务库，用于连接到 IndustrialLinkPro OpcServer 并订阅实时数据。

## 功能特性

- ✅ **连接管理**: 自动连接和重连机制
- ✅ **数据订阅**: 基于 OPC UA 订阅机制的实时数据推送
- ✅ **数据缓存**: 内存缓存最新数据点值
- ✅ **健康监控**: 定期检查连接状态和数据更新延迟
- ✅ **事件驱动**: 数据变更和连接状态变更事件通知

## 快速开始

### 1. 在项目中引用

```xml
<ProjectReference Include="..\IndustrialLinkPro.OpcClient\IndustrialLinkPro.OpcClient.csproj" />
```

### 2. 注册服务

在 `App.axaml.cs` 或 `Program.cs` 中：

```csharp
using IndustrialLinkPro.OpcClient;

// 在 ConfigureServices 或 RegisterTypes 中
services.AddOpcClientServices(configuration);
```

### 3. 配置 appsettings.json

```json
{
  "OpcClient": {
    "EndpointUrl": "opc.tcp://localhost:4842/IndustrialLinkPro",
    "ApplicationName": "IndustrialLinkPro OPC Client",
    "SessionTimeoutMs": 60000,
    "PublishingIntervalMs": 1000,
    "SamplingIntervalMs": 500,
    "QueueSize": 10,
    "AutoAcceptUntrustedCertificates": true,
    "ReconnectDelayMs": 5000,
    "HealthCheckIntervalSeconds": 30
  }
}
```

### 4. 使用示例

#### 订阅数据并监听变更

```csharp
public class MyViewModel
{
    private readonly OpcClientService _opcClient;
    
    public MyViewModel(OpcClientService opcClient)
    {
        _opcClient = opcClient;
        
        // 监听数据变更
        _opcClient.DataChanged += OnDataChanged;
        _opcClient.ConnectionStatusChanged += OnConnectionStatusChanged;
    }
    
    // 订阅数据点
    public async Task SubscribeAsync()
    {
        var nodeIds = new[]
        {
            "ns=2;s=Devices.Siemens_S7_1500.DataPoints.Temperature",
            "ns=2;s=Devices.Siemens_S7_1500.DataPoints.Pressure"
        };
        
        await _opcClient.SubscribeToNodesAsync(nodeIds);
    }
    
    private void OnDataChanged(object? sender, DataChangedEventArgs e)
    {
        // 更新 UI
        Console.WriteLine($"节点 {e.NodeId} 值变更: {e.DataPoint.Value}");
    }
    
    private void OnConnectionStatusChanged(object? sender, ConnectionStatusChangedEventArgs e)
    {
        Console.WriteLine($"连接状态: {e.OldStatus} -> {e.NewStatus}");
    }
}
```

#### 读取单个节点

```csharp
var dataPoint = await _opcClient.ReadNodeAsync("ns=2;s=Devices.Device1.DataPoints.Value1");
Console.WriteLine($"当前值: {dataPoint.Value}");
```

#### 获取缓存数据

```csharp
// 获取单个缓存值
var cachedValue = _opcClient.GetCachedValue("ns=2;s=Devices.Device1.DataPoints.Value1");

// 获取所有缓存值
var allCachedValues = _opcClient.GetAllCachedValues();
```

## 架构说明

```
OpcClientService (核心服务)
├── ConnectAsync()       - 连接到 OPC UA 服务器
├── DisconnectAsync()    - 断开连接
├── SubscribeToNodesAsync() - 订阅数据点
├── ReadNodeAsync()      - 读取单个节点
├── GetCachedValue()     - 获取缓存数据
└── Events               - 数据变更和连接状态变更事件

DataCacheService (缓存服务)
├── Update()             - 更新缓存
├── Get()                - 获取单个值
├── GetAll()             - 获取所有值
└── Remove()             - 移除缓存项

HealthMonitorService (健康监控)
├── 定期检查连接状态
├── 检测超时数据点
└── 自动重连
```

## OPC UA 节点 ID 格式

IndustrialLinkPro OpcServer 的地址空间结构：

```
Objects/
  Devices/
    {DeviceName}/
      Metadata/
        DeviceId
        DeviceType
        ProtocolType
        ConnectionString
        Status
      DataPoints/
        {PointName}  <-- 订阅这些节点获取实时数据
```

**节点 ID 示例：**
- `ns=2;s=Devices.Siemens_S7_1500.DataPoints.Temperature`
- `ns=2;s=Devices.ABB_IRB_1200.DataPoints.Position_X`
- `ns=2;s=Devices.HIKROBOT_Camera.DataPoints.Defect_Count`

## 配置选项说明

| 选项 | 默认值 | 说明 |
|------|--------|------|
| EndpointUrl | opc.tcp://localhost:4842/IndustrialLinkPro | OPC UA 服务器地址 |
| ApplicationName | IndustrialLinkPro OPC Client | 客户端应用名称 |
| SessionTimeoutMs | 60000 | 会话超时(毫秒) |
| PublishingIntervalMs | 1000 | 订阅发布间隔(毫秒) |
| SamplingIntervalMs | 500 | 采样间隔(毫秒) |
| QueueSize | 10 | 监控项队列大小 |
| AutoAcceptUntrustedCertificates | true | 自动接受不受信任的证书 |
| ReconnectDelayMs | 5000 | 重连延迟(毫秒) |
| HealthCheckIntervalSeconds | 30 | 健康检查间隔(秒) |

## 注意事项

1. **证书安全**: 生产环境应设置 `AutoAcceptUntrustedCertificates = false` 并配置正确的证书
2. **订阅数量**: 避免一次性订阅过多节点，建议分批订阅
3. **数据更新频率**: 根据实际需求调整 `SamplingIntervalMs` 和 `PublishingIntervalMs`
4. **内存使用**: 定期清理不再需要的订阅和缓存数据
