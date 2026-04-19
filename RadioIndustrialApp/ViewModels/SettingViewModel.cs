using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RadioIndustrialApp.ViewModels;

public partial class SettingViewModel : ViewModelBase
{
    [ObservableProperty]
    private string title = "系统参数配置";

    [ObservableProperty]
    private SettingCategoryViewModel? _selectedCategory;

    public ObservableCollection<SettingCategoryViewModel> Categories { get; } = new();

    // --- 通讯设置属性 ---
    [ObservableProperty] private string _opcServerUrl = "opc.tcp://127.0.0.1:4840";
    [ObservableProperty] private string _apiBaseUrl = "http://localhost:5000";
    [ObservableProperty] private int _refreshInterval = 1000;

    // --- 用户设置属性 ---
    [ObservableProperty] private string _currentUser = "Admin";
    [ObservableProperty] private string _userRole = "Administrator";
    [ObservableProperty] private bool _enableTwoFactor = false;

    // --- 系统维护设置 ---
    [ObservableProperty] private bool _enableAutoReconnect = true;
    [ObservableProperty] private bool _enableLogging = true;
    [ObservableProperty] private int _logRetentionDays = 30;
    [ObservableProperty] private string _systemVersion = "v1.2.4-stable";

    public SettingViewModel()
    {
        Categories.Add(new SettingCategoryViewModel { Name = "网络与通讯", Icon = "🌐", Id = "Network" });
        Categories.Add(new SettingCategoryViewModel { Name = "账户管理", Icon = "👤", Id = "User" });
        Categories.Add(new SettingCategoryViewModel { Name = "数据存储", Icon = "💾", Id = "Storage" });
        Categories.Add(new SettingCategoryViewModel { Name = "关于系统", Icon = "ℹ️", Id = "About" });

        SelectedCategory = Categories.First();
    }

    [RelayCommand]
    private async Task SaveSettings()
    {
        await Task.Delay(500);
        // 实际逻辑：保存到 appsettings.json 或数据库
    }

    [RelayCommand]
    private async Task TestOpcConnection()
    {
        await Task.Delay(800);
    }
}

public class SettingCategoryViewModel : ObservableObject
{
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
}
