using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.Messaging;
using IndustrialLinkPro.OpcClient.Configuration;
using IndustrialLinkPro.OpcClient.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Navigation.Regions;
using RadioIndustrialApp.Models;
using RadioIndustrialApp.Services;
using RadioIndustrialApp.ViewModels;
using RadioIndustrialApp.Views;

namespace RadioIndustrialApp;

public partial class App : PrismApplication
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        base.Initialize();
    }

    protected override AvaloniaObject CreateShell()
    {
        Console.WriteLine("CreateShell()");
        var authService = Container.Resolve<IAuthService>();
        // 根据鉴权状态，返回对应的窗口作为初始 Shell
        if (authService.IsAuthenticated)
        {
            return Container.Resolve<MainWindow>();
        }
        else
        {
            return Container.Resolve<LoginWindow>();
        }
    }

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        // 加载 appsettings.json
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
        // 注册 IConfiguration 实例
        containerRegistry.RegisterInstance<IConfiguration>(config);

        // Register HttpClient
        containerRegistry.RegisterSingleton<HttpClient>();

        // Register HttpClientService
        containerRegistry.RegisterSingleton<IHttpClientService, HttpClientService>();

        // Register OPC Client Configuration
        var opcClientOptions =
            config.GetSection(OpcClientOptions.SectionName).Get<OpcClientOptions>()
            ?? new OpcClientOptions();
        containerRegistry.RegisterInstance(Options.Create(opcClientOptions));

        // Register ILogger
        containerRegistry.RegisterInstance<ILogger<OpcClientService>>(
            new NullLogger<OpcClientService>()
        );

        // Register OPC Client Services
        containerRegistry.RegisterSingleton<DataCacheService>();
        containerRegistry.RegisterSingleton<OpcClientService>();

        // Register Auth Service
        containerRegistry.RegisterSingleton<IAuthService, AuthService>();
        containerRegistry.Register<LoginViewModel>();

        containerRegistry.RegisterForNavigation<DeviceView, DeviceViewModel>();
        containerRegistry.RegisterForNavigation<AnalysisView, AnalysisViewModel>();
        containerRegistry.RegisterForNavigation<SettingView, SettingViewModel>();
        containerRegistry.RegisterForNavigation<AlarmsView, AlarmsViewModel>();
        containerRegistry.RegisterForNavigation<IndexView, IndexViewModel>();
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();

        // 启动 OPC Client 服务
        var opcClient = Container.Resolve<OpcClientService>();
        if (opcClient != null)
        {
            // 强制在后台线程启动，绝对不卡 UI 线程
            Task.Run(async () =>
            {
                try
                {
                    await opcClient.StartAsync(default);
                }
                catch (Exception ex)
                { /* 记录日志 */
                }
            });
        }

        var authService = Container.Resolve<IAuthService>();
        if (!authService.IsAuthenticated)
        {
            // 订阅切换到主窗口的消息
            WeakReferenceMessenger.Default.Register<SwitchToMainWindowMessage>(
                this,
                (recipient, message) =>
                {
                    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    {
                        // 记录当前的登录窗口
                        var loginWindow = desktop.MainWindow;

                        // 解析并设置主窗口
                        var mainWindow = Container.Resolve<MainWindow>();
                        // 必须做这一步，否则新窗口里的 Region 不会被注册到 Prism 内部字典中。
                        var regionManager = Container.Resolve<IRegionManager>();
                        RegionManager.SetRegionManager(mainWindow, regionManager);

                        // 2. 设置并显示新窗口
                        desktop.MainWindow = mainWindow;
                        mainWindow.Show();

                        // 【关键修复 2】：使用你代码中真实的 Region 名称 "MainRegion"
                        regionManager.RequestNavigate("MainRegion", "IndexView");
                        // 关闭登录窗口
                        loginWindow?.Close();

                        // 消息已处理，注销接收器
                        WeakReferenceMessenger.Default.Unregister<SwitchToMainWindowMessage>(this);
                    }
                }
            );
        }
    }
}
