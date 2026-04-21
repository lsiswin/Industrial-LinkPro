using IndustrialLinkPro.OpcServer.Clients;
using IndustrialLinkPro.OpcServer.Contracts;

namespace IndustrialLinkPro.OpcServer.Services;

public sealed class DeviceApiDefinitionProvider(DeviceApiClient client) : IDeviceDefinitionProvider
{
    public async Task<IReadOnlyCollection<DeviceDefinition>> GetDefinitionsAsync(CancellationToken cancellationToken)
    {
        var devices = await client.GetDevicesAsync(cancellationToken);
        var allPoints = new List<DataPointDefinition>();

        foreach (var device in devices)
        {
            var points = await client.GetDataPointsAsync(device.Id, cancellationToken);
            device.DataPoints = points.ToList();
            allPoints.AddRange(device.DataPoints);
        }

        // 批量获取已存在的 NodeId 并回填
        if (allPoints.Count > 0)
        {
            var nodeIds = await client.BatchGetNodeIdsAsync(allPoints.Select(x => x.Id).ToList(), cancellationToken);
            var nodeIdDict = nodeIds.ToDictionary(x => x.Id);

            foreach (var point in allPoints)
            {
                if (nodeIdDict.TryGetValue(point.Id, out var response))
                {
                    point.NodeId = response.NodeId;
                    point.NamespaceIndex = response.NamespaceIndex;
                }
            }
        }

        return devices;
    }
}
