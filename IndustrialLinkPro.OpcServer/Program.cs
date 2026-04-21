using IndustrialLinkPro.OpcServer;
using IndustrialLinkPro.OpcServer.Clients;
using IndustrialLinkPro.OpcServer.Configuration;
using IndustrialLinkPro.OpcServer.Drivers;
using IndustrialLinkPro.OpcServer.OpcUa;
using IndustrialLinkPro.OpcServer.Runtime;
using IndustrialLinkPro.OpcServer.Services;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

// 配置作为 Windows 服务运行
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "IndustrialLinkPro OPC Server";
});

// 绑定各模块配置
builder.Services.Configure<DeviceApiOptions>(builder.Configuration.GetSection(DeviceApiOptions.SectionName));
builder.Services.Configure<OpcServerOptions>(builder.Configuration.GetSection(OpcServerOptions.SectionName));
builder.Services.Configure<DriverOptions>(builder.Configuration.GetSection(DriverOptions.SectionName));

// 配置设备 API HTTP 客户端
builder.Services.AddHttpClient<DeviceApiClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<DeviceApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});

// 注册核心单例服务
builder.Services.AddSingleton<RuntimeModel>();
builder.Services.AddSingleton<IDeviceDefinitionProvider, DeviceApiDefinitionProvider>();
builder.Services.AddSingleton<IDriverFactory, DriverFactory>();
// 将 RuntimeModel 作为 IRuntimeNodeRegistry 的实现注册
builder.Services.AddSingleton<IRuntimeNodeRegistry>(sp => sp.GetRequiredService<RuntimeModel>());
builder.Services.AddSingleton<IOpcUaServerHost, OpcUaServerHost>();

// 注册 OPC UA 主宿主服务（先于后台工作服务启动）
builder.Services.AddHostedService(sp => (OpcUaServerHost)sp.GetRequiredService<IOpcUaServerHost>());
// 注册拆分后的三项工作服务：定义同步采集、数据采集与状态上报
builder.Services.AddHostedService<DefinitionSyncWorker>();
builder.Services.AddHostedService<DataAcquisitionWorker>();
builder.Services.AddHostedService<StatusReportingWorker>();

var host = builder.Build();
host.Run();
