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

namespace RadioIndustrialApp.ViewModels;

public partial class IndexViewModel : ViewModelBase
{
    private readonly IConfiguration _configuration;
    private readonly IEventAggregator _aggregator;

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
    
    [ObservableProperty] private int _opcNodeCount = 2458;
    [ObservableProperty] private int _connectedClients = 4;
    [ObservableProperty] private string _opcServiceStatus = "Active";
    
    [ObservableProperty] private long _persistedDataCount = 3845012;
    [ObservableProperty] private int _dataThroughput = 1250; // 条/秒

    [ObservableProperty] private string _systemStatus = "系统运行正常";
    [ObservableProperty] private string _lastUpdateTime;

    public IndexViewModel(IConfiguration configuration, IEventAggregator aggregator)
    {
        _configuration = configuration;
        _aggregator = aggregator;
        
        LastUpdateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        
        InitializeChart();
        LoadMockData();
    }

    [RelayCommand]
    private async Task ConnectAllDevicesAsync()
    {
        SystemStatus = "正在初始化底层通讯链路...";
        AddLog("Info", "开始批量连接工业现场设备...");
        await Task.Delay(1000); // 模拟耗时
        foreach (var device in Devices.Where(d => d.Status != "RUN"))
        {
            device.Status = "RUN";
        }
        UpdateStatistics();
        SystemStatus = "所有设备已连接";
        AddLog("Success", "底层通讯链路建立完成。");
    }

    [RelayCommand]
    private void RefreshDevices()
    {
        LoadMockData();
        LastUpdateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        AddLog("Info", "仪表盘数据已手动刷新。");
    }

    private void LoadMockData()
    {
        Devices.Clear();
        var mockDevices = new[]
        {
            new DeviceItem { Name = "Siemens S7-1500", IpAddress = "192.168.0.10", Detail = "主控 PLC / 产线A", Protocol = "S7 / OPC UA", Status = "RUN" },
            new DeviceItem { Name = "ABB IRB 1200", IpAddress = "192.168.0.21", Detail = "上下料机器人", Protocol = "RobotStudio/OPC", Status = "RUN" },
            new DeviceItem { Name = "HIKROBOT Camera", IpAddress = "192.168.0.35", Detail = "视觉缺陷检测", Protocol = "GigE / Halcon", Status = "RUN" },
            new DeviceItem { Name = "冷链环境温湿度仪", IpAddress = "192.168.0.50", Detail = "环境监测节点", Protocol = "Modbus TCP", Status = "ERROR" },
            new DeviceItem { Name = "OPC UA 边缘网关", IpAddress = "127.0.0.1", Detail = "本地数据汇聚中枢", Protocol = "OPC UA", Status = "RUN" }
        };

        foreach (var device in mockDevices) Devices.Add(device);
        UpdateStatistics();

        SystemLogs.Clear();
        AddLog("Info", "ColdDream 工业大屏已启动。");
        AddLog("Info", "OPC UA Server (Port: 4840) 监听中...");
        AddLog("Warning", "检测到冷链环境温湿度仪 (192.168.0.50) 通讯超时。");
    }

    private void UpdateStatistics()
    {
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