using CommunityToolkit.Mvvm.ComponentModel;

namespace RadioIndustrialApp.ViewModels;

public partial class DeviceViewModel:ViewModelBase
{
    [ObservableProperty]
    private string title = "设备与标签";
    
    public DeviceViewModel()
    {
        
    }
    
}