using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IndustrialLinkPro.OpcClient.Events;
using IndustrialLinkPro.OpcClient.Models;
using IndustrialLinkPro.OpcClient.Services;
using RadioIndustrialApp.Services;

namespace RadioIndustrialApp.ViewModels;

public partial class DeviceViewModel : ViewModelBase, IDisposable
{
    private readonly IHttpClientService _httpClientService;
    private readonly OpcClientService _opcClientService;

    [ObservableProperty]
    private string title = "设备与标签监控";

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private DeviceItemViewModel? _selectedDevice;

    public ObservableCollection<DeviceItemViewModel> Devices { get; } = new();

    public DeviceViewModel(IHttpClientService httpClientService, OpcClientService opcClientService)
    {
        _httpClientService = httpClientService;
        _opcClientService = opcClientService;

        _opcClientService.DataChanged += OnOpcDataChanged;
    }

    [RelayCommand]
    private async Task LoadDataAsync(object? parameter)
    {
        if (IsLoading) return;

        // 解析参数：支持 bool 类型或字符串 "True"/"False"
        bool forceLoad = false;
        if (parameter is bool b)
        {
            forceLoad = b;
        }
        else if (parameter is string s && bool.TryParse(s, out var sb))
        {
            forceLoad = sb;
        }

        // 如果不是强制刷新，且已经有设备数据，则直接跳过加载逻辑
        if (!forceLoad && Devices.Count > 0)
        {
            return;
        }
        
        IsLoading = true;

        try
        {
            // 在后台线程执行繁重的数据拉取和处理
            await Task.Run(async () => {
                var devicesDto = await _httpClientService.GetDevicesAsync();
                var allNodeIds = new List<string>();
                var tempDeviceList = new List<DeviceItemViewModel>();

                // 使用并发请求获取所有设备的点位
                var tasks = devicesDto.Select(async dto => {
                    var deviceVm = new DeviceItemViewModel
                    {
                        Id = dto.Id,
                        Name = dto.Name,
                        Status = dto.Status,
                        Description = dto.Description,
                    };

                    var pointsDto = await _httpClientService.GetDataPointsAsync(dto.Id);
                    foreach (var p in pointsDto)
                    {
                        var dpVm = new DataPointItemViewModel
                        {
                            Id = p.Id,
                            Name = p.Name,
                            NodeId = p.NodeId,
                            Unit = p.Unit,
                            Value = "Waiting...",
                        };
                        deviceVm.DataPoints.Add(dpVm);

                        if (!string.IsNullOrWhiteSpace(p.NodeId))
                        {
                            lock (allNodeIds)
                            {
                                allNodeIds.Add(p.NodeId);
                            }
                        }
                    }
                    return deviceVm;
                });

                var results = await Task.WhenAll(tasks);

                // 回到 UI 线程更新集合
                await Dispatcher.UIThread.InvokeAsync(() => {
                    Devices.Clear();
                    foreach (var res in results) Devices.Add(res);
                    
                    if (Devices.Any())
                        SelectedDevice = Devices.First();
                });

                // OPC 连接和订阅操作同样放在后台
                if (allNodeIds.Any())
                {
                    if (_opcClientService.CurrentStatus == ConnectionStatus.Connected)
                    {
                        await _opcClientService.ClearSubscriptionsAsync();
                    }
                    else
                    {
                        await _opcClientService.ConnectAsync();
                    }
                    await _opcClientService.SubscribeToNodesAsync(allNodeIds);
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading devices: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OnOpcDataChanged(object? sender, DataChangedEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            foreach (var device in Devices)
            {
                var pt = device.DataPoints.FirstOrDefault(p => p.NodeId == e.NodeId);
                if (pt != null)
                {
                    pt.Value = e.DataPoint.Value?.ToString() ?? "N/A";
                    break;
                }
            }
        });
    }

    public void Dispose()
    {
        _opcClientService.DataChanged -= OnOpcDataChanged;
    }
}

public partial class DeviceItemViewModel : ObservableObject
{
    public Guid Id { get; set; }

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string status = string.Empty;

    [ObservableProperty]
    private string description = string.Empty;

    public ObservableCollection<DataPointItemViewModel> DataPoints { get; } = new();
}

public partial class DataPointItemViewModel : ObservableObject
{
    public Guid Id { get; set; }
    public string NodeId { get; set; } = string.Empty;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string unit = string.Empty;

    [ObservableProperty]
    private string value = "0";
}
