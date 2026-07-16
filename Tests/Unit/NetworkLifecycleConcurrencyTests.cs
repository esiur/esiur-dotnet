using System.Collections.Concurrent;
using System.Net;
using Esiur.Core;
using Esiur.Data;
using Esiur.Net;
using Esiur.Net.Sockets;

namespace Esiur.Tests.Unit;

public class NetworkLifecycleConcurrencyTests
{
    [Fact]
    public async Task NetworkConnection_DrainsDistinctBuffersQueuedWhileParserIsBusy()
    {
        using var parserEntered = new ManualResetEventSlim();
        using var releaseParser = new ManualResetEventSlim();
        var connection = new BlockingConnection(parserEntered, releaseParser);
        var socket = new TestSocket();
        connection.Assign(socket);

        var firstBuffer = BufferWith(1);
        var secondBuffer = BufferWith(2);
        var firstReceive = Task.Run(() => connection.NetworkReceive(socket, firstBuffer));

        try
        {
            Assert.True(parserEntered.Wait(TimeSpan.FromSeconds(2)));

            var secondReceive = Task.Run(() => connection.NetworkReceive(socket, secondBuffer));
            Assert.Same(secondReceive, await Task.WhenAny(
                secondReceive,
                Task.Delay(TimeSpan.FromSeconds(2))));

            releaseParser.Set();
            var bothReceives = Task.WhenAll(firstReceive, secondReceive);
            Assert.Same(bothReceives, await Task.WhenAny(
                bothReceives,
                Task.Delay(TimeSpan.FromSeconds(2))));
            await bothReceives;

            Assert.Equal(new byte[] { 1, 2 }, connection.Received.ToArray());
            Assert.Equal(0u, secondBuffer.Available);
        }
        finally
        {
            releaseParser.Set();
            await firstReceive;
        }
    }

    [Fact]
    public async Task NetworkConnection_DrainsNewSocketBufferAfterReplacementDuringReceive()
    {
        using var parserEntered = new ManualResetEventSlim();
        using var releaseParser = new ManualResetEventSlim();
        var connection = new BlockingConnection(parserEntered, releaseParser);
        var oldSocket = new TestSocket();
        var newSocket = new TestSocket();
        connection.Assign(oldSocket);

        var firstReceive = Task.Run(() =>
            connection.NetworkReceive(oldSocket, BufferWith(1)));

        try
        {
            Assert.True(parserEntered.Wait(TimeSpan.FromSeconds(2)));

            var replacementBuffer = BufferWith(2);
            var replacementReceive = Task.Run(() =>
            {
                Assert.Same(oldSocket, connection.Unassign());
                connection.Assign(newSocket);
                connection.NetworkReceive(newSocket, replacementBuffer);
            });

            Assert.Same(replacementReceive, await Task.WhenAny(
                replacementReceive,
                Task.Delay(TimeSpan.FromSeconds(2))));

            releaseParser.Set();
            var bothReceives = Task.WhenAll(firstReceive, replacementReceive);
            Assert.Same(bothReceives, await Task.WhenAny(
                bothReceives,
                Task.Delay(TimeSpan.FromSeconds(2))));
            await bothReceives;

            Assert.Equal(new byte[] { 1, 2 }, connection.Received.ToArray());
            Assert.Equal(0u, replacementBuffer.Available);
        }
        finally
        {
            releaseParser.Set();
            await firstReceive;
        }
    }

    [Fact]
    public void NetworkConnection_IgnoresCallbacksFromUnassignedSocket()
    {
        var connection = new RecordingConnection();
        var staleSocket = new TestSocket();
        var currentSocket = new TestSocket();
        var connectEvents = 0;
        var closeEvents = 0;
        connection.OnConnect += _ => connectEvents++;
        connection.OnClose += _ => closeEvents++;

        connection.Assign(staleSocket);
        Assert.Same(staleSocket, connection.Unassign());
        connection.Assign(currentSocket);

        var staleBuffer = BufferWith(9);
        connection.NetworkReceive(staleSocket, staleBuffer);
        connection.NetworkConnect(staleSocket);
        connection.NetworkClose(staleSocket);

        Assert.Equal(1u, staleBuffer.Available);
        Assert.Empty(connection.Received);
        Assert.Equal(0, connection.ConnectedCalls);
        Assert.Equal(0, connection.DisconnectedCalls);
        Assert.Equal(0, connectEvents);
        Assert.Equal(0, closeEvents);

        connection.NetworkConnect(currentSocket);
        connection.NetworkReceive(currentSocket, BufferWith(1));
        connection.NetworkClose(currentSocket);

        Assert.Equal(new byte[] { 1 }, connection.Received.ToArray());
        Assert.Equal(1, connection.ConnectedCalls);
        Assert.Equal(1, connection.DisconnectedCalls);
        Assert.Equal(1, connectEvents);
        Assert.Equal(1, closeEvents);
    }

    [Fact]
    public void NetworkServer_ClosesSocketAcceptedAfterStopSnapshot()
    {
        var acceptedSocket = new TestSocket();
        using var listener = new BlockingListener(acceptedSocket);
        var server = new TestServer();

        try
        {
            server.Start(listener);
            Assert.True(listener.AcceptEntered.Wait(TimeSpan.FromSeconds(2)));

            server.Stop();
            listener.ReleaseAccept.Set();

            Assert.True(SpinWait.SpinUntil(
                () => acceptedSocket.State == SocketState.Closed,
                TimeSpan.FromSeconds(2)));
            Assert.Empty(server.Connections);
            Assert.Equal(0, Volatile.Read(ref acceptedSocket.BeginCalls));
            Assert.Equal(0, Volatile.Read(ref server.ClientConnectedCalls));
        }
        finally
        {
            listener.ReleaseAccept.Set();
            server.Destroy();
        }
    }

    private static NetworkBuffer BufferWith(byte value)
    {
        var buffer = new NetworkBuffer();
        buffer.Write(new[] { value });
        return buffer;
    }

    private class RecordingConnection : NetworkConnection
    {
        public ConcurrentQueue<byte> Received { get; } = new();
        public int ConnectedCalls;
        public int DisconnectedCalls;

        protected override void DataReceived(NetworkBuffer buffer)
        {
            var data = buffer.Read();
            if (data == null)
                return;

            foreach (var value in data)
                Received.Enqueue(value);
        }

        protected override void Connected()
            => Interlocked.Increment(ref ConnectedCalls);

        protected override void Disconnected()
            => Interlocked.Increment(ref DisconnectedCalls);
    }

    private sealed class BlockingConnection : RecordingConnection
    {
        private readonly ManualResetEventSlim parserEntered;
        private readonly ManualResetEventSlim releaseParser;
        private int blocked;

        public BlockingConnection(
            ManualResetEventSlim parserEntered,
            ManualResetEventSlim releaseParser)
        {
            this.parserEntered = parserEntered;
            this.releaseParser = releaseParser;
        }

        protected override void DataReceived(NetworkBuffer buffer)
        {
            base.DataReceived(buffer);

            if (Interlocked.CompareExchange(ref blocked, 1, 0) == 0)
            {
                parserEntered.Set();
                releaseParser.Wait(TimeSpan.FromSeconds(5));
            }
        }
    }

    private sealed class ServerConnection : NetworkConnection
    {
        protected override void DataReceived(NetworkBuffer buffer) { }
        protected override void Connected() { }
        protected override void Disconnected() { }
    }

    private sealed class TestServer : NetworkServer<ServerConnection>
    {
        public int ClientConnectedCalls;

        protected override void ClientConnected(ServerConnection connection)
            => Interlocked.Increment(ref ClientConnectedCalls);

        protected override void ClientDisconnected(ServerConnection connection) { }
    }

    private class TestSocket : ISocket
    {
        public event DestroyedEvent? OnDestroy;

        public SocketState State { get; protected set; } = SocketState.Established;
        public INetworkReceiver<ISocket> Receiver { get; set; } = null!;
        public IPEndPoint RemoteEndPoint { get; } = new(IPAddress.Loopback, 12345);
        public IPEndPoint LocalEndPoint { get; } = new(IPAddress.Loopback, 54321);
        public int BeginCalls;

        public void Close()
        {
            if (State == SocketState.Closed)
                return;

            State = SocketState.Closed;
            Receiver?.NetworkClose(this);
        }

        public void Destroy()
        {
            Close();
            OnDestroy?.Invoke(this);
            OnDestroy = null;
        }

        public bool Begin()
        {
            Interlocked.Increment(ref BeginCalls);
            return State == SocketState.Established;
        }

        public AsyncReply<bool> BeginAsync() => new(Begin());
        public AsyncReply<bool> Connect(string hostname, ushort port) => new(true);
        public virtual ISocket Accept() => null!;
        public AsyncReply<ISocket> AcceptAsync() => new((ISocket)null!);
        public void Send(byte[] message) { }
        public void Send(byte[] message, int offset, int length) { }
        public AsyncReply<bool> SendAsync(byte[] message, int offset, int length) => new(true);
        public void Hold() { }
        public void Unhold() { }
    }

    private sealed class BlockingListener : TestSocket, IDisposable
    {
        private readonly ISocket acceptedSocket;
        private int acceptCalls;

        public BlockingListener(ISocket acceptedSocket)
        {
            this.acceptedSocket = acceptedSocket;
            State = SocketState.Listening;
        }

        public ManualResetEventSlim AcceptEntered { get; } = new();
        public ManualResetEventSlim ReleaseAccept { get; } = new();

        public override ISocket Accept()
        {
            if (Interlocked.Increment(ref acceptCalls) != 1)
                return null!;

            AcceptEntered.Set();
            ReleaseAccept.Wait(TimeSpan.FromSeconds(5));
            return acceptedSocket;
        }

        public void Dispose()
        {
            ReleaseAccept.Set();
            AcceptEntered.Dispose();
            ReleaseAccept.Dispose();
        }
    }
}
