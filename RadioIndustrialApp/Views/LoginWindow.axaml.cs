using System;
using Avalonia.Controls;
using Prism.Ioc;
using RadioIndustrialApp.ViewModels;

namespace RadioIndustrialApp.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();

        var vm = ContainerLocator.Container.Resolve<LoginViewModel>();
        vm.RequestClose = () => Close();
        DataContext = vm;
    }
}
