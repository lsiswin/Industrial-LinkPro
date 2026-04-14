我将先在工程里查找关键文件（App.axaml.cs、DashboardCardModel、Services、ViewModels、配置相关文件）以获取上下文，预期结果是这些文件路径与内容概览，之后据此起草实现计划。接下来我会并行运行几个只读搜索。## Plan: 在 RadioIndustrialApp 中集成 OPC UA 客户端

TL;DR：在现有 Avalonia + Prism 项目中引入官方 OPC Foundation .NET Standard SDK（NuGet 包 `OPCFoundation.NetStandard.Opc.Ua`），实现以 OPC UA Subscription 为主的实时采集。方案包括独立的 `IOpcUaService` 服务（负责连接/订阅/重连/安全），ViewModel 注入与 Dashboard 显示，配置文件驱动，配套单元/集成测试与运维注意点。下列任务按优先级从必须到可选细分为可独立完成的小任务，并给出需要新增/修改的文件清单与关键方法签名/伪代码（便于开发者直接实施）。

### Steps
1. 设计并实现 `IOpcUaService` 与 `OpcUaService`；在 `App.axaml.cs` 注册服务（容器单例）。  
2. 增加配置文件 `opcsettings.json` 并实现 `OpcSettings` 配置绑定/验证。  
3. 在 Services 目录实现连接、订阅、消息分发（事件/回调/Channel），并实现证书加载与信任策略。  
4. 在 ViewModel 注入 `IOpcUaService`（示例：`IndexViewModel`/`DeviceViewModel`/`SettingViewModel`），将订阅数据映射到 `DashboardCardModel.OpcCommMetric`。  
5. 编写单元与集成测试（含模拟 OPC UA 服务器或 Mocks），增加重连与回退测试。  
6. 可选：性能优化、批量订阅/批量消息处理、桥接到 DB/消息总线（Kafka/Redis/AMQP）。

### Further Considerations
1. 配置选项：支持用户名/密码与安全证书两种认证方式（优先证书）。  
2. 选项 A：单一长期订阅（简单）。B：每节点单独订阅（灵活但更多资源）。C：批量分组订阅（折中）。建议默认使用分组订阅。  

---

## 1. 高级设计（架构与组件概览）

- 架构图（逻辑层级）：
  - UI (Avalonia View/ViewModel)
  - Application Services (Prism DI)
    - IOpcUaService (核心)：负责连接/会话管理、订阅管理、数据分发
    - IOpcUaRepository（可选）：持久化接收的数据到 DB/队列
    - IOpcUaConfigurationProvider：从 `opcsettings.json` 读取并热加载配置
  - 下层: OPC UA SDK (`OPCFoundation.NetStandard.Opc.Ua`)
  - 辅助: 证书管理 (X509 存储/文件)、日志（Serilog/ILogger）、策略（Polly 风格退避）

- 组件职责：
  - `IOpcUaService`: Connect/Disconnect, CreateSubscription, AddMonitoredItem, RemoveMonitoredItem, OnDataChanged (事件或 IAsyncEnumerable/Channel)
  - `OpcUaService`（实现）: 维护单个或多个 Session；管理 Subscription 对象并处理回调；实现重连/backoff/熔断
  - `OpcSettings`（配置 POCO）: Endpoints 列表、认证、订阅参数、节点分组等
  - `OpcMessageDispatcher`（可选）: 将原始 DataChange 转为应用内事件或 DTO，并触发 ViewModel 更新或写入队列

- 依赖：
  - NuGet: `OPCFoundation.NetStandard.Opc.Ua`（必需）
  - 推荐日志: `Serilog` 或 使用项目现有 ILogger
  - 可能的测试依赖: `Moq`、`xUnit`/`NUnit`、本地 OPC UA 模拟器（如 UA-.NET Standard Sample Server 或第三方模拟器）

- 证书/安全：
  - 使用 SDK 的应用证书（X509）进行安全连接；提供证书生成/导入工具或在首次运行时自动创建并放在 `Certificates/` 目录
  - 支持两种认证：匿名/用户名密码（简单开发）与证书（生产推荐）
  - 证书信任：在首次遇到未信任证书时，按配置自动信任或要求手动导入（安全提示日志）

- 订阅策略（推荐默认）：
  - 分组订阅（Group per Device/Functionality）：每个设备或每类节点创建一个 Subscription，Subscription 的 PublishingInterval 以设备能力和业务需求配置
  - MonitoredItem SamplingInterval 与 QueueSize：默认 SamplingInterval = 1000ms，可在 `opcsettings.json` 覆盖；QueueSize = 10，DiscardOldest = true
  - 报警/关键点采用更短的间隔（例如 200ms）

- 重连/回退策略：
  - 指数退避（初始 1s，最多 30s，最多重试 N 次），重试后触发熔断（停止尝试，等待人工干预或较长冷却期）
  - Session KeepAlive 事件处理：在 KeepAlive 发现问题时尝试重建 Session，并保留已配置的 MonitoredItems
  - 连接阶段：DNS/端口不可达 -> 快速失败并进入退避 -> 后台重连任务继续尝试

---

## 2. 具体任务清单（按优先级：必须 -> 强烈建议 -> 可选）
每项都包含：输入、输出、错误模式、成功标准、估算工时、优先级。

1) 必须：添加 NuGet 依赖与项目配置
- 输入：修改 `RadioIndustrialApp.csproj`（或通过 nuget 命令）
- 输出：项目能引用 `OPCFoundation.NetStandard.Opc.Ua`
- 错误模式：包冲突、目标框架不兼容（项目为 net6/7/8/10？需确认）
- 成功标准：能编译并在代码中 `using Opc.Ua;` 无错
- 估算：0.5-1 小时
- 优先级：高

2) 必须：创建配置 POCO 与默认配置文件 `opcsettings.json`
- 输入：新增 `opcsettings.json` + `Models/OpcSettings.cs`
- 输出：可绑定配置对象 `OpcSettings`，并在启动时加载
- 错误模式：JSON 格式错误、缺少必需字段
- 成功标准：配置能成功反序列化并提供默认值
- 估算：1-2 小时
- 优先级：高

3) 必须：定义接口 `IOpcUaService`（放 `Services/IOpcUaService.cs`）
- 输入：接口设计（方法与事件）
- 输出：接口文件
- 错误模式：接口不全导致实现反复修改
- 成功标准：接口能覆盖连接/订阅/事件通知基本功能
- 估算：1 小时
- 优先级：高
- 建议接口方法签名（简短）：`Task StartAsync(CancellationToken)`, `Task StopAsync()`, `Task ConnectAsync(string endpoint)`, `Task DisconnectAsync()`, `Task<SubscriptionHandle> CreateSubscriptionAsync(SubscriptionOptions)`, `event EventHandler<OpcDataEventArgs> DataChanged`

4) 必须：实现 `OpcUaService`（`Services/OpcUaService.cs`）
- 输入：`IOpcUaService`、`OpcSettings`、OPC SDK
- 输出：会话管理、订阅管理、数据分发实现
- 错误模式：Session 创建失败、MonitoredItem 回调异常、序列化慢导致阻塞
- 成功标准：能连接到测试服务器并收到 DataChange 回调
- 估算：2-4 天（取决于熟悉度）
- 优先级：高
- 关键伪代码：
  - Session 创建：`var app = new ApplicationInstance(...); app.LoadApplicationConfiguration(...); app.CheckApplicationInstanceCertificate(...); var endpoint = CoreClientUtils.SelectEndpoint(endpointUrl, useSecurity); var session = await Session.Create(...)`
  - 创建 Subscription：`var sub = new Subscription(session.DefaultSubscription) { PublishingInterval = settings.PublishingInterval }; session.AddSubscription(sub); sub.Create();`
  - 添加 MonitoredItem：`var mi = new MonitoredItem(sub.DefaultItem) { StartNodeId = nodeId, SamplingInterval = settings.SamplingInterval }; mi.Notification += OnDataChange; sub.AddItem(mi); sub.ApplyChanges();`

5) 必须：实现证书加载/信任策略（`Services/CertificateManager.cs`）
- 输入：证书路径/配置
- 输出：X509Certificate2 对象与信任函数
- 错误模式：证书权限、Windows 存储访问错误
- 成功标准：能在首次运行生成/加载证书并用于建立安全 Session
- 估算：4-8 小时
- 优先级：高

6) 必须：在 `App.axaml.cs` 注册服务（DI）
- 输入：修改 `RegisterTypes` 方法
- 输出：容器中可解析 `IOpcUaService`
- 错误模式：注册生命周期不当（每次注入新实例导致重复连接）
- 成功标准：通过容器获取到单例服务
- 估算：0.5 小时
- 优先级：高
- 示例行（放在 `RegisterTypes` 中）：`containerRegistry.RegisterSingleton<IOpcUaService, OpcUaService>();`

7) 必须：在 `IndexViewModel` 注入并使用 `IOpcUaService`
- 输入：修改 `IndexViewModel` 构造函数
- 输出：ViewModel 能订阅 `DataChanged` 并更新 `DashboardCards` 中的 `OpcCommMetric`
- 错误模式：线程/同步问题（UI 线程更新）
- 成功标准：接收到数据后 UI 实时刷新（使用 Dispatcher/同步上下文）
- 估算：4-8 小时（包含 UI 数据绑定修改）
- 优先级：高
- 示例注入签名：`public IndexViewModel(IOpcUaService opcService) { _opc = opcService; _opc.DataChanged += OnOpcData; }`
- 更新模型：在 `OnOpcData` 中找到对应 `DashboardCardModel` 并更新其 `FooterContent`（或直接将 `FooterContent` 改为 `IOpcMetric` 强类型）

8) 强烈建议：实现 `IOpcMessageDispatcher`（`Services/OpcMessageDispatcher.cs`）
- 输入：OpcUaService 输出的原始回调
- 输出：标准化 DTO（例如 `OpcDataDto`）
- 错误模式：阻塞主线程，序列化慢
- 成功标准：回调非阻塞、可插拔
- 估算：1-2 天
- 优先级：中

9) 强烈建议：实现配置热加载（文件变更触发重新加载订阅）
- 输入：`FileSystemWatcher` 监听 `opcsettings.json`
- 输出：在安全范围内动态调整 Subscription
- 错误模式：配置不一致导致重复订阅
- 成功标准：配置变更后订阅被平滑更新
- 估算：1 天
- 优先级：中

10) 单元/集成测试（必须）
- 输入：Mock/模拟服务器或 SDK 的抽象点（可将 Session/Subscription 封装为接口）
- 输出：测试用例（连接成功、订阅到数据、断线重连）
- 错误模式：测试对真实服务器依赖导致不稳定
- 成功标准：通过 CI 的自动测试套件
- 估算：1-2 天
- 优先级：高
- 具体测试用例见后文

11) 可选：持久化桥接（DB 或 消息总线）
- 输入：`IOpcUaRepository` 接口与实现
- 输出：将数据写入 DB 或发布到 Kafka/Redis
- 估算：1-3 天
- 优先级：可选

12) 可选：批量订阅/性能调优（大量点位）
- 输入：压力测试结果
- 输出：分组策略、线程池/Channel 优化
- 估算：2-5 天
- 优先级：可选

---

## 3. 需要新增或修改的文件清单（相对路径）与内容要点

说明：不写完整大文件，仅列出类/接口/方法签名与关键伪代码片段。

- 新增：`RadioIndustrialApp/Services/IOpcUaService.cs`
  - 描述：服务接口
  - 关键方法/事件：
    - `Task StartAsync(CancellationToken ct)`
    - `Task StopAsync()`
    - `Task ConnectAsync(string endpointOrName)`
    - `Task DisconnectAsync()`
    - `Task<SubscriptionHandle> CreateSubscriptionAsync(SubscriptionOptions options)`
    - `Task AddMonitoredItemAsync(SubscriptionHandle handle, string nodeId, MonitoredItemOptions options)`
    - `event EventHandler<OpcDataEventArgs> DataChanged`
  - 错误模式：抛出明确异常类型（OpcConnectionException/SubscriptionException）

- 新增：`RadioIndustrialApp/Services/OpcUaService.cs`
  - 描述：实现细节，使用 `Opc.Ua` SDK
  - 关键字段：`ApplicationConfiguration _config`, `Session _session`, `List<Subscription> _subscriptions`, `CancellationTokenSource _cts`
  - 关键方法：
    - `private Task EnsureApplicationConfiguredAsync()` // 证书加载/生成
    - `public async Task ConnectAsync(string endpoint)` // 选择 endpoint、建立 session
    - `private void OnKeepAlive(Session sender, KeepAliveEventArgs e)` // 处理保持活性
    - `private void OnMonitoredItemNotification(MonitoredItem item, MonitoredItemNotificationEventArgs e)` // 转换并触发 DataChanged
    - 重连伪逻辑：若 KeepAlive 错误 -> ScheduleReconnectWithBackoff()
  - 伪代码片段（单行描述）：
    - 创建 Session：`Session.Create(appConfig, endpointUrl, ...);`
    - 建立 Subscription：`var sub = new Subscription(session.DefaultSubscription) { PublishingInterval = options.PublishingInterval }; session.AddSubscription(sub); sub.Create();`
    - 添加 MonitoredItem：`mi.Notification += OnMonitoredItemNotification; sub.AddItem(mi); sub.ApplyChanges();`

- 新增：`RadioIndustrialApp/Services/CertificateManager.cs`
  - 描述：封装证书生成/加载/信任逻辑
  - 方法签名：`Task<X509Certificate2> GetOrCreateApplicationCertificateAsync(ApplicationConfiguration cfg)`, `bool IsCertificateTrusted(X509Certificate2 cert)`, `void TrustCertificate(X509Certificate2 cert)`

- 新增/修改：`RadioIndustrialApp/Models/OpcSettings.cs`
  - 描述：配置 POCO
  - 属性示例：
    - `List<OpcEndpoint> Endpoints`
    - `AuthMode AuthMode` (Enum: Certificate/UserPassword/Anonymous)
    - `string CertificatePath`
    - `int PublishingInterval` (ms)
    - `int SamplingInterval` (ms)
    - `int QueueSize`
    - `List<OpcNodeDefinition> Nodes`（节点列表或分组）
  - `OpcNodeDefinition` 包括 `string NodeId`, `string DisplayName`, `string GroupId`

- 新增：`RadioIndustrialApp/opcsettings.json`
  - 描述：示例/默认配置（见下文“配置文件方案”部分）

- 修改：`RadioIndustrialApp/App.axaml.cs`
  - 描述：在 `RegisterTypes` 中注册 `IOpcUaService`
  - 示例单行：`containerRegistry.RegisterSingleton<IOpcUaService, OpcUaService>();`

- 修改：`RadioIndustrialApp/ViewModels/IndexViewModel.cs`（和可能的 `DeviceViewModel.cs`, `SettingViewModel.cs`）
  - 描述：注入 `IOpcUaService`、订阅 `DataChanged`、将数据映射到 `DashboardCardModel.FooterContent`（`OpcCommMetric`）
  - 构造器示例签名：`public IndexViewModel(IOpcUaService opcService)`；在内部注册事件：`_opcService.DataChanged += OnOpcDataChanged;`
  - OnOpcDataChanged 伪代码：找到对应 DashboardCard（例如通过 Endpoint 或 DeviceId），更新其 `FooterContent`，并在 UI 线程调度刷新（使用 Dispatcher/UI thread）

- 可选：`RadioIndustrialApp/Services/OpcMessageDispatcher.cs`
  - 描述：标准化事件到 DTO 并分发到多个消费者（ViewModel/Repository）

- 新增测试文件夹：`RadioIndustrialApp.Tests/`（单元/集成测试）
  - 描述：包含对 `OpcUaService` 的单元测试、断线重连测试（使用 Mock 或本地模拟服务器）
  - 关键测试类：`OpcUaServiceTests`, `OpcReconnectionTests`, `OpcSubscriptionTests`

---

## 4. 在 `App.axaml.cs` 中注册服务的示例（单行）
在 `protected override void RegisterTypes(IContainerRegistry containerRegistry)` 内加入：
`containerRegistry.RegisterSingleton<IOpcUaService, OpcUaService>();`

（如果需要注册为延迟启动，可先 RegisterSingleton<IOpcUaService>(() => new OpcUaService(...)) 并传入配置/Logger）

---

## 5. ViewModel 层如何注入并使用 `IOpcUaService` 与绑定示例

- 注入（构造函数注入）：
  - `public IndexViewModel(IOpcUaService opcService) { _opcService = opcService; _opcService.DataChanged += OnOpcDataChanged; }`
- DataChanged 处理（伪代码）：
  - `private void OnOpcDataChanged(object sender, OpcDataEventArgs e) { Dispatcher.UIThread.Post(() => UpdateDashboard(e)); }`
- 更新 DashboardCardModel 中的 `OpcCommMetric`：
  - 找到 DashboardCards 中对应项（例如 Title 或通过已知 Endpoint），并设置：
    - `card.FooterContent = new OpcCommMetric { Endpoint = e.Endpoint, Status = e.IsConnected, Latency = `${e.Latency}ms` };`
  - 或者改进：将 `DashboardCardModel` 增加强类型属性 `public OpcCommMetric OpcMetric { get; set; }` 并绑定 UI。当前项目 `FooterContent` 为 object，建议将其转换为强类型或在 XAML 中做 DataTemplate 处理。

- 在 `DeviceViewModel` / `SettingViewModel` 中：
  - `DeviceViewModel`：用于显示设备级别订阅状态与趋势图，调用 `IOpcUaService.CreateSubscriptionAsync(...)` 并显示返回的 `SubscriptionHandle`
  - `SettingViewModel`：提供证书管理/连接测试接口：例如 `TestConnectionAsync(endpoint)` 调用 `IOpcUaService.ConnectAsync(endpoint)` 并返回结果

---

## 6. 配置文件方案 `opcsettings.json` 与默认值（示例结构，简短示范）

建议文件名：`RadioIndustrialApp/opcsettings.json`

建议字段（默认值括在右侧）：
- `Endpoints`: [{ "Name": "PLC_01", "Url": "opc.tcp://192.168.0.10:4840", "AuthMode": "Certificate" }]
- `Auth`: { "Mode": "Certificate", "UserName": "", "Password": "", "CertificatePath": "Certificates/appcert.pfx" }
- `Subscriptions`: { "DefaultPublishingInterval": 1000, "DefaultSamplingInterval": 1000, "QueueSize": 10, "DiscardOldest": true }
- `Nodes`: [ { "GroupId": "PLC_01", "NodeId": "ns=2;s=Machine/Speed", "DisplayName": "Speed" }, ... ]
- `Reconnection`: { "InitialDelayMs": 1000, "MaxDelayMs": 30000, "MaxRetries": 0 /*0 = 无限重试*/ }
- `Logging`: { "Level": "Information", "LogFile": "logs/opcua.log" }

（实现时用 `IConfiguration` + `Options` 绑定到 `OpcSettings`）

---

## 7. 单元与集成测试建议与具体用例（2-3 个示例）

测试策略：
- 单元测试：Mock `Session`/`Subscription` 行为，验证 `OpcUaService` 在收到通知时正确转换并触发 `DataChanged` 事件。使用 `Moq` 或手工接口替身。
- 集成测试：使用本地可运行的 OPC UA 模拟服务器（例如 UA-.NET Standard Sample Server 或公有模拟器），在 CI 或本地开发机上启动模拟器端口并进行真实连接测试。也可以使用 Docker 容器化的模拟器（若可用）。

具体测试用例：

1) TestConnectAndSubscribe_Succeeds
- 目的：验证 `OpcUaService` 能使用配置连接 OPC UA 模拟服务器并创建订阅
- 步骤：
  - 启动本地 OPC UA 模拟器（或使用 TestFixture 提供的内置模拟）
  - 调用 `ConnectAsync(endpoint)`
  - 调用 `CreateSubscriptionAsync` 并 `AddMonitoredItemAsync` 对一个节点
  - 验证 Subscription 状态为 Active
- 断言：无异常抛出，返回的 SubscriptionHandle 不为空，Subscription.Active == true
- 类型：集成
- 估计：0.5-1 小时（写测试）+ 模拟器启动时间

2) TestDataChange_EventRaised
- 目的：验证当模拟器改变节点值时，`OpcUaService` 触发 `DataChanged` 并包含正确的 NodeId/Value
- 步骤：
  - 建立订阅并对测试节点添加 MonitoredItem
  - 在模拟器侧写入一个新值
  - 等待 `DataChanged` 事件并捕获事件参数
- 断言：事件被触发，事件参数中的 NodeId 与值匹配
- 类型：集成
- 估计：1-2 小时

3) TestReconnect_OnSessionLoss
- 目的：验证断线后服务能自动重连（重连策略生效）
- 步骤：
  - 建立订阅并确保接收正常
  - 模拟网络断开或直接关闭模拟器
  - 观察 `OpcUaService` 的重连尝试（可通过可见日志或内部状态回调）
  - 重启模拟器
  - 验证服务能恢复到已订阅并继续接收 DataChange
- 断言：服务在配置的退避范围内尝试重连并成功恢复订阅
- 类型：集成/系统
- 估计：2-4 小时（包括测试环境控制）

测试注意：
- 用 `CancellationToken` 控制长时间运行测试的超时
- 把与真实模拟器相关的测试标注为 Integration，允许在 CI 中选择性启用

---

## 8. 安全注意事项与运营建议

- 证书管理：
  - 生产环境优先使用证书认证；私钥文件权限设置为最小权限（仅运行服务的用户可读）
  - 提供证书轮换流程与工具（提示：使用 .pfx 与密码保护）
- 端口与网络：
  - OPC UA 默认端口 4840（opc.tcp），在防火墙上只开放必要的端口并限制来源地址
- 日志策略：
  - 记录重要事件：连接成功/失败、证书异常、订阅丢失/恢复、MonitoredItem 异常
  - 日志分级：Error/Critical -> 持久化；Info/Debug -> 根据调试需要开启
- 异常熔断/退避：
  - 使用指数退避策略（建议初始 1s -> 2s -> 4s ... 最大 30s）
  - 在连续 N 次失败后（例如 10 次）进入半人工模式（记录告警并降低尝试频率）
- 性能/资源：
  - 大规模节点时优先分组订阅以降低 Session/Subscription 数量
  - 使用异步队列（Channel）处理回调，避免在 SDK 回调中执行耗时操作
- 恶意/异常数据：
  - 对接收到的数据做基本校验（类型、范围）；对异常数据记录并可选触发告警
- 隐私与凭据：
  - 不在日志中输出明文密码或私钥片段

---

## 9. 提交物（Deliverables）与后续任务建议

必交物（MVP）：
1. `Services/IOpcUaService.cs`（接口） — 可供 DI 使用
2. `Services/OpcUaService.cs`（实现，含连接/订阅/重连/数据分发）
3. `Models/OpcSettings.cs` + `opcsettings.json`（配置与默认值）
4. `Services/CertificateManager.cs`（证书加载/生成）
5. `App.axaml.cs` 修改：注册 `IOpcUaService`
6. `ViewModels/IndexViewModel.cs` 修改：注入并消费数据
7. 单元/集成测试：至少包含上文提到的 2 个测试用例
8. README（简短）说明：如何配置、如何使用本地 OPC UA 模拟器进行测试、如何管理证书

后续/增强任务（建议）：
- 性能优化：批量订阅、并发处理策略、压力测试与调优
- 数据持久化：实现 `IOpcUaRepository` 将数据写入时序数据库（InfluxDB/Timescale）或消息总线（Kafka)
- UI：在 Dashboard 增加订阅管理界面、连接/重连历史、订阅延迟统计
- 监控与告警：导出 Prometheus 指标或集成现有监控系统
- 移植性：将 OPC 服务抽象成可独立运行的微服务/后台进程，UI 仅通过接口消费

---

这是一个草案计划，供审阅与调整；下一步我可以：
- 若你确认目标与优先级，生成更细化的实现步骤（每个必交物的实现 check-list 和伪代码）；
- 或者先给出 `opcsettings.json` 完整示例与 `IOpcUaService` 的完整接口定义草稿供你批准。

请审阅本计划并指出你希望调整的优先级、是否需要把测试用例改为仅 Mock（不启模拟器），或是否有目标 OPC UA 服务器（IP/安全策略）以便我把配置默认值对齐。
