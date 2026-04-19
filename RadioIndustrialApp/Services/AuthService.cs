using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using RadioIndustrialApp.Models;

namespace RadioIndustrialApp.Services;

public enum AppRole
{
    Guest = 0,      // Viewer (read-only)
    Operator = 1,   // Technician (configure)
    Admin = 2       // Administrator (all access)
}

public interface IAuthService
{
    bool IsAuthenticated { get; }
    AppRole CurrentRole { get; }
    string CurrentUser { get; }

    event EventHandler? AuthStateChanged;

    Task<bool> LoginAsync(string username, string password);
    Task<bool> LoginAsGuestAsync();
    void Logout();
    
    /// <summary>
    /// Checks if the user has the required role. 
    /// If not, it can prompt the user to login to a higher level.
    /// </summary>
    Task<bool> EnsureAccessAsync(AppRole requiredRole);
}

public class AuthService : IAuthService
{
    private readonly IHttpClientService _httpClientService;

    public bool IsAuthenticated { get; private set; }
    public AppRole CurrentRole { get; private set; } = AppRole.Guest;
    public string CurrentUser { get; private set; } = string.Empty;

    public event EventHandler? AuthStateChanged;

    public AuthService(IHttpClientService httpClientService)
    {
        _httpClientService = httpClientService;
    }

    public async Task<bool> LoginAsync(string username, string password)
    {
        var response = await _httpClientService.LoginAsync(new LoginRequest
        {
            UserName = username,
            Password = password
        });

        if (response != null && !string.IsNullOrEmpty(response.AccessToken))
        {
            IsAuthenticated = true;
            CurrentUser = username;
            
            // Map known testing users/roles
            if (username.Equals("admin", StringComparison.OrdinalIgnoreCase))
                CurrentRole = AppRole.Admin;
            else if (username.Equals("operator", StringComparison.OrdinalIgnoreCase))
                CurrentRole = AppRole.Operator;
            else 
                CurrentRole = AppRole.Guest;
                
            // In a real scenario, you could verify by parsing the JWT or calling:
            // var perms = await _httpClientService.GetMyPermissionsAsync();
            // Assign roles from perms

            AuthStateChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        return false;
    }

    public async Task<bool> LoginAsGuestAsync()
    {
        return await LoginAsync("viewer", "Viewer@123456");
    }

    public void Logout()
    {
        _httpClientService.Logout();
        IsAuthenticated = false;
        CurrentRole = AppRole.Guest;
        CurrentUser = string.Empty;
        AuthStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task<bool> EnsureAccessAsync(AppRole requiredRole)
    {
        if (IsAuthenticated && CurrentRole >= requiredRole)
            return true;

        // If we don't have access, spawn the login window using Avalonia application lifetime
        var completionSource = new TaskCompletionSource<bool>();
        
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // We need to resolve the window via Reflection/Locator or directly instantiate.
            // Since we know the types, we'll instantiate it directly for simplicity.
            var loginWindowType = Type.GetType("RadioIndustrialApp.Views.LoginWindow, RadioIndustrialApp");
            if (loginWindowType != null && Activator.CreateInstance(loginWindowType) is Avalonia.Controls.Window loginWindow)
            {
                // We'll attach an event to window close to verify if role is met after interacting
                loginWindow.Closed += (s, e) =>
                {
                    completionSource.TrySetResult(CurrentRole >= requiredRole);
                };

                // Show dialog over the current main window
                await loginWindow.ShowDialog(desktop.MainWindow);
                return await completionSource.Task;
            }
        }
        
        return false;
    }
}
