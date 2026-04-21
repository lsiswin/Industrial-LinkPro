using System.Net.Http.Json;
using System.Text.Json;
using IndustrialLinkPro.OpcServer.Configuration;
using IndustrialLinkPro.OpcServer.Contracts;
using Microsoft.Extensions.Options;

namespace IndustrialLinkPro.OpcServer.Clients;

public sealed class DeviceApiClient(HttpClient httpClient, IOptions<DeviceApiOptions> options)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly DeviceApiOptions _options = options.Value;

    private async Task<T?> ExecuteWithRetryAsync<T>(
        Func<Task<T?>> action,
        string actionName,
        int maxRetries = 3
    )
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                return await action();
            }
            catch (Exception ex)
            {
                if (i == maxRetries - 1)
                {
                    throw;
                }
                var delay = TimeSpan.FromSeconds(Math.Pow(2, i));
                // 这里可以使用 logger 如果将其注入，但为了保持简洁我们直接等待
                await Task.Delay(delay);
            }
        }
        return default;
    }

    public async Task<IReadOnlyCollection<DeviceDefinition>> GetDevicesAsync(
        CancellationToken cancellationToken
    )
    {
        return await ExecuteWithRetryAsync(
                async () =>
                {
                    var devices = await httpClient.GetFromJsonAsync<List<DeviceDefinition>>(
                        "/api/opc/devices",
                        JsonOptions,
                        cancellationToken
                    );

                    return (IReadOnlyCollection<DeviceDefinition>?)devices ?? [];
                },
                nameof(GetDevicesAsync)
            ) ?? [];
    }

    public async Task<IReadOnlyCollection<DataPointDefinition>> GetDataPointsAsync(
        Guid deviceId,
        CancellationToken cancellationToken
    )
    {
        return await ExecuteWithRetryAsync(
                async () =>
                {
                    var points = await httpClient.GetFromJsonAsync<List<DataPointDefinition>>(
                        $"/api/opc/devices/{deviceId}/datapoints",
                        JsonOptions,
                        cancellationToken
                    );

                    return (IReadOnlyCollection<DataPointDefinition>?)points ?? [];
                },
                nameof(GetDataPointsAsync)
            ) ?? [];
    }

    public async Task<string?> LoginAsync(CancellationToken cancellationToken)
    {
        if (
            string.IsNullOrWhiteSpace(_options.Auth.UserName)
            || string.IsNullOrWhiteSpace(_options.Auth.Password)
        )
        {
            return null;
        }

        return await ExecuteWithRetryAsync(
            async () =>
            {
                var response = await httpClient.PostAsJsonAsync(
                    "/api/auth/login",
                    new { userName = _options.Auth.UserName, password = _options.Auth.Password },
                    cancellationToken
                );

                response.EnsureSuccessStatusCode();
                var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(
                    JsonOptions,
                    cancellationToken
                );
                return auth?.AccessToken;
            },
            nameof(LoginAsync)
        );
    }

    /// <summary>
    /// 批量更新数据点的NodeId (将OPC Server创建的NodeId回写到DeviceApi)
    /// </summary>
    public async Task<bool> BatchUpdateNodeIdsAsync(
        IReadOnlyCollection<UpdateNodeIdRequest> requests,
        string? accessToken,
        CancellationToken cancellationToken
    )
    {
        if (requests.Count == 0)
        {
            return true;
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            }

            var response = await httpClient.PostAsJsonAsync(
                "/api/opc/datapoints/nodeid/batch",
                requests,
                JsonOptions,
                cancellationToken
            );

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 批量获取数据点的NodeId
    /// </summary>
    public async Task<IReadOnlyCollection<DataPointNodeIdResponse>> BatchGetNodeIdsAsync(
        IReadOnlyCollection<Guid> dataPointIds,
        CancellationToken cancellationToken
    )
    {
        if (dataPointIds.Count == 0)
        {
            return [];
        }

        try
        {
            var response = await httpClient.PostAsJsonAsync(
                "/api/opc/datapoints/nodeid/batch-get",
                new { dataPointIds },
                JsonOptions,
                cancellationToken
            );

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<
                    List<DataPointNodeIdResponse>
                >(JsonOptions, cancellationToken);
                return result ?? [];
            }

            return [];
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// 上报服务器整体汇总状态
    /// </summary>
    public async Task<bool> ReportServerStatusAsync(
        ServerStatusRequest request,
        string? accessToken,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            }

            var response = await httpClient.PostAsJsonAsync(
                "/api/opc/server/status",
                request,
                JsonOptions,
                cancellationToken);

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
