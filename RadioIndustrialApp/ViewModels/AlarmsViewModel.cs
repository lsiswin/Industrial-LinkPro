using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RadioIndustrialApp.Models;
using RadioIndustrialApp.Services;

namespace RadioIndustrialApp.ViewModels;

public partial class AlarmsViewModel : ViewModelBase
{
    private readonly IHttpClientService _httpClientService;

    [ObservableProperty]
    private string title = "工业报警与事件中心";

    [ObservableProperty]
    private int _activeAlarmCount;

    [ObservableProperty]
    private int _criticalCount;

    [ObservableProperty]
    private bool _isBusy;

    public ObservableCollection<AlarmItemViewModel> ActiveAlarms { get; } = new();
    public ObservableCollection<AlarmItemViewModel> AlarmHistory { get; } = new();

    public AlarmsViewModel(IHttpClientService httpClientService)
    {
        _httpClientService = httpClientService;
        
        // 初始加载
        LoadDataCommand.Execute(null);
    }

    [RelayCommand]
    private async Task LoadData()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            // 1. 获取活动报警
            var actives = await _httpClientService.GetActiveAlarmsAsync();
            ActiveAlarms.Clear();
            foreach (var a in actives)
            {
                ActiveAlarms.Add(MapToViewModel(a));
            }

            // 2. 获取历史记录
            var history = await _httpClientService.GetAlarmHistoryAsync(50);
            AlarmHistory.Clear();
            foreach (var h in history)
            {
                AlarmHistory.Add(MapToViewModel(h));
            }

            UpdateStats();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading alarms: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private AlarmItemViewModel MapToViewModel(AlarmDto dto)
    {
        return new AlarmItemViewModel
        {
            Id = dto.Id,
            Time = dto.OccurredAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
            Source = dto.Source,
            Message = dto.Message,
            Severity = dto.Severity.ToString(),
            Status = dto.Status.ToString(),
            User = dto.AcknowledgedBy ?? "N/A"
        };
    }

    private void UpdateStats()
    {
        ActiveAlarmCount = ActiveAlarms.Count;
        CriticalCount = ActiveAlarms.Count(a => a.Severity == "Critical");
    }

    [RelayCommand]
    private async Task AcknowledgeAlarm(AlarmItemViewModel alarmVm)
    {
        if (IsBusy) return;
        
        try 
        {
            var success = await _httpClientService.AcknowledgeAlarmAsync(alarmVm.Id);
            if (success)
            {
                // 刷新数据
                await LoadData();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error acknowledging alarm: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task AcknowledgeAll()
    {
        if (IsBusy) return;

        foreach (var alarm in ActiveAlarms.ToList())
        {
            await AcknowledgeAlarm(alarm);
        }
    }
}

public partial class AlarmItemViewModel : ObservableObject
{
    public Guid Id { get; set; }
    public string Time { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty; 
    public string User { get; set; } = string.Empty;
    
    [ObservableProperty]
    private string status = string.Empty;

    public string SeverityColor => Severity switch
    {
        "Critical" => "#EF4444",
        "Error" => "#EF4444",
        "Warning" => "#F59E0B",
        "Info" => "#3B82F6",
        _ => "#94A3B8"
    };

    public string SeverityIcon => Severity switch
    {
        "Critical" => "❌",
        "Error" => "❌",
        "Warning" => "⚠",
        "Info" => "ℹ",
        _ => "●"
    };
}
