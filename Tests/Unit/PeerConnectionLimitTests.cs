using System.Net;
using Esiur.Core;
using Esiur.Data;
using Esiur.Net;
using Esiur.Net.Sockets;
using Esiur.Protocol;
using Esiur.Resource;

namespace Esiur.Tests.Unit;

public class PeerConnectionLimitTests
{
    [Fact]
    public void Server_RejectsConnectionsAbovePerIpLimit_AndReleasesClosedSlot()
    {
        var warehouse = new Warehouse();
        warehouse.Configuration.Connections.MaximumConnectionsPerIpAddress = 1;

        var server = new EpServer();
        server.Instance = new Instance(warehouse, 1, "server", server, null);
        server.Connections = new AutoList<EpConnection, NetworkServer<EpConnection>>(server);

        var address = IPAddress.Parse("192.0.2.10");
        var firstSocket = new TestSocket(address, 10001);
        var first = new EpConnection();
        first.Assign(firstSocket);
        server.Add(first);

        var rejectedSocket = new TestSocket(address, 10002);
        var rejected = new EpConnection();
        rejected.Assign(rejectedSocket);
        server.Add(rejected);

        Assert.Single(server.Connections);
        Assert.Equal(SocketState.Closed, rejectedSocket.State);
        Assert.Equal(1, server.GetConnectionCount(address));

        firstSocket.Close();
        Assert.Equal(0, server.GetConnectionCount(address));

        var replacementSocket = new TestSocket(address, 10003);
        var replacement = new EpConnection();
        replacement.Assign(replacementSocket);
        server.Add(replacement);

        Assert.Equal(SocketState.Established, replacementSocket.State);
        Assert.Equal(1, server.GetConnectionCount(address));

        replacementSocket.Close();
    }

    sealed class TestSocket : ISocket
    {
        public event DestroyedEvent? OnDestroy;

        public SocketState State { get; private set; } = SocketState.Established;
        public INetworkReceiver<ISocket> Receiver { get; set; } = null!;
        public IPEndPoint RemoteEndPoint { get; }
        public IPEndPoint LocalEndPoint { get; } = new(IPAddress.Loopback, 10518);

        public TestSocket(IPAddress address, int port)
            => RemoteEndPoint = new IPEndPoint(address, port);

        public AsyncReply<bool> SendAsync(byte[] message, int offset, int length)
            => new(true);

        public void Send(byte[] message) { }
        public void Send(byte[] message, int offset, int length) { }

        public void Close()
        {
            if (State == SocketState.Closed)
                return;

            State = SocketState.Closed;
            Receiver?.NetworkClose(this);
        }

        public AsyncReply<bool> Connect(string hostname, ushort port) => new(true);
        public bool Begin() => true;
        public AsyncReply<bool> BeginAsync() => new(true);
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
}
