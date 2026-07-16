using System.Net;
using System.Net.Sockets;
using Esiur.Core;
using Esiur.Data;
using Esiur.Net;
using Esiur.Net.Packets.WebSocket;
using Esiur.Net.Sockets;

namespace Esiur.Tests.Unit;

public class SocketProtocolSecurityTests
{
    [Fact]
    public void NetworkServer_DoesNotBlockAcceptLoopOnSocketInitialization()
    {
        var neverCompletes = new AsyncReply<bool>();
        var stalled = new TestSocket(neverCompletes);
        var ready = new TestSocket(new AsyncReply<bool>(true));
        var listener = new TestListener(stalled, ready);
        var server = new TestServer
        {
            ConnectionInitializationTimeout = TimeSpan.FromMilliseconds(100)
        };

        try
        {
            server.Start(listener);

            Assert.True(SpinWait.SpinUntil(
                () => Volatile.Read(ref ready.BeginCalls) == 1,
                TimeSpan.FromSeconds(2)));
            Assert.True(SpinWait.SpinUntil(
                () => stalled.State == SocketState.Closed,
                TimeSpan.FromSeconds(2)));
            Assert.Equal(SocketState.Established, ready.State);
        }
        finally
        {
            server.Destroy();
        }
    }

    [Fact]
    public void WebSocketServer_RejectsUnmaskedClientFrames()
    {
        var transport = new TestSocket();
        var socket = new WSocket(transport, isServer: true)
        {
            Receiver = new TestReceiver()
        };
        var frame = ComposeFrame(
            WebsocketPacket.WSOpcode.BinaryFrame,
            final: true,
            masked: false,
            new byte[] { 1, 2, 3 });

        var input = new NetworkBuffer();
        input.Write(frame);
        socket.NetworkReceive(transport, input);

        Assert.Equal(SocketState.Closed, transport.State);
    }

    [Fact]
    public void WebSocket_ReassemblesFragmentsBeforeDeliveringThem()
    {
        var transport = new TestSocket();
        var receiver = new TestReceiver();
        var socket = new WSocket(transport, isServer: true)
        {
            Receiver = receiver
        };
        var first = ComposeFrame(
            WebsocketPacket.WSOpcode.BinaryFrame,
            final: false,
            masked: true,
            new byte[] { 1, 2 });
        var second = ComposeFrame(
            WebsocketPacket.WSOpcode.ContinuationFrame,
            final: true,
            masked: true,
            new byte[] { 3, 4 });

        var input = new NetworkBuffer();
        input.Write(first.Concat(second).ToArray());
        socket.NetworkReceive(transport, input);

        Assert.Equal(SocketState.Established, transport.State);
        Assert.Single(receiver.Messages);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, receiver.Messages[0]);
    }

    [Fact]
    public void WebSocket_RejectsInvalidUtf8AcrossFragments()
    {
        var transport = new TestSocket();
        var receiver = new TestReceiver();
        var socket = new WSocket(transport, isServer: true)
        {
            Receiver = receiver
        };
        var first = ComposeFrame(
            WebsocketPacket.WSOpcode.TextFrame,
            final: false,
            masked: true,
            new byte[] { 0xC3 });
        var second = ComposeFrame(
            WebsocketPacket.WSOpcode.ContinuationFrame,
            final: true,
            masked: true,
            new byte[] { 0x28 });

        var input = new NetworkBuffer();
        input.Write(first.Concat(second).ToArray());
        socket.NetworkReceive(transport, input);

        Assert.Equal(SocketState.Closed, transport.State);
        Assert.Empty(receiver.Messages);
    }

    [Fact]
    public void WebSocketClient_MasksEveryOutgoingFrameWithAFreshKey()
    {
        var transport = new TestSocket();
        var socket = new WSocket(transport, isServer: false);

        socket.Send(new byte[] { 5, 6, 7 });
        socket.Send(new byte[] { 8, 9 });

        Assert.Equal(2, transport.Sent.Count);
        var frame = transport.Sent[0];
        var secondFrame = transport.Sent[1];
        Assert.False(
            frame.Skip(2).Take(4)
                .SequenceEqual(secondFrame.Skip(2).Take(4)),
            "Each client frame must use a fresh masking key.");

        var parsed = new WebsocketPacket { ExpectedMask = true };
        Assert.Equal(frame.Length, parsed.Parse(frame, 0, (uint)frame.Length));
        Assert.Equal(new byte[] { 5, 6, 7 }, parsed.Message);

        parsed = new WebsocketPacket { ExpectedMask = true };
        Assert.Equal(
            secondFrame.Length,
            parsed.Parse(secondFrame, 0, (uint)secondFrame.Length));
        Assert.Equal(new byte[] { 8, 9 }, parsed.Message);
    }

    [Fact]
    public void WebSocket_MessageLimitAlsoAppliesToUnfragmentedFrames()
    {
        var transport = new TestSocket();
        var socket = new WSocket(transport, isServer: true)
        {
            Receiver = new TestReceiver(),
            MaximumMessageLength = 2
        };
        var frame = ComposeFrame(
            WebsocketPacket.WSOpcode.BinaryFrame,
            final: true,
            masked: true,
            new byte[] { 1, 2, 3 });

        var input = new NetworkBuffer();
        input.Write(frame);
        socket.NetworkReceive(transport, input);

        Assert.Equal(SocketState.Closed, transport.State);
    }

    [Fact]
    public void WebSocket_DestroyIsReentrantAndForwardsTheWrapperOnClose()
    {
        var transport = new TestSocket();
        var receiver = new ReentrantCloseReceiver();
        var socket = new WSocket(transport, isServer: true)
        {
            Receiver = receiver
        };
        receiver.OnClose = socket.Destroy;

        socket.Destroy();
        socket.Destroy();

        Assert.Equal(SocketState.Closed, transport.State);
        Assert.Same(socket, receiver.ClosedSender);
        Assert.Null(transport.Receiver);
    }

    [Fact]
    public async Task TcpSocket_BoundsCopiedPendingSendData()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        using var client = new Socket(
            AddressFamily.InterNetwork,
            SocketType.Stream,
            ProtocolType.Tcp);
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var connectTask = client.ConnectAsync(endpoint);
        var accepted = await listener.AcceptSocketAsync();
        await connectTask;

        var socket = new TcpSocket(accepted)
        {
            MaximumPendingSendBytes = 4
        };

        try
        {
            socket.Hold();
            socket.Send(new byte[] { 1, 2, 3, 4 });

            Assert.Equal(4, socket.PendingSendBytes);
            Assert.Throws<InvalidOperationException>(() => socket.Send(new byte[] { 5 }));

            var rejected = socket.SendAsync(new byte[] { 6 }, 0, 1);
            var exception = await Assert.ThrowsAsync<AsyncException>(async () => await rejected);
            Assert.IsType<InvalidOperationException>(exception.InnerException);
            Assert.Equal(4, socket.PendingSendBytes);
        }
        finally
        {
            socket.Destroy();
        }
    }

    [Fact]
    public async Task TcpSocket_SendCallbackFailureDoesNotCloseTheTransport()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        using var client = new Socket(
            AddressFamily.InterNetwork,
            SocketType.Stream,
            ProtocolType.Tcp);
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var connectTask = client.ConnectAsync(endpoint);
        using var accepted = await listener.AcceptSocketAsync();
        await connectTask;

        var socket = new TcpSocket(accepted);

        try
        {
            socket.Hold();
            var reply = socket.SendAsync(new byte[] { 42 }, 0, 1);
            _ = reply.Then(_ => throw new InvalidOperationException("Consumer callback failure."));

            socket.Unhold();

            Assert.True(await reply);
            Assert.Equal(SocketState.Established, socket.State);
            Assert.True(SpinWait.SpinUntil(
                () => socket.PendingSendBytes == 0,
                TimeSpan.FromSeconds(2)));
        }
        finally
        {
            socket.Destroy();
        }
    }

    private static byte[] ComposeFrame(
        WebsocketPacket.WSOpcode opcode,
        bool final,
        bool masked,
        byte[] payload)
    {
        var packet = new WebsocketPacket
        {
            FIN = final,
            Opcode = opcode,
            Mask = masked,
            MaskKey = new byte[] { 1, 2, 3, 4 },
            Message = payload
        };
        packet.Compose();
        return packet.Data;
    }

    private sealed class TestReceiver : INetworkReceiver<ISocket>
    {
        public List<byte[]> Messages { get; } = new();

        public void NetworkReceive(ISocket sender, NetworkBuffer buffer)
        {
            var message = buffer.Read();
            if (message != null)
                Messages.Add(message);
        }

        public void NetworkConnect(ISocket sender) { }
        public void NetworkClose(ISocket sender) { }
    }

    private sealed class ReentrantCloseReceiver : INetworkReceiver<ISocket>
    {
        public Action? OnClose { get; set; }
        public ISocket? ClosedSender { get; private set; }

        public void NetworkReceive(ISocket sender, NetworkBuffer buffer) { }
        public void NetworkConnect(ISocket sender) { }

        public void NetworkClose(ISocket sender)
        {
            ClosedSender = sender;
            OnClose?.Invoke();
        }
    }

    private sealed class TestSocket : ISocket
    {
        private readonly AsyncReply<bool>? beginReply;

        public event DestroyedEvent? OnDestroy;

        public SocketState State { get; private set; } = SocketState.Established;
        public INetworkReceiver<ISocket> Receiver { get; set; } = null!;
        public IPEndPoint RemoteEndPoint { get; } = new(IPAddress.Loopback, 12345);
        public IPEndPoint LocalEndPoint { get; } = new(IPAddress.Loopback, 54321);
        public List<byte[]> Sent { get; } = new();
        public int BeginCalls;

        public TestSocket(AsyncReply<bool>? beginReply = null)
            => this.beginReply = beginReply;

        public void Send(byte[] message) => Send(message, 0, message.Length);

        public void Send(byte[] message, int offset, int length)
        {
            var copy = new byte[length];
            Buffer.BlockCopy(message, offset, copy, 0, length);
            Sent.Add(copy);
        }

        public AsyncReply<bool> SendAsync(byte[] message, int offset, int length)
        {
            Send(message, offset, length);
            return new AsyncReply<bool>(true);
        }

        public void Close()
        {
            if (State == SocketState.Closed)
                return;

            State = SocketState.Closed;
            Receiver?.NetworkClose(this);
        }

        public AsyncReply<bool> Connect(string hostname, ushort port) => new(true);
        public bool Begin() => true;
        public AsyncReply<bool> BeginAsync()
        {
            Interlocked.Increment(ref BeginCalls);
            return beginReply ?? new AsyncReply<bool>(true);
        }
        public AsyncReply<ISocket> AcceptAsync() => new((ISocket)null!);
        public ISocket Accept() => null!;
        public void Hold() { }
        public void Unhold() { }

        public void Destroy()
        {
            Close();
            OnDestroy?.Invoke(this);
            OnDestroy = null;
        }
    }

    private sealed class TestListener : ISocket
    {
        private readonly Queue<ISocket> accepted;

        public TestListener(params ISocket[] accepted)
            => this.accepted = new Queue<ISocket>(accepted);

        public event DestroyedEvent? OnDestroy;
        public SocketState State { get; private set; } = SocketState.Listening;
        public INetworkReceiver<ISocket> Receiver { get; set; } = null!;
        public IPEndPoint RemoteEndPoint => null!;
        public IPEndPoint LocalEndPoint { get; } = new(IPAddress.Loopback, 10518);

        public ISocket Accept()
        {
            lock (accepted)
                return accepted.Count == 0 ? null! : accepted.Dequeue();
        }

        public void Close() => State = SocketState.Closed;
        public void Destroy()
        {
            Close();
            OnDestroy?.Invoke(this);
        }

        public AsyncReply<bool> SendAsync(byte[] message, int offset, int length) => new(false);
        public void Send(byte[] message) { }
        public void Send(byte[] message, int offset, int length) { }
        public AsyncReply<bool> Connect(string hostname, ushort port) => new(false);
        public bool Begin() => false;
        public AsyncReply<bool> BeginAsync() => new(false);
        public AsyncReply<ISocket> AcceptAsync() => new(Accept());
        public void Hold() { }
        public void Unhold() { }
    }

    private sealed class TestConnection : NetworkConnection
    {
        protected override void DataReceived(NetworkBuffer buffer) { }
        protected override void Connected() { }
        protected override void Disconnected() { }
    }

    private sealed class TestServer : NetworkServer<TestConnection>
    {
        protected override void ClientDisconnected(TestConnection connection) { }
        protected override void ClientConnected(TestConnection connection) { }
    }
}
