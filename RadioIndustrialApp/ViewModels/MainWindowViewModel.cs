using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Prism.Navigation.Regions;

namespace RadioIndustrialApp.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IRegionManager _regionManager;
    private const string MainRegionName = "MainRegion";

    public MainWindowViewModel(IRegionManager regionManager)
    {
        _regionManager = regionManager;
    }

    #region navigate
    public ObservableCollection<NavItem> NavItems { get; } = new()
    {
        new NavItem { Title = "首页仪表盘", Icon = "📊", Target = "IndexView" },
        new NavItem { Title = "设备与标签", Icon = "📦", Target = "DeviceView" },
        new NavItem { Title = "数据分析", Icon = "📈", Target = "AnalysisView" },
        new NavItem { Title = "报警记录", Icon = "⚠️", Target = "AlarmsView" },
        new NavItem { Title = "系统设置", Icon = "⚙️", Target = "SettingView" }
    };
    
    [RelayCommand]
    public void Navigate(string? viewName)
    {
        if (string.IsNullOrWhiteSpace(viewName)) return;

        _regionManager.RequestNavigate(MainRegionName, viewName, navigationResult =>
        {
            if (navigationResult.Success == false)
            {
                System.Diagnostics.Debug.WriteLine($"导航至 {viewName} 失败: {navigationResult.Exception?.Message}");
            }
        });
    }
    
    #endregion
}
public class NavItem
{
    public string Title { get; set; }
    public string Icon { get; set; }
    public string Target { get; set; }
}