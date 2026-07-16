using Esiur.Protocol;
using Esiur.Core;
using Esiur.Resource;
using Esiur.Security.Authority;
using Esiur.Security.Cryptography;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Esiur.AspNetCore;

internal sealed class EsiurHostedService : IHostedService
{
    private readonly EsiurRuntime runtime;
    private readonly IServiceProvider services;
    private readonly IHostApplicationLifetime applicationLifetime;
    private readonly ILogger<EsiurHostedService> logger;
    private readonly TimeSpan shutdownTimeout;
    private readonly List<IResource> attachedResources = new();
    private readonly List<(string Name, IAuthenticationProvider Provider)>
        registeredAuthenticationProviders = new();
    private readonly List<(string Name, IEncryptionProvider Provider)>
        registeredEncryptionProviders = new();
    private CancellationTokenRegistration stoppingRegistration;
    private bool lifecycleStarted;
    private bool serverStateCaptured;
    private bool originalEnableTcpListener;
    private string[] originalAllowedAuthenticationProviders = Array.Empty<string>();
    private string[] originalAllowedEncryptionProviders = Array.Empty<string>();

    public EsiurHostedService(
        EsiurRuntime runtime,
        IServiceProvider services,
        IHostApplicationLifetime applicationLifetime,
        IOptions<HostOptions> hostOptions,
        ILogger<EsiurHostedService> logger)
    {
        this.runtime = runtime;
        this.services = services;
        this.applicationLifetime = applicationLifetime;
        shutdownTimeout = hostOptions.Value.ShutdownTimeout;
        this.logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var options = runtime.Options;
        var warehouse = runtime.Warehouse;
        var server = runtime.Server;

        cancellationToken.ThrowIfCancellationRequested();

        if (!options.ManageWarehouseLifecycle && !warehouse.IsOpen)
            throw new InvalidOperationException(
                "The externally managed Warehouse must be open before Esiur starts.");

        if (options.ManageWarehouseLifecycle && warehouse.IsOpen)
            throw new InvalidOperationException(
                "The Warehouse is already open. Use manageLifecycle: false when another component owns it.");

        try
        {
            CaptureServerState(server);

            // An ASP.NET endpoint owns the transport. The EP resource still owns admission,
            // authentication and all protocol state, but it must not expose a second TCP port.
            server.EnableTcpListener = false;

            RegisterAuthenticationProviders(
                options,
                warehouse,
                server,
                registeredAuthenticationProviders);
            RegisterEncryptionProviders(
                options,
                warehouse,
                server,
                registeredEncryptionProviders);
            ValidateAllowedProviders(warehouse, server);

            foreach (var registration in options.Resources
                         .OrderBy(resource => resource.Path.Count(character => character == '/')))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (registration.ReuseExistingStore
                    && !registration.Path.Contains('/')
                    && warehouse.GetStore(registration.Path) is not null)
                    continue;

                var resource = registration.Factory(services)
                    ?? throw new InvalidOperationException(
                        $"The resource factory for '{registration.Path}' returned null.");

                if (resource.Instance is not null)
                {
                    if (ReferenceEquals(resource.Instance.Warehouse, warehouse)
                        && string.Equals(
                            resource.Instance.Link,
                            registration.Path,
                            StringComparison.Ordinal))
                        continue;

                    throw new InvalidOperationException(
                        $"The resource for '{registration.Path}' is already attached to a Warehouse.");
                }

                await warehouse.Put(registration.Path, resource);
                attachedResources.Add(resource);
            }

            if (server.Instance is null)
            {
                await warehouse.Put(options.ServerPath, server);
                attachedResources.Add(server);
            }
            else if (!ReferenceEquals(server.Instance.Warehouse, warehouse))
            {
                throw new InvalidOperationException(
                    "The configured EpServer belongs to a different Warehouse.");
            }

            if (options.ManageWarehouseLifecycle)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // Mark ownership immediately before Open so startup rollback closes a
                // partially initialized Warehouse, but provider/resource validation
                // failures before Open do not terminate an unopened user graph.
                lifecycleStarted = true;
                if (!await warehouse.Open())
                    throw new InvalidOperationException(
                        "The Warehouse is already open. Use manageLifecycle: false when another component owns it.");
                cancellationToken.ThrowIfCancellationRequested();
            }

            stoppingRegistration = applicationLifetime.ApplicationStopping.Register(
                static state =>
                {
                    var hostedService = (EsiurHostedService)state!;
                    hostedService.runtime.StopTransport();
                },
                this);

            if (!runtime.TryMarkReady(applicationLifetime.ApplicationStopping))
                throw new OperationCanceledException(
                    "The ASP.NET Core host stopped while Esiur was starting.",
                    applicationLifetime.ApplicationStopping);

            logger.LogInformation(
                "Esiur is ready on mapped ASP.NET Core WebSocket endpoints using Warehouse server {ServerPath}.",
                server.Instance?.Link ?? options.ServerPath);
        }
        catch
        {
            stoppingRegistration.Dispose();
            runtime.StopTransport();
            var cleanupFinished = !options.ManageWarehouseLifecycle || !lifecycleStarted;

            if (options.ManageWarehouseLifecycle && lifecycleStarted)
            {
                try
                {
                    // Startup cancellation must not cancel rollback immediately. Give
                    // partially initialized resources an independent, bounded window
                    // to terminate before detaching them.
                    using var cleanupCancellation = new CancellationTokenSource();
                    cleanupCancellation.CancelAfter(shutdownTimeout);
                    await CloseWarehouseAsync(warehouse, cleanupCancellation.Token);
                    cleanupFinished = true;
                }
                catch (OperationCanceledException)
                {
                    logger.LogError(
                        "Esiur startup rollback exceeded the host shutdown timeout; " +
                        "resources remain attached to avoid racing their termination callbacks.");
                }
                catch (Exception cleanupException)
                {
                    // Warehouse.Close completed with an error and released its lifecycle
                    // gate, so registrations can still be detached safely.
                    cleanupFinished = true;
                    logger.LogError(
                        cleanupException,
                        "Esiur failed to clean up after a startup failure.");
                }
            }

            if (cleanupFinished)
            {
                lifecycleStarted = false;
                CleanupHostRegistrations(warehouse, "a startup failure");
            }

            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        stoppingRegistration.Dispose();

        // ApplicationStopping normally initiates this before Kestrel waits for mapped
        // WebSocket requests. Calling it again keeps direct hosted-service shutdown safe.
        runtime.StopTransport();
        var cleanupFinished = !runtime.Options.ManageWarehouseLifecycle || !lifecycleStarted;
        OperationCanceledException? shutdownCancellation = null;

        try
        {
            await runtime.DrainTransportAsync(cancellationToken);
        }
        catch (OperationCanceledException exception)
            when (cancellationToken.IsCancellationRequested)
        {
            shutdownCancellation = exception;
        }
        catch (Exception exception)
        {
            // Transport errors are already observed per socket. Continue Warehouse
            // termination even if an unexpected adapter failure escaped the drain.
            logger.LogError(exception, "Esiur transport shutdown failed.");
        }

        if (runtime.Options.ManageWarehouseLifecycle && lifecycleStarted)
        {
            var closeTask = CloseWarehouseAsync(runtime.Warehouse);
            if (shutdownCancellation is null)
            {
                try
                {
                    await closeTask.WaitAsync(cancellationToken);
                    cleanupFinished = true;
                }
                catch (OperationCanceledException exception)
                    when (cancellationToken.IsCancellationRequested)
                {
                    shutdownCancellation = exception;
                }
                catch (Exception exception)
                {
                    // Warehouse.Close settles every resource before propagating errors.
                    cleanupFinished = true;
                    logger.LogError(exception, "Esiur failed to shut down cleanly.");
                }
            }

            if (shutdownCancellation is not null && !cleanupFinished)
            {
                // The host token no longer provides cleanup time. Continue the already
                // started close under an independent bounded deadline so cancellation
                // cannot strand a live Warehouse/provider graph.
                using var cleanupCancellation = new CancellationTokenSource();
                cleanupCancellation.CancelAfter(shutdownTimeout);
                try
                {
                    await closeTask.WaitAsync(cleanupCancellation.Token);
                    cleanupFinished = true;
                }
                catch (OperationCanceledException)
                    when (cleanupCancellation.IsCancellationRequested)
                {
                    logger.LogError(
                        "Esiur Warehouse cleanup exceeded the independent shutdown timeout; " +
                        "registrations remain attached until termination settles.");
                }
                catch (Exception exception)
                {
                    cleanupFinished = true;
                    logger.LogError(exception, "Esiur failed to shut down cleanly.");
                }
            }
        }

        if (cleanupFinished)
        {
            lifecycleStarted = false;
            CleanupHostRegistrations(runtime.Warehouse, "host shutdown");
        }

        if (shutdownCancellation is not null)
        {
            logger.LogWarning(
                "Esiur transport or Warehouse shutdown exceeded the host shutdown deadline.");
            throw shutdownCancellation;
        }
    }

    private void CaptureServerState(EpServer server)
    {
        if (serverStateCaptured)
            throw new InvalidOperationException("The Esiur hosted runtime is already started.");

        originalEnableTcpListener = server.EnableTcpListener;
        originalAllowedAuthenticationProviders =
            server.AllowedAuthenticationProviders ?? Array.Empty<string>();
        originalAllowedEncryptionProviders =
            server.AllowedEncryptionProviders ?? Array.Empty<string>();
        serverStateCaptured = true;
    }

    private void CleanupHostRegistrations(Warehouse warehouse, string operation)
    {
        for (var index = attachedResources.Count - 1; index >= 0; index--)
        {
            try
            {
                if (attachedResources[index].Instance is not null)
                    warehouse.Remove(attachedResources[index]);
            }
            catch (Exception cleanupException)
            {
                logger.LogError(
                    cleanupException,
                    "Esiur failed to detach a resource after {Operation}.",
                    operation);
            }
        }
        attachedResources.Clear();

        foreach (var registration in registeredAuthenticationProviders)
            warehouse.UnregisterAuthenticationProvider(
                registration.Name,
                registration.Provider);
        registeredAuthenticationProviders.Clear();

        foreach (var registration in registeredEncryptionProviders)
            warehouse.UnregisterEncryptionProvider(
                registration.Name,
                registration.Provider);
        registeredEncryptionProviders.Clear();

        RestoreServerState();
    }

    private void RestoreServerState()
    {
        if (!serverStateCaptured)
            return;

        runtime.Server.EnableTcpListener = originalEnableTcpListener;
        runtime.Server.AllowedAuthenticationProviders =
            originalAllowedAuthenticationProviders;
        runtime.Server.AllowedEncryptionProviders = originalAllowedEncryptionProviders;
        serverStateCaptured = false;
    }

    private void RegisterAuthenticationProviders(
        EsiurOptions options,
        Warehouse warehouse,
        EpServer server,
        List<(string Name, IAuthenticationProvider Provider)> registeredProviders)
    {
        var allowed = new HashSet<string>(
            server.AllowedAuthenticationProviders,
            StringComparer.Ordinal);

        foreach (var factory in options.AuthenticationProviders)
        {
            var provider = factory(services)
                ?? throw new InvalidOperationException(
                    "An Esiur authentication provider factory returned null.");
            var name = ResolveProviderName(provider.DefaultName, "authentication");
            var existing = warehouse.TryGetAuthenticationProvider(name);

            if (existing is null)
            {
                warehouse.RegisterAuthenticationProvider(name, provider);
                registeredProviders.Add((name, provider));
            }
            else if (!ReferenceEquals(existing, provider))
                throw new InvalidOperationException(
                    $"A different authentication provider named '{name}' is already registered.");

            allowed.Add(name);
        }

        server.AllowedAuthenticationProviders = allowed.ToArray();
    }

    private void RegisterEncryptionProviders(
        EsiurOptions options,
        Warehouse warehouse,
        EpServer server,
        List<(string Name, IEncryptionProvider Provider)> registeredProviders)
    {
        var allowed = new HashSet<string>(
            server.AllowedEncryptionProviders,
            StringComparer.Ordinal);

        foreach (var factory in options.EncryptionProviders)
        {
            var provider = factory(services)
                ?? throw new InvalidOperationException(
                    "An Esiur encryption provider factory returned null.");
            var name = ResolveProviderName(provider.DefaultName, "encryption");
            var existing = warehouse.TryGetEncryptionProvider(name);

            if (existing is null)
            {
                warehouse.RegisterEncryptionProvider(name, provider);
                registeredProviders.Add((name, provider));
            }
            else if (!ReferenceEquals(existing, provider))
                throw new InvalidOperationException(
                    $"A different encryption provider named '{name}' is already registered.");

            allowed.Add(name);
        }

        server.AllowedEncryptionProviders = allowed.ToArray();
    }

    private static void ValidateAllowedProviders(Warehouse warehouse, EpServer server)
    {
        foreach (var name in server.AllowedAuthenticationProviders)
        {
            if (warehouse.TryGetAuthenticationProvider(name) is null)
                throw new InvalidOperationException(
                    $"Allowed authentication provider '{name}' is not registered with the Warehouse.");
        }

        foreach (var name in server.AllowedEncryptionProviders)
        {
            if (warehouse.TryGetEncryptionProvider(name) is null)
                throw new InvalidOperationException(
                    $"Allowed encryption provider '{name}' is not registered with the Warehouse.");
        }

        if (!server.AllowUnauthorizedAccess
            && server.AllowedAuthenticationProviders.Length == 0)
        {
            throw new InvalidOperationException(
                "Authentication is required, but no authentication provider is allowed.");
        }

        if (server.RequireEncryption
            && server.AllowedAuthenticationProviders.Length == 0)
        {
            throw new InvalidOperationException(
                "Encrypted EP sessions require an allowed authentication provider.");
        }

        if (server.RequireEncryption && server.AllowedEncryptionProviders.Length == 0)
        {
            throw new InvalidOperationException(
                "Encryption is required, but no encryption provider is allowed.");
        }
    }

    private static string ResolveProviderName(
        string defaultName,
        string providerKind)
    {
        if (string.IsNullOrWhiteSpace(defaultName)
            || !string.Equals(defaultName, defaultName.Trim(), StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"The {providerKind} provider does not define a valid protocol name.");

        return defaultName;
    }

    private static Task<bool> CloseWarehouseAsync(Warehouse warehouse)
    {
        var completion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var reply = warehouse.Close();
        reply.Then(
            result => completion.TrySetResult(result),
            nameof(CloseWarehouseAsync),
            string.Empty,
            0);
        reply.Error(exception => completion.TrySetException(exception));
        return completion.Task;
    }

    private static Task<bool> CloseWarehouseAsync(
        Warehouse warehouse,
        CancellationToken cancellationToken)
        => CloseWarehouseAsync(warehouse).WaitAsync(cancellationToken);
}
