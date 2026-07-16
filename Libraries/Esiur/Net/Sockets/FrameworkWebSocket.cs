using Esiur.Core;
using Esiur.Misc;
using Esiur.Resource;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Esiur.Net.Sockets;

internal interface IPendingSendBudget
{
    bool TryReserve(int byteCount);
    void Release(int byteCount);
}

/// <summary>
/// Adapts a platform <see cref="WebSocket"/> to Esiur's socket abstraction.
/// </summary>
public sealed class FrameworkWebSocket : ISocket
{
    private sealed class PendingSend
    {
        public byte[] Buffer;
        public AsyncReply<bool> Reply;
        public IPendingSendBudget Budget;
    }

    public const string SubProtocol = "EP";

    private const int ReceiveBufferSize = 16 * 1024;
    private static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(2);

    private readonly object lifecycleLock = new object();
    private readonly object sendLock = new object();
    private readonly SemaphoreSlim sendOperation = new SemaphoreSlim(1, 1);
    private readonly Queue<PendingSend> sendQueue = new Queue<PendingSend>();
    private readonly byte[] receiveBuffer = new byte[ReceiveBufferSize];
    private readonly NetworkBuffer receiveNetworkBuffer = new NetworkBuffer();
    private readonly CancellationTokenSource lifetimeCancellation;
    private readonly TaskCompletionSource<object> completionSource =
        new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<object> closeNotificationSource =
        new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Uri configuredUri;

    private WebSocket socket;
    private SocketState state;
    private bool began;
    private bool closing;
    private bool completed;
    private bool destroyed;
    private bool held;
    private bool sendPumpRunning;
    private PendingSend inFlightSend;
    private int closeNotified;
    private long maximumPendingSendBytes = 16 * 1024 * 1024;
    private long pendingSendBytes;
    private long totalSent;
    private long totalReceived;

    public event DestroyedEvent OnDestroy;

    /// <summary>
    /// Creates an outbound WebSocket for an absolute <c>ws</c> or <c>wss</c> URI.
    /// The URI can include a path and query string.
    /// </summary>
    public FrameworkWebSocket(Uri uri)
    {
        configuredUri = ValidateUri(uri);
        lifetimeCancellation = new CancellationTokenSource();
        state = SocketState.Initial;
        LocalEndPoint = new IPEndPoint(IPAddress.Any, 0);
        RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
    }

    /// <summary>
    /// Wraps an accepted WebSocket. ASP.NET integrations must pass the concrete
    /// transport endpoints because EP admission controls and address-bound encryption
    /// depend on the observed peer address.
    /// </summary>
    public FrameworkWebSocket(
        WebSocket webSocket,
        IPEndPoint localEndPoint,
        IPEndPoint remoteEndPoint,
        CancellationToken cancellationToken = default)
    {
        socket = webSocket ?? throw new ArgumentNullException(nameof(webSocket));
        if (!string.Equals(webSocket.SubProtocol, SubProtocol, StringComparison.Ordinal))
            throw new ArgumentException(
                $"The WebSocket must have negotiated the '{SubProtocol}' subprotocol.",
                nameof(webSocket));

        LocalEndPoint = localEndPoint ?? throw new ArgumentNullException(nameof(localEndPoint));
        RemoteEndPoint = remoteEndPoint ?? throw new ArgumentNullException(nameof(remoteEndPoint));
        lifetimeCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        state = MapState(webSocket.State);
    }

    public INetworkReceiver<ISocket> Receiver { get; set; }

    public IPEndPoint LocalEndPoint { get; private set; }

    public IPEndPoint RemoteEndPoint { get; private set; }

    public SocketState State
    {
        get
        {
            lock (lifecycleLock)
                return state;
        }
    }

    /// <summary>
    /// Completes when the WebSocket has terminated. Unexpected transport failures
    /// fault the task; normal peer closure, local closure, and owner cancellation do not.
    /// </summary>
    public Task Completion => completionSource.Task;

    /// <summary>
    /// Completes after the protocol receiver has observed transport closure. This is
    /// separate from <see cref="Completion"/> so application callbacks cannot hold the
    /// physical transport open during host shutdown.
    /// </summary>
    internal Task CloseNotification => closeNotificationSource.Task;

    public long PendingSendBytes => Interlocked.Read(ref pendingSendBytes);

    public long TotalSent => Interlocked.Read(ref totalSent);

    public long TotalReceived => Interlocked.Read(ref totalReceived);

    internal IPendingSendBudget PendingSendBudget { get; set; }

    /// <summary>
    /// Maximum number of copied, unsent bytes retained for this connection.
    /// </summary>
    public long MaximumPendingSendBytes
    {
        get => Interlocked.Read(ref maximumPendingSendBytes);
        set
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value));

            Interlocked.Exchange(ref maximumPendingSendBytes, value);
        }
    }

    public AsyncReply<bool> Connect(string hostname, ushort port)
    {
        if (configuredUri != null)
            return Connect(configuredUri);

        if (string.IsNullOrWhiteSpace(hostname))
            throw new ArgumentException("A WebSocket host is required.", nameof(hostname));

        return Connect(new UriBuilder("ws", hostname, port).Uri);
    }

    /// <summary>
    /// Connects to a full WebSocket URI and requests the EP subprotocol.
    /// </summary>
    public async AsyncReply<bool> Connect(
        Uri uri,
        CancellationToken cancellationToken = default)
    {
        uri = ValidateUri(uri);

        ClientWebSocket client;
        lock (lifecycleLock)
        {
            if (completed || closing)
                return false;
            if (state != SocketState.Initial)
                throw new InvalidOperationException("The WebSocket has already been connected.");

            client = new ClientWebSocket();
            client.Options.AddSubProtocol(SubProtocol);
            socket = client;
            state = SocketState.Connecting;
        }

        using (var connectCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            lifetimeCancellation.Token,
            cancellationToken))
        {
            try
            {
                // ClientWebSocket does not expose its selected transport endpoint. Only
                // an IP-literal URI can therefore provide an authoritative address for
                // address-bound EP encryption; do not guess by performing a second DNS
                // lookup that may select a different address than ConnectAsync.
                var remoteAddress = IPAddress.TryParse(uri.Host, out var literalAddress)
                    ? literalAddress
                    : IPAddress.Any;
                await client.ConnectAsync(uri, connectCancellation.Token);

                if (!string.Equals(
                        client.SubProtocol,
                        SubProtocol,
                        StringComparison.Ordinal))
                {
                    throw new WebSocketException(
                        $"The server did not negotiate the required '{SubProtocol}' subprotocol.");
                }

                lock (lifecycleLock)
                {
                    if (completed || closing)
                        return false;

                    RemoteEndPoint = new IPEndPoint(remoteAddress, GetPort(uri));
                    state = SocketState.Established;
                }

                return true;
            }
            catch (Exception exception)
            {
                Finish(exception, notifyReceiver: false, abort: true);
                throw;
            }
        }
    }

    public bool Begin()
    {
        lock (lifecycleLock)
        {
            if (completed || closing || began || socket == null || state != SocketState.Established)
                return false;

            began = true;
        }

        _ = ReceiveLoopAsync();
        return true;
    }

    public AsyncReply<bool> BeginAsync() => new AsyncReply<bool>(Begin());

    public void Send(byte[] message)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        Send(message, 0, message.Length);
    }

    public void Send(byte[] message, int offset, int length)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        ValidateRange(message, offset, length);
        if (length == 0 || !CanSend())
            return;

        EnqueueSend(message, offset, length, null, throwOnCapacityFailure: true);
    }

    public AsyncReply<bool> SendAsync(byte[] message, int offset, int length)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        ValidateRange(message, offset, length);

        var reply = new AsyncReply<bool>();
        if (length == 0)
        {
            reply.Trigger(true);
            return reply;
        }

        if (!CanSend())
        {
            reply.Trigger(false);
            return reply;
        }

        EnqueueSend(message, offset, length, reply, throwOnCapacityFailure: false);
        return reply;
    }

    public void Hold()
    {
        lock (sendLock)
            held = true;
    }

    public void Unhold()
    {
        var startPump = false;
        lock (sendLock)
        {
            held = false;
            if (!sendPumpRunning && sendQueue.Count > 0 && CanSend())
            {
                sendPumpRunning = true;
                startPump = true;
            }
        }

        if (startPump)
            _ = SendLoopAsync();
    }

    public void Close() => _ = CloseAsync();

    /// <summary>
    /// Starts a graceful close and returns the same task exposed by
    /// <see cref="Completion"/>.
    /// </summary>
    public Task CloseAsync()
    {
        if (TryBeginClose())
            _ = CloseCoreAsync();

        return Completion;
    }

    public void Destroy()
    {
        DestroyedEvent destroyedHandler = null;
        lock (lifecycleLock)
        {
            if (destroyed)
                return;

            destroyed = true;
            closing = true;
            destroyedHandler = OnDestroy;
            OnDestroy = null;
        }

        Finish(null, notifyReceiver: true, abort: true);
        QueueDestroyedNotification(destroyedHandler);
    }

    public bool Trigger(ResourceOperation trigger) => true;

    public AsyncReply<ISocket> AcceptAsync() =>
        throw new NotSupportedException("A WebSocket is not a listening socket.");

    public ISocket Accept() =>
        throw new NotSupportedException("A WebSocket is not a listening socket.");

    private void EnqueueSend(
        byte[] message,
        int offset,
        int length,
        AsyncReply<bool> reply,
        bool throwOnCapacityFailure)
    {
        var startPump = false;
        var rejected = false;
        Exception capacityFailure = null;

        lock (sendLock)
        {
            if (!CanSend())
            {
                rejected = true;
            }
            else
            {
                try
                {
                    EnsureSendCapacity(length);
                }
                catch (Exception exception)
                {
                    capacityFailure = exception;
                }

                if (capacityFailure == null)
                {
                    var budget = PendingSendBudget;
                    if (budget != null && !budget.TryReserve(length))
                    {
                        capacityFailure = new InvalidOperationException(
                            "The host-wide WebSocket send queue limit was exceeded.");
                    }
                    else
                    {
                        try
                        {
                            var copy = new byte[length];
                            Buffer.BlockCopy(message, offset, copy, 0, length);
                            sendQueue.Enqueue(new PendingSend
                            {
                                Buffer = copy,
                                Reply = reply,
                                Budget = budget,
                            });
                            Interlocked.Add(ref pendingSendBytes, length);
                        }
                        catch (Exception exception)
                        {
                            budget?.Release(length);
                            capacityFailure = exception;
                        }
                    }

                    if (capacityFailure == null && !held && !sendPumpRunning)
                    {
                        sendPumpRunning = true;
                        startPump = true;
                    }
                }
            }
        }

        if (rejected)
        {
            CompleteSendReply(reply, false, null);
            return;
        }

        if (capacityFailure != null)
        {
            if (!throwOnCapacityFailure)
                CompleteSendReply(reply, false, capacityFailure);

            // Capacity exhaustion is a terminal backpressure failure. Finish must run
            // after releasing sendLock because it drains the queue under that lock and
            // can synchronously notify a receiver that re-enters this socket.
            Finish(capacityFailure, notifyReceiver: true, abort: true);

            if (throwOnCapacityFailure)
                throw capacityFailure;

            return;
        }

        if (startPump)
            _ = SendLoopAsync();
    }

    private async Task SendLoopAsync()
    {
        while (true)
        {
            PendingSend pending;
            WebSocket currentSocket;

            lock (sendLock)
            {
                if (held || !CanSend() || sendQueue.Count == 0)
                {
                    sendPumpRunning = false;
                    return;
                }

                pending = sendQueue.Dequeue();
                inFlightSend = pending;
                currentSocket = socket;
            }

            Exception sendError = null;
            var ownsSendOperation = false;
            try
            {
                await sendOperation.WaitAsync(lifetimeCancellation.Token);
                ownsSendOperation = true;
                await currentSocket.SendAsync(
                    new ArraySegment<byte>(pending.Buffer),
                    WebSocketMessageType.Binary,
                    endOfMessage: true,
                    lifetimeCancellation.Token);

            }
            catch (Exception exception)
            {
                sendError = exception;
            }
            finally
            {
                if (ownsSendOperation)
                    sendOperation.Release();
            }

            if (sendError == null)
            {
                Interlocked.Add(ref totalSent, pending.Buffer.Length);
                if (!TryCompleteInFlightSend(pending, true, null))
                {
                    lock (sendLock)
                        sendPumpRunning = false;
                    return;
                }

                continue;
            }

            if (IsClosingOrCompleted())
            {
                TryCompleteInFlightSend(pending, false, null);
                lock (sendLock)
                    sendPumpRunning = false;
                return;
            }

            if (lifetimeCancellation.IsCancellationRequested)
            {
                if (!TryCompleteInFlightSend(pending, false, null))
                {
                    lock (sendLock)
                        sendPumpRunning = false;
                    return;
                }

                lock (sendLock)
                    sendPumpRunning = false;
                Finish(null, notifyReceiver: true, abort: true);
                return;
            }

            if (!TryCompleteInFlightSend(pending, false, sendError))
            {
                lock (sendLock)
                    sendPumpRunning = false;
                return;
            }

            lock (sendLock)
                sendPumpRunning = false;
            Finish(sendError, notifyReceiver: true, abort: true);
            return;
        }
    }

    private async Task ReceiveLoopAsync()
    {
        try
        {
            while (!lifetimeCancellation.IsCancellationRequested)
            {
                var currentSocket = socket;
                if (currentSocket == null)
                    return;

                var result = await currentSocket.ReceiveAsync(
                    new ArraySegment<byte>(receiveBuffer),
                    lifetimeCancellation.Token);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await AcknowledgeRemoteCloseAsync(currentSocket, result);
                    return;
                }

                if (result.MessageType != WebSocketMessageType.Binary)
                    throw new InvalidDataException("EP WebSockets only accept binary messages.");

                if (result.Count == 0)
                    continue;

                Interlocked.Add(ref totalReceived, result.Count);
                receiveNetworkBuffer.Write(receiveBuffer, 0, (uint)result.Count);
                Receiver?.NetworkReceive(this, receiveNetworkBuffer);
            }

            if (!IsClosingOrCompleted())
                Finish(null, notifyReceiver: true, abort: true);
        }
        catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
        {
            if (!IsClosingOrCompleted())
                Finish(null, notifyReceiver: true, abort: true);
        }
        catch (ObjectDisposedException) when (IsClosingOrCompleted())
        {
        }
        catch (Exception exception)
        {
            if (!IsClosingOrCompleted())
                Finish(exception, notifyReceiver: true, abort: true);
        }
    }

    private async Task AcknowledgeRemoteCloseAsync(
        WebSocket currentSocket,
        WebSocketReceiveResult result)
    {
        // TryBeginClose can return false because a local close is already in progress.
        // That does not prove CloseCoreAsync has sent its close frame yet: the send
        // semaphore may still be held by an application write. Always inspect the
        // platform state under the same semaphore and acknowledge CloseReceived when
        // needed. If our frame was already sent, the state is CloseSent and this is a
        // no-op before completing the transport.
        TryBeginClose();

        try
        {
            using (var closeCancellation = new CancellationTokenSource(CloseTimeout))
            {
                var ownsSendOperation = false;
                try
                {
                    await sendOperation.WaitAsync(closeCancellation.Token);
                    ownsSendOperation = true;

                    if (currentSocket.State == WebSocketState.CloseReceived)
                    {
                        await currentSocket.CloseOutputAsync(
                            result.CloseStatus ?? WebSocketCloseStatus.NormalClosure,
                            result.CloseStatusDescription ?? string.Empty,
                            closeCancellation.Token);
                    }
                }
                finally
                {
                    if (ownsSendOperation)
                        sendOperation.Release();
                }
            }
        }
        catch (Exception exception)
        {
            Global.Log("FrameworkWebSocket:Close", LogType.Debug, exception.Message);
        }
        finally
        {
            Finish(null, notifyReceiver: true, abort: false);
        }
    }

    private async Task CloseCoreAsync()
    {
        var currentSocket = socket;
        try
        {
            if (currentSocket == null
                || (currentSocket.State != WebSocketState.Open
                    && currentSocket.State != WebSocketState.CloseReceived))
            {
                Finish(null, notifyReceiver: true, abort: false);
                return;
            }

            if (currentSocket.State == WebSocketState.Open
                || currentSocket.State == WebSocketState.CloseReceived)
            {
                using (var closeCancellation = new CancellationTokenSource(CloseTimeout))
                {
                    var ownsSendOperation = false;
                    try
                    {
                        await sendOperation.WaitAsync(closeCancellation.Token);
                        ownsSendOperation = true;
                        if (currentSocket.State == WebSocketState.Open
                            || currentSocket.State == WebSocketState.CloseReceived)
                        {
                            await currentSocket.CloseOutputAsync(
                                WebSocketCloseStatus.NormalClosure,
                                string.Empty,
                                closeCancellation.Token);
                        }
                    }
                    finally
                    {
                        if (ownsSendOperation)
                            sendOperation.Release();
                    }
                }
            }

            // CloseOutputAsync sends our half of the close handshake. Keep the receive
            // loop alive until the peer acknowledges it; otherwise disposing the ASP.NET
            // socket here makes clients observe an incomplete close handshake.
            var completed = await Task.WhenAny(
                Completion,
                Task.Delay(CloseTimeout));
            if (ReferenceEquals(completed, Completion))
            {
                try { await Completion; } catch { }
            }
            else
            {
                Finish(null, notifyReceiver: true, abort: true);
            }
        }
        catch (Exception exception)
        {
            Global.Log("FrameworkWebSocket:Close", LogType.Debug, exception.Message);
            Finish(null, notifyReceiver: true, abort: true);
        }
    }

    private bool TryBeginClose()
    {
        lock (lifecycleLock)
        {
            if (completed || closing)
                return false;

            closing = true;
            state = SocketState.Closed;
            return true;
        }
    }

    private void Finish(Exception error, bool notifyReceiver, bool abort)
    {
        WebSocket currentSocket;
        INetworkReceiver<ISocket> receiver = null;
        List<PendingSend> pendingSends;

        lock (lifecycleLock)
        {
            if (completed)
                return;

            completed = true;
            closing = true;
            state = SocketState.Closed;
            currentSocket = socket;
            socket = null;

            if (notifyReceiver && Interlocked.Exchange(ref closeNotified, 1) == 0)
                receiver = Receiver;
        }

        try { lifetimeCancellation.Cancel(); } catch { }
        if (abort)
        {
            try { currentSocket?.Abort(); } catch { }
        }
        try { currentSocket?.Dispose(); } catch { }

        lock (sendLock)
        {
            pendingSends = sendQueue.ToList();
            sendQueue.Clear();
            if (inFlightSend != null)
            {
                pendingSends.Add(inFlightSend);
                inFlightSend = null;
            }
            sendPumpRunning = false;
            foreach (var pending in pendingSends)
            {
                ReleasePendingSend(pending);
                // Mark every reply terminal before the socket can publish transport
                // completion. Callbacks are never invoked synchronously under sendLock.
                CompleteSendReply(pending.Reply, false, error, detachCallbacks: true);
            }
        }

        if (error != null)
            Global.Log("FrameworkWebSocket", LogType.Debug, error.Message);

        QueueCloseNotification(receiver);
        CompleteTransport(error);
    }

    private bool CanSend()
    {
        lock (lifecycleLock)
            return !completed && !closing && state == SocketState.Established && socket != null;
    }

    private bool IsClosingOrCompleted()
    {
        lock (lifecycleLock)
            return closing || completed;
    }

    private void EnsureSendCapacity(int length)
    {
        var limit = Interlocked.Read(ref maximumPendingSendBytes);
        var pending = Interlocked.Read(ref pendingSendBytes);
        if (length > limit - pending)
        {
            throw new InvalidOperationException(
                $"The WebSocket send queue exceeded its {limit}-byte limit.");
        }
    }

    private void ReleasePendingSend(PendingSend pending)
    {
        Interlocked.Add(ref pendingSendBytes, -pending.Buffer.Length);
        pending.Budget?.Release(pending.Buffer.Length);
    }

    private bool TryCompleteInFlightSend(
        PendingSend pending,
        bool result,
        Exception error)
    {
        lock (sendLock)
        {
            if (!ReferenceEquals(inFlightSend, pending))
                return false;

            ReleasePendingSend(pending);
            // The terminal state is set while this socket still owns the send. Finish
            // cannot publish Completion in the ownership-transfer gap.
            CompleteSendReply(
                pending.Reply,
                result,
                error,
                detachCallbacks: true);
            inFlightSend = null;
            return true;
        }
    }

    private void QueueCloseNotification(INetworkReceiver<ISocket> receiver)
    {
        if (receiver == null)
        {
            closeNotificationSource.TrySetResult(null);
            return;
        }

        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                receiver.NetworkClose(this);
            }
            catch (Exception exception)
            {
                Global.Log(exception);
            }
            finally
            {
                closeNotificationSource.TrySetResult(null);
            }
        });
    }

    private void QueueDestroyedNotification(DestroyedEvent handler)
    {
        if (handler == null)
            return;

        // Preserve the historical NetworkClose-before-OnDestroy ordering without
        // allowing either consumer callback to block the caller of Destroy.
        _ = closeNotificationSource.Task.ContinueWith(
            completedNotification =>
            {
                try { handler(this); }
                catch (Exception exception) { Global.Log(exception); }
            },
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.Default);
    }

    private void CompleteTransport(Exception error)
    {
        if (error == null)
            completionSource.TrySetResult(null);
        else
            completionSource.TrySetException(error);
    }

    private static Uri ValidateUri(Uri uri)
    {
        if (uri == null)
            throw new ArgumentNullException(nameof(uri));
        if (!uri.IsAbsoluteUri ||
            (!string.Equals(uri.Scheme, "ws", StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(uri.Scheme, "wss", StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException(
                "The WebSocket URI must be absolute and use the ws or wss scheme.",
                nameof(uri));
        }

        return uri;
    }

    private static int GetPort(Uri uri) =>
        uri.IsDefaultPort
            ? string.Equals(uri.Scheme, "wss", StringComparison.OrdinalIgnoreCase) ? 443 : 80
            : uri.Port;

    private static SocketState MapState(WebSocketState webSocketState)
    {
        switch (webSocketState)
        {
            case WebSocketState.Open:
                return SocketState.Established;
            case WebSocketState.Connecting:
                return SocketState.Connecting;
            case WebSocketState.None:
                return SocketState.Initial;
            default:
                return SocketState.Closed;
        }
    }

    private static void ValidateRange(byte[] message, int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > message.Length - length)
            throw new ArgumentOutOfRangeException();
    }

    private static void CompleteSendReply(
        AsyncReply<bool> reply,
        bool result,
        Exception error,
        bool detachCallbacks = false)
    {
        if (reply == null)
            return;

        try
        {
            if (error == null && detachCallbacks)
                reply.TriggerDetached(result);
            else if (error == null)
                reply.Trigger(result);
            else if (detachCallbacks)
                reply.TriggerErrorDetached(error);
            else
                reply.TriggerError(error);
        }
        catch (Exception exception)
        {
            Global.Log(exception);
        }
    }

    public void NetworkConnect(ISocket sender) => Receiver?.NetworkConnect(this);
}
