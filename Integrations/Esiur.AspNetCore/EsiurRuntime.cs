using Esiur.Protocol;
using Esiur.Net.Sockets;
using Esiur.Resource;
using Microsoft.Extensions.Options;

namespace Esiur.AspNetCore;

internal sealed class EsiurRuntime : IPendingSendBudget
{
    private readonly object lifecycleLock = new();
    private readonly HashSet<FrameworkWebSocket> stoppingSockets = new();
    private bool ready;
    private long pendingSendBytes;

    public EsiurRuntime(IOptions<EsiurOptions> options)
    {
        Options = options.Value;
        Warehouse = Options.Warehouse;
        Server = Options.Server;
        AllowedOrigins = Options.AllowedWebSocketOrigins
            .Select(EsiurOrigin.Normalize)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public EsiurOptions Options { get; }
    public Warehouse Warehouse { get; }
    public EpServer Server { get; }
    public IReadOnlySet<string> AllowedOrigins { get; }
    public bool IsReady
    {
        get
        {
            lock (lifecycleLock)
                return ready;
        }
    }

    public bool TryMarkReady(CancellationToken applicationStopping)
    {
        lock (lifecycleLock)
        {
            if (applicationStopping.IsCancellationRequested)
                return false;

            ready = true;
            return true;
        }
    }

    public bool TryAdd(EpConnection connection)
    {
        lock (lifecycleLock)
            return ready && Server.TryAdd(connection);
    }

    public void StopTransport()
    {
        // Admission and the Stop snapshot share this lock. A request that passed the
        // initial HTTP readiness check therefore cannot add a connection after shutdown
        // has already taken the server's connection snapshot.
        lock (lifecycleLock)
        {
            ready = false;
            CaptureActiveWebSockets();
            Server.Stop();
        }
    }

    public async Task DrainTransportAsync(CancellationToken cancellationToken)
    {
        FrameworkWebSocket[] sockets;
        lock (lifecycleLock)
        {
            CaptureActiveWebSockets();
            sockets = stoppingSockets.ToArray();
        }

        foreach (var socket in sockets)
            _ = socket.CloseAsync();

        try
        {
            // Normal shutdown waits for both physical closure and protocol cleanup.
            // A blocked application callback is bounded by the host stop token.
            await Task.WhenAll(sockets.Select(ObserveShutdownAsync))
                .WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A host deadline is terminal. Abort any peer that did not finish the
            // close handshake so no transport can outlive provider/resource cleanup.
            foreach (var socket in sockets)
                if (!socket.Completion.IsCompleted)
                    socket.Destroy();

            await Task.WhenAll(sockets.Select(ObserveCompletionAsync));
            throw;
        }
        finally
        {
            lock (lifecycleLock)
                foreach (var socket in sockets)
                    stoppingSockets.Remove(socket);
        }
    }

    private void CaptureActiveWebSockets()
    {
        foreach (var connection in Server.Connections.ToArray())
            if (connection.Socket is FrameworkWebSocket socket)
                stoppingSockets.Add(socket);
    }

    private static async Task ObserveCompletionAsync(FrameworkWebSocket socket)
    {
        try { await socket.Completion; }
        catch { }
    }

    private static async Task ObserveShutdownAsync(FrameworkWebSocket socket)
    {
        await ObserveCompletionAsync(socket);
        await socket.CloseNotification;
    }

    bool IPendingSendBudget.TryReserve(int byteCount)
    {
        var limit = Options.MaximumTotalPendingWebSocketSendBytes;
        while (true)
        {
            var current = Interlocked.Read(ref pendingSendBytes);
            if (byteCount > limit - current)
                return false;

            if (Interlocked.CompareExchange(
                    ref pendingSendBytes,
                    current + byteCount,
                    current) == current)
                return true;
        }
    }

    void IPendingSendBudget.Release(int byteCount)
        => Interlocked.Add(ref pendingSendBytes, -byteCount);

    public bool IsOriginAllowed(string? origin, string requestScheme, string requestAuthority)
    {
        if (string.IsNullOrWhiteSpace(origin))
            return false;

        if (Options.AllowAnyWebSocketOrigin)
            return true;

        if (!EsiurOrigin.TryNormalize(origin, out var normalized))
            return false;

        if (AllowedOrigins.Count == 0)
        {
            return EsiurOrigin.TryNormalize(
                    $"{requestScheme}://{requestAuthority}",
                    out var requestOrigin)
                && string.Equals(normalized, requestOrigin, StringComparison.OrdinalIgnoreCase);
        }

        return AllowedOrigins.Contains(normalized);
    }
}
