using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using RadioIndustrialApp.Models;
using RadioIndustrialApp.Services;

namespace RadioIndustrialApp.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private readonly IAuthService _authService;

    public Action? RequestClose;

    [ObservableProperty]
    private string _userName = "admin";

    [ObservableProperty]
    private string _password = "Admin@123456";

    [ObservableProperty]
    private string _errorMessage = "";

    [ObservableProperty]
    private bool _isLoading;

    public LoginViewModel(IAuthService authService)
    {
        _authService = authService;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsLoading)
            return;
        IsLoading = true;
        ErrorMessage = "";

        var success = await _authService.LoginAsync(UserName, Password);
        IsLoading = false;

        if (success)
        {
            // 发送切换窗口消息
            WeakReferenceMessenger.Default.Send(
                new SwitchToMainWindowMessage { Username = UserName }
            );
            RequestClose?.Invoke();
        }
        else
            ErrorMessage = "登录失败，请检查用户名或密码以及后台连接情况。";
    }

    [RelayCommand]
    private async Task LoginAsGuestAsync()
    {
        if (IsLoading)
            return;
        IsLoading = true;
        ErrorMessage = "";

        var success = await _authService.LoginAsGuestAsync();
        IsLoading = false;

        if (success)
            RequestClose?.Invoke();
        else
            ErrorMessage = "访客登录失败，请检查服务器连接。";
    }
}
