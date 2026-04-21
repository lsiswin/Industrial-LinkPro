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
using RadioIndustrialApp.ViewModels;

namespace RadioIndustrialApp.Services;

/// <summary>
/// 全局 HTTP 客户端服务接口 - 包装后台 DeviceApi 所有控制器接口
/// </summary>
public interface IHttpClientService
{
    // === AuthController 认证相关 ===
    Task<AuthResponse?> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default
    );
    void Logout();

    // === DashboardController 仪表盘相关 ===
    Task<DashboardStatsDto> GetDashboardStatsAsync(CancellationToken cancellationToken = default);

    // === AdminDevicesController / OpcController 设备与数据点相关 ===
    Task<List<DeviceDto>> GetDevicesAsync(CancellationToken cancellationToken = default);
    Task<List<DataPointDto>> GetDataPointsAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default
    );
    Task<object?> GetDeviceByIdAsync(Guid deviceId, CancellationToken cancellationToken = default);
    Task<object?> CreateDeviceAsync(
        DeviceDto deviceDto,
        CancellationToken cancellationToken = default
    );
    Task<object?> UpdateDeviceAsync(
        Guid deviceId,
        DeviceDto deviceDto,
        CancellationToken cancellationToken = default
    );
    Task<bool> DeleteDeviceAsync(Guid deviceId, CancellationToken cancellationToken = default);

    Task<object?> GetDataPointNodeIdAsync(
        Guid dataPointId,
        CancellationToken cancellationToken = default
    );
    Task<object?> BatchGetDataPointNodeIdsAsync(
        BatchGetNodeIdRequest request,
        CancellationToken cancellationToken = default
    );
    Task<object?> UpdateDataPointNodeIdAsync(
        Guid dataPointId,
        UpdateDataPointNodeIdRequest request,
        CancellationToken cancellationToken = default
    );
    Task<object?> BatchUpdateDataPointNodeIdsAsync(
        List<UpdateNodeIdBatchRequest> request,
        CancellationToken cancellationToken = default
    );

    // === OperationLogsController 操作日志相关 ===
    Task<List<OperationLogDto>> GetOperationLogsAsync(
        int take = 100,
        CancellationToken cancellationToken = default
    );

    // === PermissionsController 权限相关 ===
    Task<PermissionSummaryResponse?> GetMyPermissionsAsync(
        CancellationToken cancellationToken = default
    );

    // === PlcDataController PLC 数据与存储相关 ===
    Task<object?> BatchSavePlcDataAsync(
        BatchPlcDataRequest request,
        CancellationToken cancellationToken = default
    );
    Task<object?> SavePlcDataAsync(
        PlcDataItemRequest request,
        CancellationToken cancellationToken = default
    );
    Task<object?> GetPlcDataByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PlcDataPagedResponse?> QueryPlcDataAsync(
        string queryParams,
        CancellationToken cancellationToken = default
    );
    Task<List<PlcDataRecordDto>> GetLatestPlcDataAsync(
        Guid deviceId,
        int top = 10,
        CancellationToken cancellationToken = default
    );

    // === AlarmsController 报警相关 ===
    Task<List<AlarmDto>> GetActiveAlarmsAsync(CancellationToken cancellationToken = default);
    Task<List<AlarmDto>> GetAlarmHistoryAsync(
        int take = 100,
        CancellationToken cancellationToken = default
    );
    Task<bool> AcknowledgeAlarmAsync(Guid alarmId, CancellationToken cancellationToken = default);
    Task<AlarmItem> CreateAlarmAsync(
        CreateAlarmRequest request,
        CancellationToken cancellationToken = default
    );
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

    public async Task<AuthResponse?> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/api/auth/login",
                request,
                cancellationToken
            );
            if (response.IsSuccessStatusCode)
            {
                var authResult = await response.Content.ReadFromJsonAsync<AuthResponse>(
                    JsonOptions,
                    cancellationToken
                );
                if (authResult?.AccessToken != null)
                {
                    _accessToken = authResult.AccessToken;
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                        "Bearer",
                        _accessToken
                    );
                }
                return authResult;
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    public void Logout()
    {
        _accessToken = null;
        _httpClient.DefaultRequestHeaders.Authorization = null;
    }

    #endregion

    #region DashboardController 仪表盘相关 API

    public async Task<DashboardStatsDto> GetDashboardStatsAsync(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<DashboardStatsDto>(
                "/api/admin/dashboard/stats",
                JsonOptions,
                cancellationToken
            );
            return response ?? new DashboardStatsDto();
        }
        catch
        {
            return new DashboardStatsDto();
        }
    }

    #endregion

    #region AdminDevicesController / OpcController 设备与数据点相关 API

    public async Task<List<DeviceDto>> GetDevicesAsync(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<List<DeviceDto>>(
                "/api/admin/devices",
                JsonOptions,
                cancellationToken
            );
            return response ?? new List<DeviceDto>();
        }
        catch
        {
            return new List<DeviceDto>();
        }
    }

    public async Task<List<DataPointDto>> GetDataPointsAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<List<DataPointDto>>(
                $"/api/admin/devices/{deviceId}/datapoints",
                JsonOptions,
                cancellationToken
            );
            return response ?? new List<DataPointDto>();
        }
        catch
        {
            return new List<DataPointDto>();
        }
    }

    public async Task<object?> GetDeviceByIdAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default
    ) =>
        await _httpClient.GetFromJsonAsync<object>(
            $"/api/admin/devices/{deviceId}",
            JsonOptions,
            cancellationToken
        );

    public async Task<object?> CreateDeviceAsync(
        DeviceDto deviceDto,
        CancellationToken cancellationToken = default
    ) =>
        (
            await _httpClient.PostAsJsonAsync("/api/admin/devices", deviceDto, cancellationToken)
        ).Content.ReadFromJsonAsync<object>(JsonOptions, cancellationToken);

    public async Task<object?> UpdateDeviceAsync(
        Guid deviceId,
        DeviceDto deviceDto,
        CancellationToken cancellationToken = default
    ) =>
        (
            await _httpClient.PutAsJsonAsync(
                $"/api/admin/devices/{deviceId}",
                deviceDto,
                cancellationToken
            )
        ).Content.ReadFromJsonAsync<object>(JsonOptions, cancellationToken);

    public async Task<bool> DeleteDeviceAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default
    ) =>
        (
            await _httpClient.DeleteAsync($"/api/admin/devices/{deviceId}", cancellationToken)
        ).IsSuccessStatusCode;

    public async Task<object?> GetDataPointNodeIdAsync(
        Guid dataPointId,
        CancellationToken cancellationToken = default
    ) =>
        await _httpClient.GetFromJsonAsync<object>(
            $"/api/opc/datapoints/{dataPointId}/nodeid",
            JsonOptions,
            cancellationToken
        );

    public async Task<object?> BatchGetDataPointNodeIdsAsync(
        BatchGetNodeIdRequest request,
        CancellationToken cancellationToken = default
    ) =>
        (
            await _httpClient.PostAsJsonAsync(
                "/api/opc/datapoints/nodeid/batch-get",
                request,
                cancellationToken
            )
        ).Content.ReadFromJsonAsync<object>(JsonOptions, cancellationToken);

    public async Task<object?> UpdateDataPointNodeIdAsync(
        Guid dataPointId,
        UpdateDataPointNodeIdRequest request,
        CancellationToken cancellationToken = default
    ) =>
        (
            await _httpClient.PostAsJsonAsync(
                $"/api/opc/datapoints/{dataPointId}/nodeid",
                request,
                cancellationToken
            )
        ).Content.ReadFromJsonAsync<object>(JsonOptions, cancellationToken);

    public async Task<object?> BatchUpdateDataPointNodeIdsAsync(
        List<UpdateNodeIdBatchRequest> request,
        CancellationToken cancellationToken = default
    ) =>
        (
            await _httpClient.PostAsJsonAsync(
                "/api/opc/datapoints/nodeid/batch",
                request,
                cancellationToken
            )
        ).Content.ReadFromJsonAsync<object>(JsonOptions, cancellationToken);

    #endregion

    #region OperationLogsController 操作日志相关 API

    public async Task<List<OperationLogDto>> GetOperationLogsAsync(
        int take = 100,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<List<OperationLogDto>>(
                $"/api/admin/operation-logs?take={take}",
                JsonOptions,
                cancellationToken
            );
            return response ?? new List<OperationLogDto>();
        }
        catch
        {
            return new List<OperationLogDto>();
        }
    }

    #endregion

    #region PermissionsController 权限相关 API

    public async Task<PermissionSummaryResponse?> GetMyPermissionsAsync(
        CancellationToken cancellationToken = default
    ) =>
        await _httpClient.GetFromJsonAsync<PermissionSummaryResponse>(
            "/api/admin/permissions/me",
            JsonOptions,
            cancellationToken
        );

    #endregion

    #region PlcDataController PLC数据记录相关 API

    public async Task<object?> BatchSavePlcDataAsync(
        BatchPlcDataRequest request,
        CancellationToken cancellationToken = default
    ) =>
        (
            await _httpClient.PostAsJsonAsync("/api/plc-data/batch", request, cancellationToken)
        ).Content.ReadFromJsonAsync<object>(JsonOptions, cancellationToken);

    public async Task<object?> SavePlcDataAsync(
        PlcDataItemRequest request,
        CancellationToken cancellationToken = default
    ) =>
        (
            await _httpClient.PostAsJsonAsync("/api/plc-data", request, cancellationToken)
        ).Content.ReadFromJsonAsync<object>(JsonOptions, cancellationToken);

    public async Task<object?> GetPlcDataByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    ) =>
        await _httpClient.GetFromJsonAsync<object>(
            $"/api/plc-data/{id}",
            JsonOptions,
            cancellationToken
        );

    public async Task<PlcDataPagedResponse?> QueryPlcDataAsync(
        string queryParams,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<PlcDataPagedResponse>(
                $"/api/plc-data/query{queryParams}",
                JsonOptions,
                cancellationToken
            );
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<PlcDataRecordDto>> GetLatestPlcDataAsync(
        Guid deviceId,
        int top = 10,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<PlcDataRecordDto>>(
                    $"/api/plc-data/devices/{deviceId}/latest?top={top}",
                    JsonOptions,
                    cancellationToken
                ) ?? new List<PlcDataRecordDto>();
        }
        catch
        {
            return new List<PlcDataRecordDto>();
        }
    }

    #endregion

    #region AlarmsController 报警相关 API

    public async Task<List<AlarmDto>> GetActiveAlarmsAsync(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var response = await _httpClient.GetAsync(
                "/api/admin/alarms/active",
                cancellationToken
            );
            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                System.Diagnostics.Debug.WriteLine(
                    $"GetActiveAlarms failed: {response.StatusCode}, {content}"
                );
                return new List<AlarmDto>();
            }
            return await response.Content.ReadFromJsonAsync<List<AlarmDto>>(
                    JsonOptions,
                    cancellationToken
                ) ?? new List<AlarmDto>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetActiveAlarms exception: {ex.Message}");
            return new List<AlarmDto>();
        }
    }

    public async Task<List<AlarmDto>> GetAlarmHistoryAsync(
        int take = 100,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"/api/admin/alarms/history?take={take}",
                cancellationToken
            );
            if (!response.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"GetAlarmHistory failed: {response.StatusCode}"
                );
                return new List<AlarmDto>();
            }
            return await response.Content.ReadFromJsonAsync<List<AlarmDto>>(
                    JsonOptions,
                    cancellationToken
                ) ?? new List<AlarmDto>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetAlarmHistory exception: {ex.Message}");
            return new List<AlarmDto>();
        }
    }

    public async Task<bool> AcknowledgeAlarmAsync(
        Guid alarmId,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            return (
                await _httpClient.PostAsync(
                    $"/api/admin/alarms/{alarmId}/acknowledge",
                    null,
                    cancellationToken
                )
            ).IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<AlarmItem> CreateAlarmAsync(
        CreateAlarmRequest request,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var result = await _httpClient.PostAsJsonAsync(
                "/api/admin/alarms",
                request,
                cancellationToken
            );
            // 2. 检查 HTTP 状态码，如果不是 2xx (如 500, 400)，抛出异常进入 catch
            result.EnsureSuccessStatusCode();

            // 3. 将 JSON 反序列化为后端的 AlarmDto (使用 await 代替 .Result)
            var dto = await result.Content.ReadFromJsonAsync<AlarmDto>(
                JsonOptions,
                cancellationToken
            );

            if (dto == null)
                return null;

            // 4. 将 DTO 手动映射为前端所需的 ObservableObject (AlarmItem)
            return new AlarmItem
            {
                Id = dto.Id,
                // 将后端的 UTC 时间转换为前端本地机器时间进行展示
                Time = dto.OccurredAtUtc.LocalDateTime,
                Source = dto.Source,
                Message = dto.Message,
                Severity = dto.Severity,
                // 假设后端的 Status 是枚举，将其转为前端需要的字符串
                Status = dto.Status.ToString(),
                // 如果 AcknowledgedBy 为 null，则赋默认空字符串
                User = dto.AcknowledgedBy ?? string.Empty,
            };
        }
        catch
        {
            return new AlarmItem();
        }
    }

    #endregion
}
