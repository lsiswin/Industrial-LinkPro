using IndustrialLinkPro.OpcServer.Contracts;

namespace IndustrialLinkPro.OpcServer.Services;

public interface IDeviceDefinitionProvider
{
    Task<IReadOnlyCollection<DeviceDefinition>> GetDefinitionsAsync(CancellationToken cancellationToken);
}
