# Industrial-LinkPro 工业互联平台

Industrial-LinkPro 是一套高性能、模块化的工业数据采集与监控解决方案。它基于 .NET 10 开发，通过 OPC UA 协议将异构的工业设备（如西门子 S7 PLC、Modbus 设备等）抽象为统一的数据模型，并提供实时监控桌面客户端。

## 🌟 核心特性

- **多协议支持**：内置 Modbus TCP 和 Siemens S7 协议驱动，支持工业设备快速接入。
- **动态 OPC UA 服务**：动态生成地址空间，实时同步设备定义与数据点，支持标准 OPC UA 客户端接入。
- **高性能采集**：采用生产者-消费者模型，支持大规模数据点的并行采集与历史缓冲区维护。
- **跨平台管理端**：基于 Avalonia UI 打造的高颜值桌面客户端，支持设备管理、报警监控与数据分析。
- **微服务/插件化架构**：核心逻辑与协议驱动解耦，易于扩展新协议或集成第三方 API。

## 🏗 模块说明

| 模块名称 | 职责描述 |
| --- | --- |
| **IndustrialLinkPro.OpcServer** | **核心服务端**。负责设备定义同步、驱动加载、原始数据采集及 OPC UA 服务宿主。可作为 Windows 服务运行。 |
| **IndustrialLinkPro.OpcClient** | **客户端 SDK**。封装了与 OpcServer 的连接管理、自动重连、数据订阅与二级缓存逻辑。 |
| **RadioIndustrialApp** | **桌面监控端**。基于 MVVM 模式开发的 Avalonia 应用，提供仪表盘、设备状态、报警看板等功能。 |
| **RadioIndustriaLibrary** | **通用基础库**。定义跨模块的枚举（如设备状态）、共享模型和事件契约。 |

## 🛠 技术栈

- **后端**: .NET 10, ASP.NET Core
- **工业通讯**: OPC UA (OPC Foundation SDK), Modbus TCP, S7NetPlus
- **桌面 UI**: Avalonia UI (MVVM)
- **依赖注入**: Microsoft.Extensions.DependencyInjection
- **配置与日志**: Microsoft.Extensions.Options, Serilog/Trace

## 🚀 快速开始

### 前置要求

- 安装 [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- 建议使用 IDE: JetBrains Rider 或 Visual Studio 2022+

### 1. 运行服务端

```bash
cd IndustrialLinkPro.OpcServer
# 修改 appsettings.json 中的 DeviceApi 配置（如适用）
dotnet run
```

默认情况下，OPC UA 服务将监听 `opc.tcp://localhost:4842/IndustrialLinkPro`。

### 2. 启动管理端

```bash
cd RadioIndustrialApp
dotnet run
```

## ⚙️ 核心配置

### OpcServer 配置

在 `IndustrialLinkPro.OpcServer/appsettings.json` 中：

- `OpcServerOptions`: 设置服务器端口、证书名及应用描述。
- `DeviceApiOptions`: 配置从哪个外部 API 同步设备资产定义。
- `DriverOptions`: 配置各协议驱动的全局超时与重试参数。

### OpcClient 配置

在客户端应用的 `appsettings.json` 中：

- `EndpointUrl`: 指向服务端地址。
- `PublishingIntervalMs`: 数据订阅的刷新频率。

## 📐 架构逻辑

1. **DefinitionSyncWorker**: 定期从 API 同步最新的设备与点位定义。
2. **DriverFactory**: 根据连接字符串（如 `s7://192.168.1.10`）动态实例化协议驱动。
3. **DataAcquisitionWorker**: 循环读取各驱动数据，更新 `RuntimeModel` 内存状态。
4. **OpcUaServerHost**: 将 `RuntimeModel` 中的数据映射到 OPC UA 地址空间，供外部订阅。
5. **HistorySyncWorker**: 负责将采集到的瞬时数据同步至历史缓冲区或外部存储。

---

© 2026 Industrial-LinkPro Team. All Rights Reserved.
