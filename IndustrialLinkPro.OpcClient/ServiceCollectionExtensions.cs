using IndustrialLinkPro.OpcClient.Configuration;
using IndustrialLinkPro.OpcClient.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IndustrialLinkPro.OpcClient;

/// <summary>
/// OPC Client 服务注册扩展方法
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加 OPC Client 服务
    /// </summary>
    public static IServiceCollection AddOpcClientServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 注册配置
        services.Configure<OpcClientOptions>(
            configuration.GetSection(OpcClientOptions.SectionName));

        // 注册核心服务
        services.AddSingleton<DataCacheService>();
        services.AddHostedService<OpcClientService>();
        services.AddHostedService<HealthMonitorService>();

        return services;
    }

    /// <summary>
    /// 添加 OPC Client 服务 (使用自定义配置回调)
    /// </summary>
    public static IServiceCollection AddOpcClientServices(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<OpcClientOptions>? configureOptions = null)
    {
        services.AddOpcClientServices(configuration);

        if (configureOptions != null)
        {
            services.Configure(OpcClientOptions.SectionName, configureOptions);
        }

        return services;
    }
}
