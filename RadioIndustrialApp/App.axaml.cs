using System;
using System.Linq;
using Avalonia;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Configuration;
using Prism.DryIoc;
using Prism.Ioc;
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

        return Container.Resolve<MainWindow>();
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

        containerRegistry.RegisterForNavigation<DeviceView, DeviceViewModel>();
        containerRegistry.RegisterForNavigation<AnalysisView, AnalysisViewModel>();
        containerRegistry.RegisterForNavigation<SettingView, SettingViewModel>();
        containerRegistry.RegisterForNavigation<AlarmsView, AlarmsViewModel>();
        containerRegistry.RegisterForNavigation<IndexView, IndexViewModel>();

    }
}
