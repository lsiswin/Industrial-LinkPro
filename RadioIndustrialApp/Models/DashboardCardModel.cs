using CommunityToolkit.Mvvm.ComponentModel;
using RadioIndustrialApp.ViewModels;

namespace RadioIndustrialApp.Models;

public partial class DashboardCardModel: ViewModelBase
{
    public string Title { get; set; }
    public string Value { get; set; }
    public string IconData { get; set; }
    public string AccentColor { get; set; }
        
    [ObservableProperty]
    private object footerContent;
    
}
// --- 业务模型定义 ---

// 1. OPC 通讯指标
public class OpcCommMetric : ViewModelBase
{
    public string Endpoint { get; set; }    // opc.tcp://192.168.0.10:4840
    
    public bool Status { get; set; }      // Connected / Disconnected
    public string Latency { get; set; }     // 8ms
}

// 2. 数据引擎负载
public class DataEngineLoad : ViewModelBase
{
    public int TotalNodes { get; set; }     // 总采集点位
    public double SamplingRate { get; set; } // 采样频率 (Hz)
    public double CpuUsage { get; set; }    // 进程占用
}

// 3. 实时告警
public partial class ActiveAlarm : ViewModelBase
{
    public string Level { get; set; }       // CRITICAL / WARNING
    public string Message { get; set; }     // 例如：S7-1500 DB1.DBX0.0 通讯超时
    public string Time { get; set; }        // 14:20:05
}

// 4. 生产/视觉指标
public class ProductionMetric : ViewModelBase
{
    public double YieldRate { get; set; }   // 良率 99.2%
    public int CycleTime { get; set; }      // 循环周期 ms
    public int Count { get; set; }          // 计数
}