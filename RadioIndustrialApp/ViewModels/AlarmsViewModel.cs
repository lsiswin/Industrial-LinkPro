using CommunityToolkit.Mvvm.ComponentModel;
using Prism.Navigation.Regions;

namespace RadioIndustrialApp.ViewModels;

public partial class AlarmsViewModel:ViewModelBase
{
    [ObservableProperty]
    private string title = "报警记录";

    public AlarmsViewModel()
    {
        
    }

}