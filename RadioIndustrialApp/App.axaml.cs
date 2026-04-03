using System;
using Avalonia;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
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
        containerRegistry.RegisterForNavigation<DeviceView,DeviceViewModel>();
        containerRegistry.RegisterForNavigation<AnalysisView,AnalysisViewModel>();
        containerRegistry.RegisterForNavigation<SettingView,SettingViewModel>();
        containerRegistry.RegisterForNavigation<AlarmsView,AlarmsViewModel>();
        containerRegistry.RegisterForNavigation<IndexView,IndexViewModel>();
    }


    public override void OnFrameworkInitializationCompleted()
    {
        base.OnFrameworkInitializationCompleted();
        DisableAvaloniaDataAnnotationValidation();

        
    }
    private void DisableAvaloniaDataAnnotationValidation()
    {
        
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
    
    
}