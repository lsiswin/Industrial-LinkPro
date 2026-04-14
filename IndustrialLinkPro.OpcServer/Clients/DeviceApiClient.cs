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

    public async Task<IReadOnlyCollection<DeviceDefinition>> GetDevicesAsync(CancellationToken cancellationToken)
    {
        var devices = await httpClient.GetFromJsonAsync<List<DeviceDefinition>>(
            "/api/opc/devices",
            JsonOptions,
            cancellationToken);

        return devices ?? [];
    }

    public async Task<IReadOnlyCollection<DataPointDefinition>> GetDataPointsAsync(Guid deviceId, CancellationToken cancellationToken)
    {
        var points = await httpClient.GetFromJsonAsync<List<DataPointDefinition>>(
            $"/api/opc/devices/{deviceId}/datapoints",
            JsonOptions,
            cancellationToken);

        return points ?? [];
    }

    public async Task<string?> LoginAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Auth.UserName) || string.IsNullOrWhiteSpace(_options.Auth.Password))
        {
            return null;
        }

        var response = await httpClient.PostAsJsonAsync(
            "/api/auth/login",
            new { userName = _options.Auth.UserName, password = _options.Auth.Password },
            cancellationToken);

        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions, cancellationToken);
        return auth?.AccessToken;
    }
}
