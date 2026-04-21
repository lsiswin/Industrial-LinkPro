using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IndustrialLinkPro.OpcClient.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.Extensions.Configuration;
using Prism.Events;
using RadioIndustrialApp.Services;
using SkiaSharp;

namespace RadioIndustrialApp.ViewModels;

public partial class IndexViewModel : ViewModelBase
{
    private readonly IHttpClientService _httpClientService;
    private readonly IAuthService _authService;
    private readonly OpcClientService _opcClientService;

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
    [ObservableProperty]
    private int _totalDevices = 0;

    [ObservableProperty]
    private int _onlineDevices = 0;

    [ObservableProperty]
    private int _errorDevices = 0;

    [ObservableProperty]
    private int _opcNodeCount = 0;

    [ObservableProperty]
    private int _connectedClients = 0;

    [ObservableProperty]
    private string _opcServiceStatus = "Unknown";

    [ObservableProperty]
    private long _persistedDataCount = 0;

    [ObservableProperty]
    private int _dataThroughput = 0; // 条/秒

    [ObservableProperty]
    private string _systemStatus = "系统运行正常";

    [ObservableProperty]
    private string _lastUpdateTime;

    [ObservableProperty]
    private bool _isLoading;

    private readonly System.Threading.PeriodicTimer _refreshTimer;
    private readonly CancellationTokenSource _cts = new();

    public IndexViewModel(
        IHttpClientService httpClientService,
        IAuthService authService,
        OpcClientService opcClientService
    )
    {
        _httpClientService = httpClientService;
        _authService = authService;
        _opcClientService = opcClientService;

        LastUpdateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        InitializeChart();

        // 设置定时刷新（10秒一次）
        _refreshTimer = new System.Threading.PeriodicTimer(TimeSpan.FromSeconds(10));
        _ = StartPeriodicRefreshAsync();
    }

    private async Task StartPeriodicRefreshAsync()
    {
        // 初始加载
        await RefreshDevicesAsync();

        try
        {
            while (await _refreshTimer.WaitForNextTickAsync(_cts.Token))
            {
                await RefreshDevicesAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // 正常停止
        }
    }

    [RelayCommand]
    private async Task ConnectAllDevicesAsync()
    {
        if (!await _authService.EnsureAccessAsync(AppRole.Admin))
        {
            AddLog("Error", "身份验证失败或权限不足，无法执行操作。");
            return;
        }

        SystemStatus = "正在尝试建立 OPC UA 通讯链路...";
        AddLog("Info", "发起 OpcClient 握手请求...");

        try
        {
            await _opcClientService.ConnectAsync();

            // 可选：获取全站所有的 NodeId 进行订阅，以达到“总览监听”的目的
            var devicesDto = await _httpClientService.GetDevicesAsync();
            var allNodeIds = new System.Collections.Generic.List<string>();
            foreach (var d in devicesDto)
            {
                var points = await _httpClientService.GetDataPointsAsync(d.Id);
                allNodeIds.AddRange(
                    points.Where(p => !string.IsNullOrWhiteSpace(p.NodeId)).Select(p => p.NodeId)
                );
            }
            if (allNodeIds.Any())
            {
                await _opcClientService.ClearSubscriptionsAsync();
                await _opcClientService.SubscribeToNodesAsync(allNodeIds);
            }

            // 更新状态
            SystemStatus = "OPC 引擎已连接";
            AddLog("Success", $"成功连接至底层服务器，已挂载 {allNodeIds.Count} 个监控节点。");

            // 联动刷新仪表盘卡片
            await RefreshDevicesAsync();
        }
        catch (Exception ex)
        {
            SystemStatus = "连接失败";
            AddLog("Error", $"通讯链路建立失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task RefreshDevicesAsync()
    {
        if (IsLoading)
            return;
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
            OpcServiceStatus = stats.IsOpcEngineRunning ? "Running" : "Stopped";

            // 2. 获取设备列表
            var deviceDtos = await _httpClientService.GetDevicesAsync();
            Devices.Clear();
            foreach (var dto in deviceDtos)
            {
                // 如果设备不包含详细协议或是临时注册，我们给个默认显示
                var ip = string.IsNullOrWhiteSpace(dto.DeviceType) ? "Unknown" : dto.DeviceType;
                var rawStatus = string.IsNullOrWhiteSpace(dto.Status)
                    ? "OFFLINE"
                    : dto.Status.ToUpper();
                string displayStatus =
                    rawStatus == "ACTIVE" || rawStatus == "ONLINE" ? "RUN" : rawStatus;

                Devices.Add(
                    new DeviceItem
                    {
                        Name = dto.Name,
                        IpAddress = ip, // 使用 DeviceType 代替 Mock IP
                        Detail = dto.Description,
                        Protocol = string.IsNullOrWhiteSpace(dto.Protocol)
                            ? "OPC UA"
                            : dto.Protocol,
                        Status = displayStatus,
                    }
                );
            }

            // 3. 收取系统最新的告警信息进入日志
            var activeAlarms = await _httpClientService.GetActiveAlarmsAsync(
                cancellationToken: default
            );
            SystemLogs.Clear();
            foreach (var alarm in activeAlarms.OrderByDescending(a => a.OccurredAtUtc).Take(20))
            {
                SystemLogs.Add(
                    new SystemLogItem
                    {
                        Time = alarm.OccurredAtUtc.ToString("HH:mm:ss"),
                        Level = alarm.Severity.ToString(),
                        Message = $"{alarm.Source}: {alarm.Message}",
                    }
                );
            }

            LastUpdateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            SystemStatus = "系统运行正常";
            if (SystemLogs.Count == 0)
                AddLog("Success", "仪表盘数据已拉取并刷新，目前无告警。");
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
        SystemLogs.Insert(
            0,
            new SystemLogItem
            {
                Time = DateTime.Now.ToString("HH:mm:ss"),
                Level = level,
                Message = message,
            }
        );
        if (SystemLogs.Count > 50)
            SystemLogs.RemoveAt(SystemLogs.Count - 1);
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
                Fill = new LinearGradientPaint(
                    new SKColor(0, 255, 255, 60),
                    new SKColor(0, 255, 255, 0),
                    SKPoint.Empty,
                    new SKPoint(0, 1)
                ),
                GeometrySize = 8,
                GeometryStroke = new SolidColorPaint(SKColors.Cyan) { StrokeThickness = 2 },
            },
        };

        XAxes = new Axis[]
        {
            new Axis
            {
                Labels = new[] { "10:00", "10:05", "10:10", "10:15", "10:20", "10:25", "10:30" },
                LabelsPaint = new SolidColorPaint(SKColors.LightGray),
            },
        };
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

    public string StatusColor =>
        Status switch
        {
            "RUN" => "#10B981", // Emerald
            "ERROR" => "#EF4444", // Red
            "OFFLINE" => "#64748B", // Slate
            _ => "#64748B",
        };

    public string StatusIcon =>
        Status switch
        {
            "RUN" => "⚡",
            "ERROR" => "⚠",
            "OFFLINE" => "⏸",
            _ => "❓",
        };
}

public class SystemLogItem
{
    public string Time { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    public string LevelColor =>
        Level switch
        {
            "Info" => "#3B82F6",
            "Success" => "#10B981",
            "Warning" => "#F59E0B",
            "Error" => "#EF4444",
            _ => "#94A3B8",
        };
}
