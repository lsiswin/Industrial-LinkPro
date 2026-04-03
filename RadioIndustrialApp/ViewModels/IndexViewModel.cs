using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Prism.Commands;
using RadioIndustrialApp.Models;
using SkiaSharp;

namespace RadioIndustrialApp.ViewModels;

public partial class IndexViewModel:ViewModelBase
{
    // 图表数据
    public ISeries[] Series { get; set; }
    public Axis[] XAxes { get; set; }
    public Axis[] YAxes { get; set; }

    // 设备列表
    public ObservableCollection<DeviceItem> Devices { get; set; }
        public ObservableCollection<DashboardCardModel> DashboardCards { get; set; }
        

        public IndexViewModel()
        {
            DashboardCards = new ObservableCollection<DashboardCardModel>();
            Load();
        }

        
        private void Load()
        {
            DashboardCards.Clear();

            // 1. 西门子 S7-1500 连接状态
            DashboardCards.Add(new DashboardCardModel {
                Title = "OPC UA 控制器链路",
                Value = "ONLINE",
                AccentColor = "#4CAF50",
                IconData = "M2,2H22V22H2M4,4V20H20V4H4M6,6H18V10H6V6M6,12H18V18H6V12Z", // 示例 PathData
                FooterContent = new OpcCommMetric { Endpoint = "192.168.0.10", Status = true, Latency = "5ms" }
            });

            // 2. 数据处理引擎
            DashboardCards.Add(new DashboardCardModel {
                Title = "数据采集引擎",
                Value = "2,450 Nodes",
                AccentColor = "#2196F3",
                IconData = "M12,6V18L18,12L12,6M7,6V18L13,12L7,6Z",
                FooterContent = new DataEngineLoad { TotalNodes = 2450, SamplingRate = 10, CpuUsage = 15.4 }
            });

            // 3. 实时告警
            DashboardCards.Add(new DashboardCardModel {
                Title = "系统活动告警",
                Value = "02 Active",
                AccentColor = "#F44336",
                IconData = "M13,14H11V9H13M13,18H11V16H13M1,21H23L12,2L1,21Z",
                FooterContent = new ActiveAlarm { Level = "CRITICAL", Message = "西门子 PLC 握手超时", Time = "12:05:33" }
            });

            // 4. 生产良率 (结合你的视觉背景)
            DashboardCards.Add(new DashboardCardModel {
                Title = "生产工艺指标",
                Value = "98.5 % OK",
                AccentColor = "#00BCD4",
                IconData = "M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M12,4A8,8 0 0,1 20,12A8,8 0 0,1 12,20A8,8 0 0,1 4,12A8,8 0 0,1 12,4M11,7V13H13V7H11M11,15V17H13V15H11Z",
                FooterContent = new ProductionMetric { YieldRate = 98.5, CycleTime = 1200, Count = 4500 }
            });
            // 1. 初始化图表 (简化版配置)
            Series = new ISeries[] {
                new LineSeries<double> { 
                    Values = new double[] { 25, 30, 42, 38, 45, 40, 52, 55 }, 
                    Stroke = new SolidColorPaint(SKColors.CornflowerBlue) { StrokeThickness = 3 },
                    GeometrySize = 0, Fill = null, Name = "CPU负载" 
                },
                new LineSeries<double> { 
                    Values = new double[] { 60, 62, 63, 64, 63, 66, 65, 65 }, 
                    Stroke = new SolidColorPaint(SKColors.Cyan) { StrokeThickness = 3 },
                    GeometrySize = 0, Fill = null, Name = "内存占用" 
                }
            };

            // 2. 初始化设备快照
            Devices = new ObservableCollection<DeviceItem> {
                new() { Name = "Siemens S7-1500_01", IpAddress = "192.168.1.100", Detail = "Rack 0, Slot 1", Status = "RUN" },
                new() { Name = "Siemens S7-1200_PACK", IpAddress = "192.168.1.112", Detail = "Rack 0, Slot 0", Status = "RUN" },
                new() { Name = "Modbus_TCP_Gateway", IpAddress = "192.168.1.50", Detail = "连接超时", Status = "ERROR" },
                new() { Name = "Assembly_Line_3", IpAddress = "192.168.1.120", Detail = "未配置", Status = "OFFLINE" }
            };
        }
    }
public class DeviceItem : ObservableObject
{
    public string Name { get; set; }
    public string IpAddress { get; set; }
    public string Detail { get; set; }
    public string Status { get; set; } // RUN, ERROR, OFFLINE
    public string StatusColor => Status switch {
        "RUN" => "#4CAF50",
        "ERROR" => "#F44336",
        _ => "#5C5E66"
    };
}