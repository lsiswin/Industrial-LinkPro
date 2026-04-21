using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
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

    public ObservableCollection<AlarmItem> ActiveAlarms { get; } = new();
    public ObservableCollection<AlarmItem> AlarmHistory { get; } = new();

    public AlarmsViewModel(IHttpClientService httpClientService)
    {
        _httpClientService = httpClientService;

        // 初始加载
        LoadDataCommand.Execute(null);
        WeakReferenceMessenger.Default.Register<NewAlarmsMessage>(
            this,
            (recipient, message) =>
            {
                // 注意：因为 Send 是在 Task.Run (后台线程) 触发的，
                // 操作 UI 绑定的 ObservableCollection 必须切回 UI 线程！
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var alarm = message.AlarmItem;

                    // 1. 将新报警插入到活动报警列表的最前面 (索引 0)
                    ActiveAlarms.Insert(0, alarm);

                    // 2. 如果历史列表中也需要展示，同步插入
                    AlarmHistory.Insert(0, alarm);

                    // 3. [关键优化] 限制集合最大容量，防止长期运行导致内存泄漏 (OOM)
                    if (AlarmHistory.Count > 50)
                    {
                        // 移除末尾最旧的数据
                        AlarmHistory.RemoveAt(AlarmHistory.Count - 1);
                    }

                    // 4. 更新统计信息（如报警总数）
                    UpdateStats();
                });
            }
        );
    }

    [RelayCommand]
    private async Task LoadData()
    {
        if (IsBusy)
            return;
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

    private AlarmItem MapToViewModel(AlarmDto dto)
    {
        return new AlarmItem
        {
            Id = dto.Id,
            Time = dto.OccurredAtUtc.DateTime,
            Source = dto.Source,
            Message = dto.Message,
            Severity = dto.Severity,
            Status = dto.Status.ToString(),
            User = dto.AcknowledgedBy ?? "N/A",
        };
    }

    private void UpdateStats()
    {
        ActiveAlarmCount = ActiveAlarms.Count;
        CriticalCount = ActiveAlarms.Count(a => a.Severity == AlarmSeverity.Critical);
    }

    [RelayCommand]
    private async Task AcknowledgeAlarm(AlarmItem alarmVm)
    {
        if (IsBusy)
            return;

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
        if (IsBusy)
            return;

        foreach (var alarm in ActiveAlarms.ToList())
        {
            await AcknowledgeAlarm(alarm);
        }
    }
}

public partial class AlarmItem : ObservableObject
{
    public Guid Id { get; set; }
    public DateTime Time { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public AlarmSeverity Severity { get; set; } = AlarmSeverity.Info;
    public string User { get; set; } = string.Empty;

    [ObservableProperty]
    private string status = string.Empty;

    public string SeverityColor =>
        Severity switch
        {
            AlarmSeverity.Critical => "#EF4444",
            AlarmSeverity.Error => "#EF4444",
            AlarmSeverity.Warning => "#F59E0B",
            AlarmSeverity.Info => "#3B82F6",
            _ => "#94A3B8",
        };

    public string SeverityIcon =>
        Severity switch
        {
            AlarmSeverity.Critical => "❌",
            AlarmSeverity.Error => "❌",
            AlarmSeverity.Warning => "⚠",
            AlarmSeverity.Info => "ℹ",
            _ => "●",
        };
}
