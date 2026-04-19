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
using RadioIndustrialApp.Services;
using RadioIndustrialApp.Models;

namespace RadioIndustrialApp.ViewModels;

public partial class AnalysisViewModel : ViewModelBase
{
    private readonly IHttpClientService _httpClientService;

    [ObservableProperty]
    private string title = "工业生产数据深度分析";

    [ObservableProperty]
    private ISeries[] _series = Array.Empty<ISeries>();

    [ObservableProperty]
    private Axis[] _xAxes = Array.Empty<Axis>();

    [ObservableProperty]
    private Axis[] _yAxes = Array.Empty<Axis>();

    [ObservableProperty]
    private string _selectedPeriod = "Last 24 Hours";

    [ObservableProperty]
    private bool _isBusy;

    public ObservableCollection<string> Periods { get; } = new() { "Last 1 Hour", "Last 6 Hours", "Last 24 Hours", "Last 7 Days" };

    public ObservableCollection<AnalysisStatViewModel> Statistics { get; } = new();

    public AnalysisViewModel(IHttpClientService httpClientService)
    {
        _httpClientService = httpClientService;
        InitializeChart();
        LoadStatistics();
    }

    partial void OnSelectedPeriodChanged(string value)
    {
        RefreshAnalysisCommand.Execute(null);
    }

    private void InitializeChart()
    {
        Series = new ISeries[]
        {
            new LineSeries<double> { Values = new double[] { 0, 0, 0, 0 }, Name = "等待数据加载..." }
        };

        XAxes = new Axis[] { new Axis { LabelsPaint = new SolidColorPaint(SKColors.Gray) } };
        YAxes = new Axis[] { new Axis { LabelsPaint = new SolidColorPaint(SKColors.Gray) } };
    }

    private void LoadStatistics()
    {
        Statistics.Clear();
        Statistics.Add(new AnalysisStatViewModel { Label = "平均生产效率", Value = "64.5%", Trend = "+2.3%", IsPositive = true });
        Statistics.Add(new AnalysisStatViewModel { Label = "峰值能耗", Value = "50.2 kW/h", Trend = "+0.5%", IsPositive = false });
        Statistics.Add(new AnalysisStatViewModel { Label = "设备稼动率 (OEE)", Value = "88.4%", Trend = "+1.2%", IsPositive = true });
        Statistics.Add(new AnalysisStatViewModel { Label = "异常停机次数", Value = "2 次", Trend = "-50%", IsPositive = true });
    }

    [RelayCommand]
    private async Task RefreshAnalysis()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            DateTimeOffset startTime;
            switch (SelectedPeriod)
            {
                case "Last 1 Hour": startTime = DateTimeOffset.UtcNow.AddHours(-1); break;
                case "Last 6 Hours": startTime = DateTimeOffset.UtcNow.AddHours(-6); break;
                case "Last 24 Hours": startTime = DateTimeOffset.UtcNow.AddHours(-24); break;
                case "Last 7 Days": startTime = DateTimeOffset.UtcNow.AddDays(-7); break;
                default: startTime = DateTimeOffset.UtcNow.AddHours(-24); break;
            }

            var queryParams = $"?startTime={startTime:O}&pageSize=500";
            // 显式调用之前已在接口中定义的 QueryPlcDataAsync
            var result = await _httpClientService.QueryPlcDataAsync(queryParams);

            if (result != null && result.Records != null && result.Records.Any())
            {
                var sortedRecords = result.Records.OrderBy(r => r.Timestamp).ToList();
                
                // 修复 Lambda 类型推导错误 (CS1662)
                var values = sortedRecords.Select<PlcDataRecordDto, double>(r => {
                    if (double.TryParse(r.Value, out var val)) return val;
                    return 0.0;
                }).ToArray();

                var timeLabels = sortedRecords.Select(r => r.Timestamp.ToLocalTime().ToString("HH:mm")).ToArray();

                Series = new ISeries[]
                {
                    new LineSeries<double>
                    {
                        Values = values,
                        Name = "历史采集趋势",
                        Stroke = new SolidColorPaint(SKColors.DodgerBlue) { StrokeThickness = 3 },
                        Fill = new LinearGradientPaint(new SKColor(30, 144, 255, 50), new SKColor(30, 144, 255, 0), SKPoint.Empty, new SKPoint(0, 1)),
                        GeometrySize = 6,
                    }
                };

                XAxes = new Axis[]
                {
                    new Axis
                    {
                        Labels = timeLabels,
                        LabelsPaint = new SolidColorPaint(SKColors.Gray),
                        LabelsRotation = 45,
                        SeparatorsPaint = new SolidColorPaint(SKColors.DarkSlateGray) { StrokeThickness = 1 }
                    }
                };
            }
            else
            {
                Series = new ISeries[]
                {
                    new LineSeries<double> { Values = new double[] { 0 }, Name = "后端未查询到数据" }
                };
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error refreshing analysis: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExportReport(string format)
    {
        IsBusy = true;
        await Task.Delay(1000); // 模拟导出
        IsBusy = false;
    }
}

public class AnalysisStatViewModel
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Trend { get; set; } = string.Empty;
    public bool IsPositive { get; set; }

    public string TrendColor => IsPositive ? "#10B981" : "#EF4444";
}
