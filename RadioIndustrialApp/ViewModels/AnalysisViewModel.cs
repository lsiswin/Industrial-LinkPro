using CommunityToolkit.Mvvm.ComponentModel;
using Prism.Navigation.Regions;

namespace RadioIndustrialApp.ViewModels;

public partial class AnalysisViewModel:ViewModelBase
{
        [ObservableProperty]
        private string title = "数据分析";
        
        public AnalysisViewModel()
        {
            
        }
}