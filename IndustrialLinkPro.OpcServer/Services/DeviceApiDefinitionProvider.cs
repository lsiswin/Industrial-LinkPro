using IndustrialLinkPro.OpcServer.Clients;
using IndustrialLinkPro.OpcServer.Contracts;

namespace IndustrialLinkPro.OpcServer.Services;

public sealed class DeviceApiDefinitionProvider(DeviceApiClient client) : IDeviceDefinitionProvider
{
    public async Task<IReadOnlyCollection<DeviceDefinition>> GetDefinitionsAsync(CancellationToken cancellationToken)
    {
        var devices = await client.GetDevicesAsync(cancellationToken);
        foreach (var device in devices)
        {
            var points = await client.GetDataPointsAsync(device.Id, cancellationToken);
            device.DataPoints = points.ToList();
        }

        return devices;
    }
}
