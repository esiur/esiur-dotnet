using Esiur.CLI.Authentication;
using Esiur.CLI.Configuration;
using Esiur.Protocol;
using Esiur.Resource;
using Esiur.Security.Authority;
using Esiur.Security.Authority.Providers;

namespace Esiur.CLI.Client;

public interface IEsiurSessionFactory
{
    Task<EsiurSession> ConnectAsync(
        ResolvedConnection settings,
        bool passwordFromStandardInput,
        TextReader input,
        TextWriter error,
        CancellationToken cancellationToken);
}

public sealed class EsiurSessionFactory(ICredentialService credentials) : IEsiurSessionFactory
{
    public async Task<EsiurSession> ConnectAsync(
        ResolvedConnection settings,
        bool passwordFromStandardInput,
        TextReader input,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var warehouse = new Warehouse();
        byte[]? password = null;
        try
        {
            var profile = settings.Profile ?? new ConnectionProfile
            {
                Name = settings.DisplayName,
                Endpoint = settings.Endpoint,
                Provider = settings.Provider,
                Identity = settings.Identity,
                Domain = settings.Domain ?? string.Empty,
            };
            var context = new EpConnectionContext
            {
                AutoReconnect = false,
                AuthenticationTimeout = settings.Timeout,
                Domain = settings.Domain ?? string.Empty,
            };

            if (PromptCredentialService.IsPasswordProvider(settings.Provider))
            {
                if (string.IsNullOrWhiteSpace(settings.Identity))
                    throw new CliException("Password authentication requires an identity.", ExitCodes.InvalidArguments);
                password = await credentials.GetPasswordAsync(
                    profile, passwordFromStandardInput, input, error, cancellationToken);
                warehouse.RegisterAuthenticationProvider(
                    new PasswordClientProvider(settings.Identity, password!));
                context.AuthenticationMode = AuthenticationMode.InitializerIdentity;
                context.AuthenticationProtocol = PasswordAuthenticationProvider.ProtocolName;
                context.Identity = settings.Identity;
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (settings.Timeout > TimeSpan.Zero) timeout.CancelAfter(settings.Timeout);
            var (endpoint, webSocketUri) = EndpointParser.Parse(settings.Endpoint);
            if (webSocketUri is not null) context.WebSocketUri = webSocketUri;
            var connectTask = AwaitReply(warehouse.Get<EpConnection>(endpoint, context), timeout.Token);
            var connection = await connectTask;
            if (connection is null)
                throw new CliException($"Could not connect to {endpoint}.", ExitCodes.ConnectionFailed);
            return new EsiurSession(warehouse, connection, settings);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            try { await warehouse.Close(); } catch { }
            throw new CliException("The connection timed out.", ExitCodes.Timeout);
        }
        catch (CliException)
        {
            try { await warehouse.Close(); } catch { }
            throw;
        }
        catch (Exception exception)
        {
            try { await warehouse.Close(); } catch { }
            var authentication = PromptCredentialService.IsPasswordProvider(settings.Provider)
                && exception.Message.Contains("auth", StringComparison.OrdinalIgnoreCase);
            throw new CliException(
                authentication ? "Authentication failed." : $"Connection failed: {exception.Message}",
                authentication ? ExitCodes.AuthenticationFailed : ExitCodes.ConnectionFailed,
                exception);
        }
        finally
        {
            if (password is not null) Array.Clear(password, 0, password.Length);
        }
    }

    internal static Task<T> AwaitReply<T>(Esiur.Core.AsyncReply<T> reply, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        reply.Then(value => completion.TrySetResult((T)value))
            .Error(exception => completion.TrySetException(exception));
        var registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        _ = completion.Task.ContinueWith(_ => registration.Dispose(), TaskScheduler.Default);
        return completion.Task;
    }
}

public sealed class EsiurSession : IAsyncDisposable
{
    internal EsiurSession(Warehouse warehouse, EpConnection connection, ResolvedConnection settings)
    {
        Warehouse = warehouse;
        Connection = connection;
        Settings = settings;
    }

    public Warehouse Warehouse { get; }
    public EpConnection Connection { get; }
    public ResolvedConnection Settings { get; }

    public async ValueTask DisposeAsync()
    {
        try { Connection.Destroy(); } catch { }
        try { await Warehouse.Close(); } catch { }
    }
}

public static class EndpointParser
{
    /// <summary>
    /// Splits an endpoint into the <c>ep://host:port</c> string used to identify
    /// the connection to <see cref="Warehouse.Get{T}"/>, and — for <c>ws(s)://</c>
    /// endpoints with a path — the literal <see cref="Uri"/> to dial as
    /// <see cref="Esiur.Protocol.EpConnectionContext.WebSocketUri"/> instead of
    /// deriving the socket URL from host/port alone.
    /// </summary>
    public static (string ConnectEndpoint, Uri? WebSocketUri) Parse(string endpoint)
    {
        ConfigurationResolver.ValidateEndpoint(endpoint);
        var uri = new Uri(endpoint);
        var connectEndpoint = $"ep://{uri.Authority}";
        var isWebSocketScheme = uri.Scheme is "ws" or "wss";
        return isWebSocketScheme ? (connectEndpoint, uri) : (connectEndpoint, null);
    }

    public static string ConnectionEndpoint(string endpoint) => Parse(endpoint).ConnectEndpoint;
}
