using System.Net;
using System.Net.WebSockets;
using Esiur.Net.Sockets;
using Esiur.Protocol;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace Esiur.AspNetCore;

internal sealed class EsiurWebSocketEndpoint
{
    private readonly EsiurRuntime runtime;
    private readonly ILogger<EsiurWebSocketEndpoint> logger;

    public EsiurWebSocketEndpoint(
        EsiurRuntime runtime,
        ILogger<EsiurWebSocketEndpoint> logger)
    {
        this.runtime = runtime;
        this.logger = logger;
    }

    public async Task HandleAsync(HttpContext context)
    {
        if (!runtime.IsReady)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return;
        }

        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status426UpgradeRequired;
            context.Response.Headers["Upgrade"] = "websocket";
            return;
        }

        if (!context.WebSockets.WebSocketRequestedProtocols.Contains(
                FrameworkWebSocket.SubProtocol,
                StringComparer.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync(
                $"The '{FrameworkWebSocket.SubProtocol}' WebSocket subprotocol is required.",
                context.RequestAborted);
            return;
        }

        var origins = context.Request.Headers.Origin;
        if (!IsOriginAllowed(origins, context))
        {
            logger.LogDebug(
                "Rejected Esiur WebSocket from disallowed origin {Origin}.",
                origins.ToString());
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        if (!TryCreateEndPoint(
                context.Connection.LocalIpAddress,
                context.Connection.LocalPort,
                out var localEndPoint)
            || !TryCreateEndPoint(
                context.Connection.RemoteIpAddress,
                context.Connection.RemotePort,
                out var remoteEndPoint))
        {
            logger.LogError(
                "Esiur cannot accept a WebSocket without concrete local and remote transport endpoints.");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return;
        }

        WebSocket? webSocket = null;
        FrameworkWebSocket? socket = null;
        EpConnection? connection = null;
        var admitted = false;

        try
        {
            webSocket = await context.WebSockets.AcceptWebSocketAsync(
                FrameworkWebSocket.SubProtocol);

            socket = new FrameworkWebSocket(
                webSocket,
                localEndPoint,
                remoteEndPoint,
                context.RequestAborted)
            {
                MaximumPendingSendBytes = runtime.Options.MaximumPendingWebSocketSendBytes,
                PendingSendBudget = runtime,
            };

            connection = new EpConnection();
            connection.Assign(socket);

            // Recheck readiness while admission is serialized with host shutdown. The
            // WebSocket upgrade can take long enough for ApplicationStopping to begin
            // after the initial readiness check.
            admitted = runtime.TryAdd(connection);
            if (!admitted)
                return;

            if (!socket.Begin())
                throw new InvalidOperationException("The Esiur WebSocket could not be started.");

            logger.LogDebug(
                "Accepted Esiur WebSocket from {RemoteEndPoint}.",
                socket.RemoteEndPoint);

            await socket.Completion.WaitAsync(context.RequestAborted);
            await socket.CloseNotification.WaitAsync(context.RequestAborted);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            logger.LogDebug("The Esiur WebSocket request was aborted by its peer or host.");
        }
        catch (WebSocketException exception)
        {
            logger.LogDebug(exception, "The Esiur WebSocket transport closed with an error.");
        }
        catch (InvalidDataException exception)
        {
            logger.LogDebug(exception, "The Esiur WebSocket sent invalid EP data.");
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "The Esiur WebSocket endpoint failed.");
        }
        finally
        {
            if (admitted && connection is not null)
            {
                try
                {
                    runtime.Server.Remove(connection);
                }
                catch (Exception exception)
                {
                    logger.LogDebug(exception, "Failed to remove an Esiur connection during cleanup.");
                }
            }

            if (connection is not null)
            {
                if (socket is not null)
                    DestroyAfterCloseNotification(connection, socket);
                else
                    DestroyConnection(connection);
            }
            else
            {
                try
                {
                    socket?.Destroy();
                    webSocket?.Dispose();
                }
                catch (Exception exception)
                {
                    logger.LogDebug(exception, "Failed to dispose an Esiur WebSocket cleanly.");
                }
            }
        }
    }

    private void DestroyAfterCloseNotification(
        EpConnection connection,
        FrameworkWebSocket socket)
    {
        // RequestAborted commonly wins the endpoint wait when a peer disappears.
        // Abort the physical transport immediately, but keep the connection assigned
        // until NetworkClose has run; otherwise its stale-socket guard would skip
        // Disconnected and the authentication provider's Logout callback.
        socket.Destroy();
        if (socket.CloseNotification.IsCompleted)
        {
            DestroyConnection(connection);
            return;
        }

        _ = DestroyAfterCloseNotificationAsync(connection, socket);
    }

    private async Task DestroyAfterCloseNotificationAsync(
        EpConnection connection,
        FrameworkWebSocket socket)
    {
        await socket.CloseNotification;
        DestroyConnection(connection);
    }

    private void DestroyConnection(EpConnection connection)
    {
        try
        {
            connection.Destroy();
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Failed to dispose an Esiur connection cleanly.");
        }
    }

    private bool IsOriginAllowed(StringValues origins, HttpContext context)
        => origins.Count == 0
            || (origins.Count == 1
                && runtime.IsOriginAllowed(
                    origins[0],
                    context.Request.Scheme,
                    context.Request.Host.Value ?? string.Empty));

    private static bool TryCreateEndPoint(
        IPAddress? address,
        int port,
        out IPEndPoint endPoint)
    {
        endPoint = null!;
        if (address is null
            || port is <= IPEndPoint.MinPort or > IPEndPoint.MaxPort
            || address.Equals(IPAddress.Any)
            || address.Equals(IPAddress.IPv6Any)
            || address.Equals(IPAddress.None)
            || address.Equals(IPAddress.IPv6None))
            return false;

        endPoint = new IPEndPoint(address, port);
        return true;
    }
}
