using Esiur.Protocol;
using Esiur.Resource;
using Esiur.Security.Authority;
using Esiur.Security.Authority.Providers;
using Esiur.Security.Cryptography;
using Esiur.Stores;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Esiur.AspNetCore;

/// <summary>
/// Fluent registration surface for an Esiur runtime hosted by ASP.NET Core.
/// </summary>
public sealed class EsiurBuilder
{
    internal EsiurBuilder(IServiceCollection services) => Services = services;

    /// <summary>The application service collection.</summary>
    public IServiceCollection Services { get; }

    /// <summary>Uses an existing Warehouse. The ASP.NET host owns its lifecycle by default.</summary>
    public EsiurBuilder UseWarehouse(Warehouse warehouse, bool manageLifecycle = true)
    {
        ArgumentNullException.ThrowIfNull(warehouse);
        Services.Configure<EsiurOptions>(options =>
        {
            options.Warehouse = warehouse;
            options.ManageWarehouseLifecycle = manageLifecycle;
        });
        return this;
    }

    /// <summary>
    /// Uses an existing EP server. <paramref name="serverPath"/> is used only when the
    /// server is not already attached to the Warehouse.
    /// </summary>
    public EsiurBuilder UseServer(EpServer server, string serverPath = "sys/server")
    {
        ArgumentNullException.ThrowIfNull(server);
        var path = EsiurConfigurationPath.Normalize(serverPath);
        Services.Configure<EsiurOptions>(options =>
        {
            options.Server = server;
            options.ServerPath = path;
        });
        return this;
    }

    /// <summary>Configures the final EP server, including externally supplied servers.</summary>
    public EsiurBuilder ConfigureServer(Action<EpServer> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        Services.Configure<EsiurOptions>(options => options.ServerConfigurations.Add(configure));
        return this;
    }

    /// <summary>
    /// Explicitly allows unauthenticated EP sessions. Use this only for resources whose
    /// authorization policy is safe for anonymous callers.
    /// </summary>
    public EsiurBuilder AllowAnonymous()
    {
        Services.Configure<EsiurOptions>(options =>
            options.ServerConfigurations.Add(server =>
                server.AllowUnauthorizedAccess = true));
        return this;
    }

    /// <summary>Rejects EP sessions that do not negotiate authenticated encryption.</summary>
    public EsiurBuilder RequireEncryption()
    {
        Services.Configure<EsiurOptions>(options =>
            options.ServerConfigurations.Add(server => server.RequireEncryption = true));
        return this;
    }

    /// <summary>
    /// Includes remote exception messages in addition to stable error codes. Prefer the
    /// code-only default in production because messages can contain application details.
    /// </summary>
    public EsiurBuilder IncludeExceptionMessages()
    {
        Services.Configure<EsiurOptions>(options =>
            options.ServerConfigurations.Add(server =>
                server.ExceptionLevel |= Esiur.Core.ExceptionLevel.Code
                    | Esiur.Core.ExceptionLevel.Message));
        return this;
    }

    /// <summary>Limits copied outbound WebSocket data retained per connection.</summary>
    public EsiurBuilder LimitPendingWebSocketSendBytes(long maximumBytes)
    {
        if (maximumBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumBytes));

        Services.Configure<EsiurOptions>(options =>
            options.MaximumPendingWebSocketSendBytes = maximumBytes);
        return this;
    }

    /// <summary>Limits copied outbound WebSocket data retained by the whole Esiur host.</summary>
    public EsiurBuilder LimitTotalPendingWebSocketSendBytes(long maximumBytes)
    {
        if (maximumBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumBytes));

        Services.Configure<EsiurOptions>(options =>
            options.MaximumTotalPendingWebSocketSendBytes = maximumBytes);
        return this;
    }

    /// <summary>Configures the final Warehouse runtime limits.</summary>
    public EsiurBuilder ConfigureWarehouse(Action<WarehouseConfiguration> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        Services.Configure<EsiurOptions>(options => options.WarehouseConfigurations.Add(configure));
        return this;
    }

    /// <summary>Adds an in-memory root store.</summary>
    public EsiurBuilder AddMemoryStore(string path = "sys")
        => AddResourceRegistration(
            path,
            _ => new MemoryStore(),
            reuseExistingStore: true);

    /// <summary>Adds a resource constructed through ASP.NET Core dependency injection.</summary>
    public EsiurBuilder AddResource<TResource>(string path)
        where TResource : class, IResource
        => AddResourceRegistration(
            path,
            services => ActivatorUtilities.CreateInstance<TResource>(services),
            reuseExistingStore: false);

    /// <summary>Adds an existing resource instance.</summary>
    public EsiurBuilder AddResource<TResource>(string path, TResource resource)
        where TResource : class, IResource
    {
        ArgumentNullException.ThrowIfNull(resource);
        return AddResourceRegistration(path, _ => resource, reuseExistingStore: false);
    }

    /// <summary>Adds a resource created by an application service factory.</summary>
    public EsiurBuilder AddResource<TResource>(
        string path,
        Func<IServiceProvider, TResource> factory)
        where TResource : class, IResource
    {
        ArgumentNullException.ThrowIfNull(factory);
        return AddResourceRegistration(
            path,
            services => factory(services)
                ?? throw new InvalidOperationException(
                    $"The resource factory for '{path}' returned null."),
            reuseExistingStore: false);
    }

    /// <summary>
    /// Registers and allows an authentication provider under its default protocol name.
    /// </summary>
    public EsiurBuilder UseAuthentication(IAuthenticationProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return UseAuthentication(_ => provider);
    }

    /// <summary>
    /// Registers and allows a dependency-injected authentication provider.
    /// </summary>
    public EsiurBuilder UseAuthentication<TProvider>()
        where TProvider : class, IAuthenticationProvider
    {
        Services.TryAddSingleton<TProvider>();
        return UseAuthentication(
            services => services.GetRequiredService<TProvider>());
    }

    /// <summary>
    /// Registers and allows an authentication provider factory under the provider's
    /// default protocol name.
    /// </summary>
    public EsiurBuilder UseAuthentication(
        Func<IServiceProvider, IAuthenticationProvider> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        Services.Configure<EsiurOptions>(options =>
            options.AuthenticationProviders.Add(factory));
        return this;
    }

    /// <summary>
    /// Adds server-side password authentication without requiring a custom provider
    /// class. The synchronous lookup should read a cached, precomputed Esiur password
    /// credential and return <see langword="null"/> for an unknown account.
    /// </summary>
    public EsiurBuilder UsePasswordAuthentication(
        Func<string, string, PasswordHash?> findCredential)
    {
        ArgumentNullException.ThrowIfNull(findCredential);
        return UsePasswordAuthentication(
            (_, identity, domain) => findCredential(identity, domain));
    }

    /// <summary>
    /// Adds server-side password authentication with access to the application service
    /// provider. Resolve only singleton/cached services because authentication providers
    /// are host-scoped and credential lookup is synchronous.
    /// </summary>
    public EsiurBuilder UsePasswordAuthentication(
        Func<IServiceProvider, string, string, PasswordHash?> findCredential)
    {
        ArgumentNullException.ThrowIfNull(findCredential);
        return UseAuthentication(services =>
            new CallbackPasswordAuthenticationProvider(
                (identity, domain) => findCredential(services, identity, domain)));
    }

    /// <summary>Registers and allows an encryption provider under its default name.</summary>
    public EsiurBuilder UseEncryption(IEncryptionProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return UseEncryption(_ => provider);
    }

    /// <summary>Registers and allows a dependency-injected encryption provider.</summary>
    public EsiurBuilder UseEncryption<TProvider>()
        where TProvider : class, IEncryptionProvider
    {
        Services.TryAddSingleton<TProvider>();
        return UseEncryption(
            services => services.GetRequiredService<TProvider>());
    }

    /// <summary>
    /// Registers and allows an encryption provider factory under the provider's default
    /// protocol name.
    /// </summary>
    public EsiurBuilder UseEncryption(
        Func<IServiceProvider, IEncryptionProvider> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        Services.Configure<EsiurOptions>(options =>
            options.EncryptionProviders.Add(factory));
        return this;
    }

    /// <summary>Allows browser WebSocket requests from the specified origins.</summary>
    public EsiurBuilder AllowWebSocketOrigins(params string[] origins)
    {
        ArgumentNullException.ThrowIfNull(origins);
        if (origins.Length == 0)
            throw new ArgumentException("At least one origin is required.", nameof(origins));

        var normalized = origins.Select(EsiurOrigin.Normalize).ToArray();
        Services.Configure<EsiurOptions>(options =>
        {
            foreach (var origin in normalized)
                options.AllowedWebSocketOrigins.Add(origin);
        });
        return this;
    }

    /// <summary>Allows browser WebSocket requests from every origin.</summary>
    public EsiurBuilder AllowAnyWebSocketOrigin()
    {
        Services.Configure<EsiurOptions>(options => options.AllowAnyWebSocketOrigin = true);
        return this;
    }

    private EsiurBuilder AddResourceRegistration(
        string path,
        Func<IServiceProvider, IResource> factory,
        bool reuseExistingStore)
    {
        var normalizedPath = EsiurConfigurationPath.Normalize(path);
        Services.Configure<EsiurOptions>(options =>
            options.Resources.Add(
                new EsiurResourceRegistration(normalizedPath, factory, reuseExistingStore)));
        return this;
    }
}
