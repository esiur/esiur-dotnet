using Esiur.Protocol;
using Esiur.Resource;
using Esiur.Security.Authority;
using Esiur.Security.Cryptography;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;

namespace Esiur.AspNetCore;

/// <summary>
/// Configures the Esiur runtime hosted by ASP.NET Core.
/// </summary>
public sealed class EsiurOptions
{
    /// <summary>The Warehouse owned by the ASP.NET Core host.</summary>
    public Warehouse Warehouse { get; set; } = new();

    /// <summary>The EP server that accepts mapped WebSocket connections.</summary>
    public EpServer Server { get; set; } = new()
    {
        ExceptionLevel = Esiur.Core.ExceptionLevel.Code,
    };

    /// <summary>
    /// Warehouse path used when <see cref="Server"/> has not already been added to the
    /// Warehouse.
    /// </summary>
    public string ServerPath { get; set; } = "sys/server";

    /// <summary>
    /// Opens the Warehouse when the host starts and closes it when the host stops.
    /// Disable only when another component explicitly owns the Warehouse lifecycle.
    /// </summary>
    public bool ManageWarehouseLifecycle { get; set; } = true;

    /// <summary>
    /// Allows browser WebSocket requests from every Origin. Non-browser clients that do
    /// not send an Origin header are always allowed.
    /// </summary>
    public bool AllowAnyWebSocketOrigin { get; set; }

    /// <summary>
    /// Browser origins allowed to open an Esiur WebSocket. When this set is empty, only
    /// the request's own origin is allowed. Once configured, it becomes the complete
    /// browser-origin allowlist.
    /// </summary>
    public ISet<string> AllowedWebSocketOrigins { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Maximum copied outbound WebSocket data retained per connection while the peer is
    /// under backpressure.
    /// </summary>
    public long MaximumPendingWebSocketSendBytes { get; set; } = 16 * 1024 * 1024;

    /// <summary>
    /// Maximum copied, unsent WebSocket data retained across every connection hosted by
    /// this Esiur runtime.
    /// </summary>
    public long MaximumTotalPendingWebSocketSendBytes { get; set; } = 256L * 1024 * 1024;

    internal IList<EsiurResourceRegistration> Resources { get; } =
        new List<EsiurResourceRegistration>();

    internal IList<Func<IServiceProvider, IAuthenticationProvider>>
        AuthenticationProviders { get; } =
        new List<Func<IServiceProvider, IAuthenticationProvider>>();

    internal IList<Func<IServiceProvider, IEncryptionProvider>>
        EncryptionProviders { get; } =
        new List<Func<IServiceProvider, IEncryptionProvider>>();

    internal IList<Action<EpServer>> ServerConfigurations { get; } =
        new List<Action<EpServer>>();

    internal IList<Action<WarehouseConfiguration>> WarehouseConfigurations { get; } =
        new List<Action<WarehouseConfiguration>>();
}

internal sealed record EsiurResourceRegistration(
    string Path,
    Func<IServiceProvider, IResource> Factory,
    bool ReuseExistingStore);

internal sealed class EsiurOptionsPostConfigure : IPostConfigureOptions<EsiurOptions>
{
    public void PostConfigure(string? name, EsiurOptions options)
    {
        if (EsiurConfigurationPath.TryNormalize(options.ServerPath, out var serverPath))
            options.ServerPath = serverPath;

        if (options.Warehouse is not null)
            foreach (var configure in options.WarehouseConfigurations)
                configure(options.Warehouse.Configuration);

        if (options.Server is not null)
        {
            // ASP.NET hosts are remotely reachable by default. Supplied core servers can
            // opt back into messages/source/trace through ConfigureServer, but must not
            // accidentally carry the core diagnostic disclosure default into production.
            options.Server.ExceptionLevel &= Esiur.Core.ExceptionLevel.Code;

            foreach (var configure in options.ServerConfigurations)
                configure(options.Server);
        }
    }
}

internal sealed class EsiurOptionsValidator : IValidateOptions<EsiurOptions>
{
    public ValidateOptionsResult Validate(string? name, EsiurOptions options)
    {
        var failures = new List<string>();

        if (options.Warehouse is null)
            failures.Add("EsiurOptions.Warehouse is required.");

        if (options.Server is null)
            failures.Add("EsiurOptions.Server is required.");

        if (!EsiurConfigurationPath.TryNormalize(options.ServerPath, out var serverPath))
        {
            failures.Add(
                "EsiurOptions.ServerPath must be a relative Warehouse path such as 'sys/server'.");
        }
        else if (!serverPath.Contains('/'))
        {
            failures.Add("EsiurOptions.ServerPath must place the server below a root store.");
        }

        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var resource in options.Resources)
        {
            if (!EsiurConfigurationPath.TryNormalize(resource.Path, out var resourcePath))
            {
                failures.Add($"The resource path '{resource.Path}' is invalid.");
                continue;
            }

            if (!paths.Add(resourcePath))
                failures.Add($"The resource path '{resourcePath}' is registered more than once.");
        }

        if (serverPath is not null && paths.Contains(serverPath))
            failures.Add($"The server path '{serverPath}' is also used by another resource.");

        if (serverPath is not null && options.Server?.Instance is null)
        {
            var rootPath = serverPath.Split('/')[0];
            var existingStore = options.Warehouse?.GetStore(rootPath);
            if (existingStore is null && !paths.Contains(rootPath))
            {
                failures.Add(
                    $"ServerPath '{serverPath}' requires a root store. " +
                    $"Call AddMemoryStore(\"{rootPath}\") or supply a Warehouse containing it.");
            }
        }

        if (options.Server?.Instance is { } serverInstance
            && options.Warehouse is not null
            && !ReferenceEquals(serverInstance.Warehouse, options.Warehouse))
        {
            failures.Add("The supplied EpServer belongs to a different Warehouse.");
        }

        if (options.Server?.IsRunning == true)
        {
            failures.Add(
                "The supplied EpServer is already listening. Stop it before handing it to ASP.NET Core.");
        }

        if (!options.ManageWarehouseLifecycle
            && (options.Server?.Instance is null || options.Resources.Count > 0))
        {
            failures.Add(
                "When ManageWarehouseLifecycle is false, attach and initialize the server and " +
                "resources before registering Esiur with ASP.NET Core.");
        }

        if (options.Server?.AllowedAuthenticationProviders is null)
            failures.Add("EpServer.AllowedAuthenticationProviders cannot be null.");

        if (options.Server?.AllowedEncryptionProviders is null)
            failures.Add("EpServer.AllowedEncryptionProviders cannot be null.");

        if (options.Server is { } authenticationServer
            && options.AuthenticationProviders.Count == 0
            && (authenticationServer.AllowedAuthenticationProviders?.Length ?? 0) == 0)
        {
            if (!authenticationServer.AllowUnauthorizedAccess)
            {
                failures.Add(
                    "Authentication is required, but no authentication provider is configured. " +
                    "Call UseAuthentication(...) or explicitly enable anonymous access.");
            }
            else if (authenticationServer.RequireEncryption)
            {
                failures.Add(
                    "Encrypted EP sessions require authentication, but no authentication " +
                    "provider is configured. Call UseAuthentication(...).");
            }
        }

        if (options.Server is { RequireEncryption: true } encryptionServer
            && options.EncryptionProviders.Count == 0
            && (encryptionServer.AllowedEncryptionProviders?.Length ?? 0) == 0)
        {
            failures.Add(
                "Encryption is required, but no encryption provider is configured. " +
                "Call UseEncryption(...).");
        }

        if (options.Server is { AuthenticationTimeout: var timeout }
            && timeout <= TimeSpan.Zero)
        {
            failures.Add("EpServer.AuthenticationTimeout must be greater than zero.");
        }

        if (options.MaximumPendingWebSocketSendBytes <= 0)
        {
            failures.Add(
                "EsiurOptions.MaximumPendingWebSocketSendBytes must be greater than zero.");
        }

        if (options.MaximumTotalPendingWebSocketSendBytes <= 0)
        {
            failures.Add(
                "EsiurOptions.MaximumTotalPendingWebSocketSendBytes must be greater than zero.");
        }
        else if (options.MaximumPendingWebSocketSendBytes
                 > options.MaximumTotalPendingWebSocketSendBytes)
        {
            failures.Add(
                "MaximumPendingWebSocketSendBytes cannot exceed the host-wide send limit.");
        }

        ValidateWarehouseConfiguration(options.Warehouse?.Configuration, failures);

        if (options.AllowAnyWebSocketOrigin && options.AllowedWebSocketOrigins.Count > 0)
        {
            failures.Add(
                "Configure either AllowAnyWebSocketOrigin or AllowedWebSocketOrigins, not both.");
        }

        foreach (var origin in options.AllowedWebSocketOrigins)
        {
            if (!EsiurOrigin.TryNormalize(origin, out _))
                failures.Add($"The WebSocket origin '{origin}' is not a valid HTTP(S) origin.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateWarehouseConfiguration(
        WarehouseConfiguration? configuration,
        List<string> failures)
    {
        if (configuration is null)
        {
            failures.Add("Warehouse.Configuration is required.");
            return;
        }

        if (configuration.Connections is null)
        {
            failures.Add("Warehouse.Configuration.Connections is required.");
        }
        else
        {
            var connections = configuration.Connections;
            if (connections.MaximumConnections < 0)
                failures.Add("MaximumConnections cannot be negative.");
            if (connections.MaximumConnectionsPerIpAddress < 0)
                failures.Add("MaximumConnectionsPerIpAddress cannot be negative.");
            if (connections.MaximumConnectionAttempts < 0)
                failures.Add("MaximumConnectionAttempts cannot be negative.");
            if (connections.MaximumConnectionAttemptsPerIpAddress < 0)
                failures.Add("MaximumConnectionAttemptsPerIpAddress cannot be negative.");
            if ((connections.MaximumConnectionAttempts > 0
                    || connections.MaximumConnectionAttemptsPerIpAddress > 0)
                && connections.ConnectionAttemptWindow <= TimeSpan.Zero)
            {
                failures.Add(
                    "ConnectionAttemptWindow must be greater than zero when attempt limiting is enabled.");
            }
        }

        if (configuration.Parser is null)
            failures.Add("Warehouse.Configuration.Parser is required.");
        else
        {
            if (configuration.Parser.MaximumCollectionItems < 0)
                failures.Add("MaximumCollectionItems cannot be negative.");
            if (configuration.Parser.MaximumTypeMetadataDepth < 0)
                failures.Add("MaximumTypeMetadataDepth cannot be negative.");
        }

        if (configuration.ResourceAttachments is null)
            failures.Add("Warehouse.Configuration.ResourceAttachments is required.");
        else
        {
            if (configuration.ResourceAttachments.MaximumAttachedResourcesPerConnection < 0)
                failures.Add("MaximumAttachedResourcesPerConnection cannot be negative.");
            if (configuration.ResourceAttachments.MaximumPendingAttachmentsPerConnection < 0)
                failures.Add("MaximumPendingAttachmentsPerConnection cannot be negative.");
        }

        if (configuration.Encryption is null)
            failures.Add("Warehouse.Configuration.Encryption is required.");

        if (configuration.RateControl is null)
            failures.Add("Warehouse.Configuration.RateControl is required.");
        else
        {
            if (configuration.RateControl.DenialsBeforeConnectionBlock < 0)
                failures.Add("DenialsBeforeConnectionBlock cannot be negative.");
            if (configuration.RateControl.DenialsBeforeConnectionBlock > 0
                && configuration.RateControl.DenialWindow <= TimeSpan.Zero)
                failures.Add("DenialWindow must be greater than zero when blocking is enabled.");
            if (configuration.RateControl.ConnectionBlockDelay < TimeSpan.Zero)
                failures.Add("ConnectionBlockDelay cannot be negative.");
        }
    }
}

internal static class EsiurConfigurationPath
{
    public static string Normalize(string path)
    {
        if (!TryNormalize(path, out var normalized))
            throw new ArgumentException(
                "A relative Warehouse path without empty, '.' or '..' segments is required.",
                nameof(path));

        return normalized;
    }

    public static bool TryNormalize(
        string? path,
        [NotNullWhen(true)] out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var candidate = path.Trim().Trim('/');
        if (candidate.Length == 0
            || candidate.Contains('\\')
            || candidate.Contains('?')
            || candidate.Contains('#'))
            return false;

        var segments = candidate.Split('/');
        if (segments.Any(segment =>
                string.IsNullOrWhiteSpace(segment) || segment is "." or ".."))
            return false;

        normalized = string.Join('/', segments);
        return true;
    }
}

internal static class EsiurOrigin
{
    public static string Normalize(string origin)
    {
        if (!TryNormalize(origin, out var normalized))
            throw new ArgumentException("A valid HTTP(S) origin is required.", nameof(origin));

        return normalized;
    }

    public static bool TryNormalize(
        string? origin,
        [NotNullWhen(true)] out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(origin)
            || !Uri.TryCreate(origin, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrEmpty(uri.Host)
            || uri.UserInfo.Length != 0
            || (uri.AbsolutePath != "/" && uri.AbsolutePath.Length != 0)
            || uri.Query.Length != 0
            || uri.Fragment.Length != 0)
            return false;

        normalized = uri.GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped);
        return true;
    }
}
