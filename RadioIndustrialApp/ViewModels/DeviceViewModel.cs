using System;
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
    private async Task LoadDataAsync()
    {
        if (IsLoading)
            return;
        IsLoading = true;

        try
        {
            Devices.Clear();
            var devicesDto = await _httpClientService.GetDevicesAsync();

            var allNodeIds = new System.Collections.Generic.List<string>();

            foreach (var dto in devicesDto)
            {
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
                        Value = "Loading...",
                    };
                    deviceVm.DataPoints.Add(dpVm);

                    if (!string.IsNullOrWhiteSpace(p.NodeId))
                    {
                        allNodeIds.Add(p.NodeId);
                    }
                }

                Devices.Add(deviceVm);
            }

            if (allNodeIds.Any())
            {
                if (_opcClientService.CurrentStatus == ConnectionStatus.Connected)
                {
                    await _opcClientService.ClearSubscriptionsAsync(); // 防止多次点击刷新导致节点句柄堆叠
                }
                else
                {
                    await _opcClientService.ConnectAsync();
                }
                
                await _opcClientService.SubscribeToNodesAsync(allNodeIds);
            }

            if (Devices.Any())
            {
                SelectedDevice = Devices.First();
            }
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
                    // You could also update a timestamp property here
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
