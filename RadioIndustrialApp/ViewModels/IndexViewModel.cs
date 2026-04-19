using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using Prism.Events;
using Microsoft.Extensions.Configuration;
using RadioIndustrialApp.Services;

namespace RadioIndustrialApp.ViewModels;

public partial class IndexViewModel : ViewModelBase
{
    private readonly IConfiguration _configuration;
    private readonly IEventAggregator _aggregator;
    private readonly IHttpClientService _httpClientService;
    private readonly IAuthService _authService;

    // ========== 数据集合 ==========
    public ObservableCollection<DeviceItem> Devices { get; set; } = new();
    public ObservableCollection<SystemLogItem> SystemLogs { get; set; } = new();

    // ========== 图表属性 ==========
    [ObservableProperty]
    private ISeries[] chartSeries;

    [ObservableProperty]
    private Axis[] xAxes;

    [ObservableProperty]
    private Axis[] yAxes;

    // ========== 顶部统计指标 ==========
    [ObservableProperty] private int _totalDevices = 0;
    [ObservableProperty] private int _onlineDevices = 0;
    [ObservableProperty] private int _errorDevices = 0;
    
    [ObservableProperty] private int _opcNodeCount = 0;
    [ObservableProperty] private int _connectedClients = 0;
    [ObservableProperty] private string _opcServiceStatus = "Unknown";
    
    [ObservableProperty] private long _persistedDataCount = 0;
    [ObservableProperty] private int _dataThroughput = 0; // 条/秒

    [ObservableProperty] private string _systemStatus = "系统运行正常";
    [ObservableProperty] private string _lastUpdateTime;

    [ObservableProperty] private bool _isLoading;

    public IndexViewModel(IConfiguration configuration, IEventAggregator aggregator, IHttpClientService httpClientService, IAuthService authService)
    {
        _configuration = configuration;
        _aggregator = aggregator;
        _httpClientService = httpClientService;
        _authService = authService;
        
        LastUpdateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        
        InitializeChart();
    }

    [RelayCommand]
    private async Task ConnectAllDevicesAsync()
    {
        if (!await _authService.EnsureAccessAsync(AppRole.Admin))
        {
            AddLog("Error", "身份验证失败或权限不足，无法执行操作。");
            return;
        }

        SystemStatus = "正在初始化底层通讯链路...";
        AddLog("Info", "开始批量连接工业现场设备...");
        await Task.Delay(1000); // Wait for connection simulation or API call
        foreach (var device in Devices.Where(d => d.Status != "RUN"))
        {
            device.Status = "RUN";
        }
        UpdateLocalStatistics();
        SystemStatus = "所有设备已连接";
        AddLog("Success", "底层通讯链路建立完成。");
    }

    [RelayCommand]
    private async Task RefreshDevicesAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        SystemStatus = "正在拉取最新数据...";

        try
        {
            // 1. 获取统计信息
            var stats = await _httpClientService.GetDashboardStatsAsync();
            TotalDevices = stats.TotalDevices;
            OnlineDevices = stats.OnlineDevices;
            ErrorDevices = stats.ErrorDevices;
            OpcNodeCount = stats.OpcNodeCount;
            ConnectedClients = stats.ConnectedClients;
            PersistedDataCount = stats.PersistedDataCount;
            DataThroughput = stats.DataThroughput;

            // 2. 获取设备列表
            var deviceDtos = await _httpClientService.GetDevicesAsync();
            Devices.Clear();
            foreach (var dto in deviceDtos)
            {
                Devices.Add(new DeviceItem
                {
                    Name = dto.Name,
                    IpAddress = "192.168.x.x", // or get from API if available
                    Detail = dto.Description,
                    Protocol = dto.Protocol,
                    Status = dto.Status == "Active" ? "RUN" : dto.Status == "Error" ? "ERROR" : "OFFLINE"
                });
            }

            LastUpdateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            SystemStatus = "系统运行正常";
            AddLog("Success", "仪表盘数据已拉取并刷新。");
        }
        catch (Exception ex)
        {
            SystemStatus = "数据拉取失败";
            AddLog("Error", $"刷新仪表盘数据时发生异常: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void UpdateLocalStatistics()
    {
        // Example local recalculation if local states mutate.
        TotalDevices = Devices.Count;
        OnlineDevices = Devices.Count(d => d.Status == "RUN");
        ErrorDevices = Devices.Count(d => d.Status == "ERROR");
    }

    private void AddLog(string level, string message)
    {
        SystemLogs.Insert(0, new SystemLogItem 
        { 
            Time = DateTime.Now.ToString("HH:mm:ss"), 
            Level = level, 
            Message = message 
        });
        if (SystemLogs.Count > 50) SystemLogs.RemoveAt(SystemLogs.Count - 1);
    }

    private void InitializeChart()
    {
        ChartSeries = new ISeries[]
        {
            new LineSeries<double>
            { 
                Values = new double[] { 1200, 1250, 1180, 1300, 1250, 1400, 1350 }, 
                Name = "数据采集吞吐量 (次/秒)",
                Stroke = new SolidColorPaint(SKColors.Cyan) { StrokeThickness = 3 },
                Fill = new LinearGradientPaint(new SKColor(0, 255, 255, 60), new SKColor(0, 255, 255, 0), SKPoint.Empty, new SKPoint(0, 1)),
                GeometrySize = 8,
                GeometryStroke = new SolidColorPaint(SKColors.Cyan) { StrokeThickness = 2 }
            }
        };

        XAxes = new Axis[] { new Axis { Labels = new[] { "10:00", "10:05", "10:10", "10:15", "10:20", "10:25", "10:30" }, LabelsPaint = new SolidColorPaint(SKColors.LightGray) } };
        YAxes = new Axis[] { new Axis { LabelsPaint = new SolidColorPaint(SKColors.LightGray) } };
    }
}

public partial class DeviceItem : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusColor))]
    [NotifyPropertyChangedFor(nameof(StatusIcon))]
    private string status = "OFFLINE";

    public string Name { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string Protocol { get; set; } = string.Empty;

    public string StatusColor => Status switch
    {
        "RUN" => "#10B981",    // Emerald
        "ERROR" => "#EF4444",  // Red
        "OFFLINE" => "#64748B",// Slate
        _ => "#64748B"
    };

    public string StatusIcon => Status switch
    {
        "RUN" => "⚡",
        "ERROR" => "⚠",
        "OFFLINE" => "⏸",
        _ => "❓"
    };
}

public class SystemLogItem
{
    public string Time { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    public string LevelColor => Level switch
    {
        "Info" => "#3B82F6",
        "Success" => "#10B981",
        "Warning" => "#F59E0B",
        "Error" => "#EF4444",
        _ => "#94A3B8"
    };
}