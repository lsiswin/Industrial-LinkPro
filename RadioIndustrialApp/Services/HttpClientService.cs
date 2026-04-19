using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using RadioIndustrialApp.Models;

namespace RadioIndustrialApp.Services;

/// <summary>
/// 全局 HTTP 客户端服务接口 - 包装后台 DeviceApi 所有控制器接口
/// </summary>
public interface IHttpClientService
{
    // === AuthController 认证相关 ===
    Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    void Logout();

    // === DashboardController 仪表盘相关 ===
    Task<DashboardStatsDto> GetDashboardStatsAsync(CancellationToken cancellationToken = default);

    // === AdminDevicesController / OpcController 设备与数据点相关 ===
    Task<List<DeviceDto>> GetDevicesAsync(CancellationToken cancellationToken = default);
    Task<List<DataPointDto>> GetDataPointsAsync(Guid deviceId, CancellationToken cancellationToken = default);
    Task<object?> GetDeviceByIdAsync(Guid deviceId, CancellationToken cancellationToken = default);
    Task<object?> CreateDeviceAsync(DeviceDto deviceDto, CancellationToken cancellationToken = default);
    Task<object?> UpdateDeviceAsync(Guid deviceId, DeviceDto deviceDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteDeviceAsync(Guid deviceId, CancellationToken cancellationToken = default);
    
    Task<object?> GetDataPointNodeIdAsync(Guid dataPointId, CancellationToken cancellationToken = default);
    Task<object?> BatchGetDataPointNodeIdsAsync(BatchGetNodeIdRequest request, CancellationToken cancellationToken = default);
    Task<object?> UpdateDataPointNodeIdAsync(Guid dataPointId, UpdateDataPointNodeIdRequest request, CancellationToken cancellationToken = default);
    Task<object?> BatchUpdateDataPointNodeIdsAsync(List<UpdateNodeIdBatchRequest> request, CancellationToken cancellationToken = default);

    // === OperationLogsController 操作日志相关 ===
    Task<List<OperationLogDto>> GetOperationLogsAsync(int take = 100, CancellationToken cancellationToken = default);

    // === PermissionsController 权限相关 ===
    Task<PermissionSummaryResponse?> GetMyPermissionsAsync(CancellationToken cancellationToken = default);

    // === PlcDataController PLC 数据与存储相关 ===
    Task<object?> BatchSavePlcDataAsync(BatchPlcDataRequest request, CancellationToken cancellationToken = default);
    Task<object?> SavePlcDataAsync(PlcDataItemRequest request, CancellationToken cancellationToken = default);
    Task<object?> GetPlcDataByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<object?> QueryPlcDataAsync(string queryParams, CancellationToken cancellationToken = default);
    Task<object?> GetLatestPlcDataAsync(Guid deviceId, int top = 10, CancellationToken cancellationToken = default);
    Task<object?> CleanupOldPlcDataAsync(int retainDays = 30, CancellationToken cancellationToken = default);
}

/// <summary>
/// 全局 HTTP 客户端服务实现 - 包含了鉴权与所有 DeviceApi 的调用逻辑
/// </summary>
public class HttpClientService : IHttpClientService
{
    private readonly HttpClient _httpClient;
    private string? _accessToken;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    public HttpClientService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        
        var baseUrl = configuration["DeviceApi:BaseUrl"] ?? "http://localhost:5288";
        _httpClient.BaseAddress = new Uri(baseUrl);
    }

    #region AuthController 认证相关 API
    
    /// <summary>
    /// 登录接口 (POST /api/auth/login)
    /// </summary>
    public async Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/auth/login", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var authResult = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions, cancellationToken);
                if (authResult?.AccessToken != null)
                {
                    _accessToken = authResult.AccessToken;
                    // 在此后所有请求中附加上 Auth Bearer Token 用于身份校验
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                }
                return authResult;
            }
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Login failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 登出：从客户端移除 JWT Token
    /// </summary>
    public void Logout()
    {
        _accessToken = null;
        _httpClient.DefaultRequestHeaders.Authorization = null;
    }

    #endregion

    #region DashboardController 仪表盘相关 API
    
    /// <summary>
    /// 获取仪表盘统计总览 (GET /api/admin/dashboard/stats)
    /// </summary>
    public async Task<DashboardStatsDto> GetDashboardStatsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<DashboardStatsDto>("/api/admin/dashboard/stats", JsonOptions, cancellationToken);
            return response ?? new DashboardStatsDto();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to get dashboard stats: {ex.Message}");
            return new DashboardStatsDto();
        }
    }

    #endregion

    #region AdminDevicesController / OpcController 设备与数据点相关 API

    /// <summary>
    /// 获取设备列表 (GET /api/admin/devices)
    /// </summary>
    public async Task<List<DeviceDto>> GetDevicesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<List<DeviceDto>>("/api/admin/devices", JsonOptions, cancellationToken);
            return response ?? new List<DeviceDto>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to get devices: {ex.Message}");
            return new List<DeviceDto>();
        }
    }

    /// <summary>
    /// 根据设备ID获取数据点列表 (GET /api/admin/devices/{deviceId}/datapoints)
    /// </summary>
    public async Task<List<DataPointDto>> GetDataPointsAsync(Guid deviceId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<List<DataPointDto>>($"/api/admin/devices/{deviceId}/datapoints", JsonOptions, cancellationToken);
            return response ?? new List<DataPointDto>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to get data points for device {deviceId}: {ex.Message}");
            return new List<DataPointDto>();
        }
    }
    
    /// <summary>
    /// 根据ID获取设备单体详情 (GET /api/admin/devices/{deviceId})
    /// </summary>
    public async Task<object?> GetDeviceByIdAsync(Guid deviceId, CancellationToken cancellationToken = default)
    {
        try { return await _httpClient.GetFromJsonAsync<object>($"/api/admin/devices/{deviceId}", JsonOptions, cancellationToken); }
        catch { return null; }
    }

    /// <summary>
    /// 创建新设备 (POST /api/admin/devices)
    /// </summary>
    public async Task<object?> CreateDeviceAsync(DeviceDto deviceDto, CancellationToken cancellationToken = default)
    {
        try
        {
            var r = await _httpClient.PostAsJsonAsync("/api/admin/devices", deviceDto, cancellationToken);
            return await r.Content.ReadFromJsonAsync<object>(JsonOptions, cancellationToken);
        }
        catch { return null; }
    }

    /// <summary>
    /// 更新设备 (PUT /api/admin/devices/{deviceId})
    /// </summary>
    public async Task<object?> UpdateDeviceAsync(Guid deviceId, DeviceDto deviceDto, CancellationToken cancellationToken = default)
    {
        try
        {
            var r = await _httpClient.PutAsJsonAsync($"/api/admin/devices/{deviceId}", deviceDto, cancellationToken);
            return await r.Content.ReadFromJsonAsync<object>(JsonOptions, cancellationToken);
        }
        catch { return null; }
    }

    /// <summary>
    /// 删除设备 (DELETE /api/admin/devices/{deviceId})
    /// </summary>
    public async Task<bool> DeleteDeviceAsync(Guid deviceId, CancellationToken cancellationToken = default)
    {
        try
        {
            var r = await _httpClient.DeleteAsync($"/api/admin/devices/{deviceId}", cancellationToken);
            return r.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    /// <summary>
    /// 获取单个数据点的NodeId (GET /api/opc/datapoints/{dataPointId}/nodeid)
    /// </summary>
    public async Task<object?> GetDataPointNodeIdAsync(Guid dataPointId, CancellationToken cancellationToken = default)
    {
        try { return await _httpClient.GetFromJsonAsync<object>($"/api/opc/datapoints/{dataPointId}/nodeid", JsonOptions, cancellationToken); }
        catch { return null; }
    }

    /// <summary>
    /// 批量获取数据点的NodeId (POST /api/opc/datapoints/nodeid/batch-get)
    /// </summary>
    public async Task<object?> BatchGetDataPointNodeIdsAsync(BatchGetNodeIdRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var r = await _httpClient.PostAsJsonAsync("/api/opc/datapoints/nodeid/batch-get", request, cancellationToken);
            return await r.Content.ReadFromJsonAsync<object>(JsonOptions, cancellationToken);
        }
        catch { return null; }
    }

    /// <summary>
    /// 更新单个数据点的NodeId (POST /api/opc/datapoints/{dataPointId}/nodeid)
    /// </summary>
    public async Task<object?> UpdateDataPointNodeIdAsync(Guid dataPointId, UpdateDataPointNodeIdRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var r = await _httpClient.PostAsJsonAsync($"/api/opc/datapoints/{dataPointId}/nodeid", request, cancellationToken);
            return await r.Content.ReadFromJsonAsync<object>(JsonOptions, cancellationToken);
        }
        catch { return null; }
    }

    /// <summary>
    /// 批量更新数据点的NodeId (POST /api/opc/datapoints/nodeid/batch)
    /// </summary>
    public async Task<object?> BatchUpdateDataPointNodeIdsAsync(List<UpdateNodeIdBatchRequest> request, CancellationToken cancellationToken = default)
    {
        try
        {
            var r = await _httpClient.PostAsJsonAsync("/api/opc/datapoints/nodeid/batch", request, cancellationToken);
            return await r.Content.ReadFromJsonAsync<object>(JsonOptions, cancellationToken);
        }
        catch { return null; }
    }

    #endregion

    #region OperationLogsController 操作日志相关 API

    /// <summary>
    /// 获取最近的操作日志 (GET /api/admin/operation-logs)
    /// </summary>
    public async Task<List<OperationLogDto>> GetOperationLogsAsync(int take = 100, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<List<OperationLogDto>>($"/api/admin/operation-logs?take={take}", JsonOptions, cancellationToken);
            return response ?? new List<OperationLogDto>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to get logs: {ex.Message}");
            return new List<OperationLogDto>();
        }
    }

    #endregion

    #region PermissionsController 权限相关 API

    /// <summary>
    /// 获取当前登录用户的权限总览 (GET /api/admin/permissions/me)
    /// </summary>
    public async Task<PermissionSummaryResponse?> GetMyPermissionsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<PermissionSummaryResponse>("/api/admin/permissions/me", JsonOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to get permissions: {ex.Message}");
            return null;
        }
    }

    #endregion

    #region PlcDataController PLC数据记录相关 API

    /// <summary>
    /// 批量保存PLC数据 (POST /api/plc-data/batch)
    /// </summary>
    public async Task<object?> BatchSavePlcDataAsync(BatchPlcDataRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var r = await _httpClient.PostAsJsonAsync("/api/plc-data/batch", request, cancellationToken);
            return await r.Content.ReadFromJsonAsync<object>(JsonOptions, cancellationToken);
        }
        catch { return null; }
    }

    /// <summary>
    /// 单条保存PLC数据 (POST /api/plc-data)
    /// </summary>
    public async Task<object?> SavePlcDataAsync(PlcDataItemRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var r = await _httpClient.PostAsJsonAsync("/api/plc-data", request, cancellationToken);
            return await r.Content.ReadFromJsonAsync<object>(JsonOptions, cancellationToken);
        }
        catch { return null; }
    }

    /// <summary>
    /// 根据ID获取PLC持久化数据 (GET /api/plc-data/{id})
    /// </summary>
    public async Task<object?> GetPlcDataByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try { return await _httpClient.GetFromJsonAsync<object>($"/api/plc-data/{id}", JsonOptions, cancellationToken); }
        catch { return null; }
    }

    /// <summary>
    /// 分页条件查询PLC数据 (GET /api/plc-data/query)
    /// </summary>
    /// <param name="queryParams">例如: "?deviceId=xx&amp;pageIndex=1&amp;pageSize=100"</param>
    public async Task<object?> QueryPlcDataAsync(string queryParams, CancellationToken cancellationToken = default)
    {
        try { return await _httpClient.GetFromJsonAsync<object>($"/api/plc-data/query{queryParams}", JsonOptions, cancellationToken); }
        catch { return null; }
    }

    /// <summary>
    /// 获取某个设备最新的PLC数据集合 (GET /api/plc-data/devices/{deviceId}/latest)
    /// </summary>
    public async Task<object?> GetLatestPlcDataAsync(Guid deviceId, int top = 10, CancellationToken cancellationToken = default)
    {
        try { return await _httpClient.GetFromJsonAsync<object>($"/api/plc-data/devices/{deviceId}/latest?top={top}", JsonOptions, cancellationToken); }
        catch { return null; }
    }

    /// <summary>
    /// 清理过期的基础PLC历史数据 (DELETE /api/plc-data/cleanup)
    /// </summary>
    public async Task<object?> CleanupOldPlcDataAsync(int retainDays = 30, CancellationToken cancellationToken = default)
    {
        try
        {
            var r = await _httpClient.DeleteAsync($"/api/plc-data/cleanup?retainDays={retainDays}", cancellationToken);
            return await r.Content.ReadFromJsonAsync<object>(JsonOptions, cancellationToken);
        }
        catch { return null; }
    }

    #endregion
}
