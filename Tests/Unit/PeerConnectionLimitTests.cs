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

    [Fact]
    public void Server_RejectsRepeatedConnectionsAbovePerIpAttemptRate()
    {
        var warehouse = new Warehouse();
        warehouse.Configuration.Connections.MaximumConnectionsPerIpAddress = 0;
        warehouse.Configuration.Connections.MaximumConnectionAttemptsPerIpAddress = 2;
        warehouse.Configuration.Connections.ConnectionAttemptWindow = TimeSpan.FromMinutes(1);

        var server = new EpServer();
        server.Instance = new Instance(warehouse, 1, "server", server, null);
        server.Connections = new AutoList<EpConnection, NetworkServer<EpConnection>>(server);
        var address = IPAddress.Parse("192.0.2.20");

        for (var index = 0; index < 2; index++)
        {
            var admittedSocket = new TestSocket(address, 11000 + index);
            var admitted = new EpConnection();
            admitted.Assign(admittedSocket);
            server.Add(admitted);
            Assert.Equal(SocketState.Established, admittedSocket.State);
            admittedSocket.Close();
        }

        var rejectedSocket = new TestSocket(address, 11002);
        var rejected = new EpConnection();
        rejected.Assign(rejectedSocket);
        server.Add(rejected);

        Assert.Equal(SocketState.Closed, rejectedSocket.State);
        Assert.Empty(server.Connections);

        var otherSocket = new TestSocket(IPAddress.Parse("192.0.2.21"), 11003);
        var other = new EpConnection();
        other.Assign(otherSocket);
        server.Add(other);
        Assert.Equal(SocketState.Established, otherSocket.State);
        otherSocket.Close();
    }

    [Fact]
    public async Task Server_ClosesStalledAuthenticationAtDeadlineAndReleasesSlot()
    {
        var warehouse = new Warehouse();
        warehouse.Configuration.Connections.MaximumConnectionsPerIpAddress = 1;
        var server = new EpServer
        {
            AuthenticationTimeout = TimeSpan.FromMilliseconds(50),
        };
        server.Instance = new Instance(warehouse, 1, "server", server, null);
        server.Connections = new AutoList<EpConnection, NetworkServer<EpConnection>>(server);

        var address = IPAddress.Parse("192.0.2.30");
        var socket = new TestSocket(address, 12000);
        var connection = new EpConnection();
        connection.Assign(socket);
        server.Add(connection);

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
        while ((socket.State != SocketState.Closed
                || server.GetConnectionCount(address) != 0
                || server.Connections.Count != 0)
            && DateTime.UtcNow < deadline)
            await Task.Delay(10);

        Assert.Equal(SocketState.Closed, socket.State);
        Assert.Equal(0, server.GetConnectionCount(address));
        Assert.Empty(server.Connections);
        Assert.False(connection.Session.Authenticated);
    }

    [Fact]
    public async Task Client_ClosesStalledAuthenticationAtDeadline()
    {
        var socket = new TestSocket(IPAddress.Parse("192.0.2.40"), 13000);
        var connection = new EpConnection
        {
            AuthenticationTimeout = TimeSpan.FromMilliseconds(50),
        };
        var warehouse = new Warehouse();
        connection.Instance = new Instance(
            warehouse,
            1,
            "example.test",
            connection,
            null);

        _ = connection.Connect(socket, "example.test", 10518, "example.test");

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
        while (socket.State != SocketState.Closed && DateTime.UtcNow < deadline)
            await Task.Delay(10);

        Assert.Equal(SocketState.Closed, socket.State);
        Assert.False(connection.Session.Authenticated);
        Assert.Equal(EpConnectionStatus.Closed, connection.Status);
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
