using CommunityToolkit.Mvvm.ComponentModel;

namespace RadioIndustrialApp.ViewModels;

public partial class SettingViewModel:ViewModelBase
{
    [ObservableProperty]
    private string title = "系统配置";
    
    public SettingViewModel()
    {
        
    }
}