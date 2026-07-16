using System.Net;
using Esiur.Core;
using Esiur.Net;
using Esiur.Net.Sockets;
using Esiur.Protocol;
using Esiur.Resource;

namespace Esiur.Tests.Unit;

public sealed class EpConnectionReconnectTests
{
    [Fact]
    public async Task InitialAutoReconnect_CreatesAFreshSocketAfterConnectFailure()
    {
        var failedSocket = new ConnectTestSocket(connects: false);
        var connectedSocket = new ConnectTestSocket(connects: true);
        var sockets = new Queue<ConnectTestSocket>(
            new[] { failedSocket, connectedSocket });
        var connection = new EpConnection
        {
            AutoReconnect = true,
            ReconnectInterval = 0,
            ClientSocketFactory = () => sockets.Dequeue(),
        };
        var warehouse = new Warehouse();
        await warehouse.Put("client", connection);

        var open = connection.Connect(
            hostname: "localhost",
            port: 10518,
            domain: "test");

        var completed = await Task.WhenAny(
            connectedSocket.Began,
            Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.True(
            ReferenceEquals(completed, connectedSocket.Began),
            $"Fresh socket was not started (remaining={sockets.Count}, " +
            $"failed-connects={failedSocket.ConnectCount}, " +
            $"replacement-connects={connectedSocket.ConnectCount}, " +
            $"open-error={open.Exception?.Message}).");

        Assert.Equal(1, failedSocket.ConnectCount);
        Assert.Equal(1, connectedSocket.ConnectCount);
        Assert.Equal(1, connectedSocket.BeginCount);
        Assert.Same(connectedSocket, connection.Socket);
        Assert.Empty(sockets);

        connection.Destroy();
    }

    [Fact]
    public async Task DestroyDuringPendingConnect_DoesNotAttachLateSocket()
    {
        var delayedSocket = new ConnectTestSocket(connects: null);
        var connection = new EpConnection
        {
            ClientSocketFactory = () => delayedSocket,
        };
        var warehouse = new Warehouse();
        await warehouse.Put("client", connection);

        var open = connection.Connect(
            hostname: "localhost",
            port: 10518,
            domain: "test");

        await delayedSocket.ConnectInvoked.WaitAsync(TimeSpan.FromSeconds(2));
        connection.Destroy();
        delayedSocket.CompleteConnect(true);

        Assert.Null(connection.Socket);
        Assert.Equal(0, delayedSocket.BeginCount);
        Assert.Equal(SocketState.Closed, delayedSocket.State);
        Assert.NotNull(open.Exception);
    }

    [Fact]
    public async Task DisablingAutoReconnect_CompletesDelayedOpenAndAllowsAnotherConnect()
    {
        var failedSocket = new ConnectTestSocket(connects: false);
        var factoryCalls = 0;
        var connection = new EpConnection
        {
            AutoReconnect = true,
            ReconnectInterval = 30,
            ClientSocketFactory = () =>
            {
                factoryCalls++;
                return failedSocket;
            },
        };
        var warehouse = new Warehouse();
        await warehouse.Put("client", connection);

        var open = connection.Connect(
            hostname: "localhost",
            port: 10518,
            domain: "test");

        connection.AutoReconnect = false;

        Assert.NotNull(open.Exception);
        Assert.Equal(EpConnectionStatus.Closed, connection.Status);
        Assert.Equal(1, factoryCalls);

        var replacement = new ConnectTestSocket(connects: true);
        var secondOpen = connection.Connect(replacement);
        Assert.Null(secondOpen.Exception);
        Assert.Same(replacement, connection.Socket);

        connection.Destroy();
    }

    [Fact]
    public async Task DisablingAutoReconnectDuringDisconnectDelay_PreventsReconnect()
    {
        var initialSocket = new ConnectTestSocket(connects: true);
        var factoryCalls = 0;
        var connection = new EpConnection
        {
            AutoReconnect = true,
            ReconnectInterval = 1,
            ClientSocketFactory = () =>
            {
                factoryCalls++;
                return new ConnectTestSocket(connects: true);
            },
        };
        var warehouse = new Warehouse();
        await warehouse.Put("client", connection);

        _ = connection.Connect(
            initialSocket,
            hostname: "localhost",
            port: 10518,
            domain: "test");
        initialSocket.Disconnect();
        connection.AutoReconnect = false;

        await Task.Delay(TimeSpan.FromMilliseconds(1_200));

        Assert.Equal(0, factoryCalls);
        connection.Destroy();
    }

    private sealed class ConnectTestSocket : ISocket
    {
        private readonly bool? connects;
        private readonly TaskCompletionSource began = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource connectInvoked = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private AsyncReply<bool>? pendingConnect;
        private SocketState state = SocketState.Initial;

        public ConnectTestSocket(bool? connects) => this.connects = connects;

        public event DestroyedEvent? OnDestroy;
        public SocketState State => state;
        public INetworkReceiver<ISocket> Receiver { get; set; } = null!;
        public IPEndPoint RemoteEndPoint { get; } =
            new(IPAddress.Loopback, 10518);
        public IPEndPoint LocalEndPoint { get; } =
            new(IPAddress.Loopback, 50000);
        public int ConnectCount { get; private set; }
        public int BeginCount { get; private set; }
        public Task Began => began.Task;
        public Task ConnectInvoked => connectInvoked.Task;

        public AsyncReply<bool> Connect(string hostname, ushort port)
        {
            ConnectCount++;
            connectInvoked.TrySetResult();
            if (connects is null)
            {
                pendingConnect = new AsyncReply<bool>();
                return pendingConnect;
            }

            if (connects.Value)
            {
                state = SocketState.Established;
                return new AsyncReply<bool>(true);
            }

            state = SocketState.Closed;
            var reply = new AsyncReply<bool>();
            reply.TriggerError(new InvalidOperationException("connect failed"));
            return reply;
        }

        public void CompleteConnect(bool connected)
        {
            var reply = pendingConnect
                ?? throw new InvalidOperationException("No connection is pending.");
            pendingConnect = null;
            state = connected ? SocketState.Established : SocketState.Closed;
            reply.Trigger(connected);
        }

        public void Disconnect()
        {
            state = SocketState.Closed;
            Receiver.NetworkClose(this);
        }

        public bool Begin()
        {
            BeginCount++;
            began.TrySetResult();
            return state == SocketState.Established;
        }

        public AsyncReply<bool> BeginAsync() => new(Begin());
        public AsyncReply<bool> SendAsync(byte[] message, int offset, int length) =>
            new(state == SocketState.Established);
        public void Send(byte[] message) { }
        public void Send(byte[] message, int offset, int length) { }
        public void Hold() { }
        public void Unhold() { }
        public void Close() => state = SocketState.Closed;
        public void Destroy()
        {
            state = SocketState.Closed;
            OnDestroy?.Invoke(this);
        }

        public AsyncReply<ISocket> AcceptAsync() =>
            throw new NotSupportedException();
        public ISocket Accept() => throw new NotSupportedException();
    }
}
