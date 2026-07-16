using Esiur.Protocol;
using Esiur.Resource;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Esiur.AspNetCore;

/// <summary>Registers Esiur with the ASP.NET Core dependency injection container.</summary>
public static class EsiurServiceCollectionExtensions
{
    /// <summary>Adds one host-managed Esiur runtime.</summary>
    public static EsiurBuilder AddEsiur(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (services.Any(descriptor =>
                descriptor.ServiceType == typeof(EsiurRegistrationMarker)))
        {
            throw new InvalidOperationException(
                "Esiur has already been added to this service collection.");
        }

        services.AddSingleton<EsiurRegistrationMarker>();
        services.AddOptions<EsiurOptions>().ValidateOnStart();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IPostConfigureOptions<EsiurOptions>,
                EsiurOptionsPostConfigure>());
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<EsiurOptions>,
                EsiurOptionsValidator>());

        services.AddSingleton<EsiurRuntime>();
        services.AddSingleton<Warehouse>(provider =>
            provider.GetRequiredService<EsiurRuntime>().Warehouse);
        services.AddSingleton<EpServer>(provider =>
            provider.GetRequiredService<EsiurRuntime>().Server);
        services.AddSingleton<EsiurWebSocketEndpoint>();
        services.AddSingleton<IHostedService, EsiurHostedService>();

        return new EsiurBuilder(services);
    }

    /// <summary>Adds and configures one host-managed Esiur runtime.</summary>
    public static EsiurBuilder AddEsiur(
        this IServiceCollection services,
        Action<EsiurBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = services.AddEsiur();
        configure(builder);
        return builder;
    }

    /// <summary>Adds Esiur around an existing Warehouse and EP server.</summary>
    public static EsiurBuilder AddEsiur(
        this IServiceCollection services,
        Warehouse warehouse,
        EpServer server,
        bool manageWarehouseLifecycle = true)
        => services.AddEsiur()
            .UseWarehouse(warehouse, manageWarehouseLifecycle)
            .UseServer(server);

    private sealed class EsiurRegistrationMarker;
}
