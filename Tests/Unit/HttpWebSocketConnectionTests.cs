using System.Net;
using System.Reflection;
using System.Text;
using Esiur.Core;
using Esiur.Data;
using Esiur.Net;
using Esiur.Net.Http;
using Esiur.Net.Packets.Http;
using Esiur.Net.Packets.WebSocket;
using Esiur.Net.Sockets;
using Esiur.Resource;

namespace Esiur.Tests.Unit;

public class HttpWebSocketConnectionTests
{
    [Fact]
    public void Handshake_RequiresGetHttp11TokensVersionAndCanonical16ByteKey()
    {
        Assert.True(HttpConnection.IsWebsocketRequest(ValidHandshake()));

        var request = ValidHandshake();
        request.Method = Esiur.Net.Packets.Http.HttpMethod.POST;
        Assert.False(HttpConnection.IsWebsocketRequest(request));

        request = ValidHandshake();
        request.Version = "HTTP/1.0";
        Assert.False(HttpConnection.IsWebsocketRequest(request));

        request = ValidHandshake();
        request.RawMethod = "get";
        Assert.False(HttpConnection.IsWebsocketRequest(request));

        request = ValidHandshake();
        request.Headers["Connection"] = "keep-alive, not-an-upgrade";
        Assert.False(HttpConnection.IsWebsocketRequest(request));

        request = ValidHandshake();
        request.Headers["Upgrade"] = "notwebsocket";
        Assert.False(HttpConnection.IsWebsocketRequest(request));

        request = ValidHandshake();
        request.Headers["Sec-WebSocket-Version"] = "12";
        Assert.False(HttpConnection.IsWebsocketRequest(request));

        request = ValidHandshake();
        request.Headers["Sec-WebSocket-Key"] = "not-base64";
        Assert.False(HttpConnection.IsWebsocketRequest(request));

        request = ValidHandshake();
        request.Headers["Sec-WebSocket-Key"] = Convert.ToBase64String(new byte[15]);
        Assert.False(HttpConnection.IsWebsocketRequest(request));
    }

    [Fact]
    public void Upgrade_SelectsOnlyAnExplicitlySupportedSubprotocol()
    {
        var request = ValidHandshake();
        request.Headers["Sec-WebSocket-Protocol"] = "chat, superchat";
        var response = new HttpResponsePacket();
        response.Headers["Sec-WebSocket-Protocol"] = "untrusted";

        Assert.True(HttpConnection.Upgrade(request, response));
        Assert.Null(response.Headers["Sec-WebSocket-Protocol"]);
        Assert.Equal("websocket", response.Headers["Upgrade"]);
        Assert.Equal("Upgrade", response.Headers["Connection"]);

        response = new HttpResponsePacket();
        Assert.True(HttpConnection.Upgrade(
            request,
            response,
            new[] { "superchat", "chat" },
            out var selected));
        Assert.Equal("superchat", selected);
        Assert.Equal("superchat", response.Headers["Sec-WebSocket-Protocol"]);
    }

    [Fact]
    public void MalformedAdvertisedUpgrade_IsRejectedBeforeFiltersOrRoutesRun()
    {
        var server = new HttpServer();
        var (connection, socket, filter) = CreateHttpConnection(server);
        var request = Encoding.ASCII.GetBytes(
            "GET / HTTP/1.1\r\n" +
            "Connection: Upgrade\r\n" +
            "Upgrade: websocket\r\n" +
            "Sec-WebSocket-Version: 13\r\n" +
            "Sec-WebSocket-Key: invalid\r\n\r\n");

        Receive(connection, socket, request);

        Assert.Equal(SocketState.Closed, socket.State);
        Assert.False(connection.WSMode);
        Assert.Equal(0, filter.ExecutionCount);
        Assert.StartsWith("HTTP/1.1 400 Bad Request", Encoding.ASCII.GetString(Assert.Single(socket.Sent)));
    }

    [Fact]
    public void ValidUpgrade_UsesServerSubprotocolConfiguration()
    {
        var server = new HttpServer
        {
            WebSocketSubprotocols = new[] { "superchat" }
        };
        var (connection, socket, filter) = CreateHttpConnection(server);
        var request = Encoding.ASCII.GetBytes(
            "GET / HTTP/1.1\r\n" +
            "Connection: keep-alive, Upgrade\r\n" +
            "Upgrade: websocket\r\n" +
            "Sec-WebSocket-Version: 13\r\n" +
            $"Sec-WebSocket-Key: {Convert.ToBase64String(new byte[16])}\r\n" +
            "Sec-WebSocket-Protocol: chat, superchat\r\n\r\n");

        Receive(connection, socket, request);

        Assert.Equal(SocketState.Established, socket.State);
        Assert.True(connection.WSMode);
        Assert.Equal("superchat", connection.WebSocketSubprotocol);
        Assert.Equal(1, filter.ExecutionCount);
        Assert.Contains(
            "sec-websocket-protocol: superchat\r\n",
            Encoding.ASCII.GetString(Assert.Single(socket.Sent)),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidUpgrade_DrainsFramesCoalescedWithTheHttpHandshake()
    {
        var (connection, socket, filter) = CreateHttpConnection();
        var handshake = Encoding.ASCII.GetBytes(
            "GET / HTTP/1.1\r\n" +
            "Connection: Upgrade\r\n" +
            "Upgrade: websocket\r\n" +
            "Sec-WebSocket-Version: 13\r\n" +
            $"Sec-WebSocket-Key: {Convert.ToBase64String(new byte[16])}\r\n\r\n");
        var input = Concat(
            handshake,
            Frame(WebsocketPacket.WSOpcode.BinaryFrame, true, true, new byte[] { 1 }),
            Frame(WebsocketPacket.WSOpcode.BinaryFrame, true, true, new byte[] { 2 }));

        Receive(connection, socket, input);

        Assert.True(connection.WSMode);
        Assert.Equal(3, filter.ExecutionCount);
        Assert.Collection(
            filter.Messages,
            message => Assert.Equal(new byte[] { 1 }, message.Payload),
            message => Assert.Equal(new byte[] { 2 }, message.Payload));
    }

    [Fact]
    public void BuiltInWebSocket_SendAlwaysRecomposesServerFramesAsUnmasked()
    {
        var (connection, socket, _) = CreateWebSocketConnection();
        var packet = new WebsocketPacket
        {
            FIN = true,
            Opcode = WebsocketPacket.WSOpcode.BinaryFrame,
            Mask = true,
            MaskKey = new byte[] { 1, 2, 3, 4 },
            Message = new byte[] { 7, 8, 9 }
        };
        packet.Compose();

        connection.Send(packet);

        var sent = ParseServerFrame(Assert.Single(socket.Sent));
        Assert.False(sent.Mask);
        Assert.Equal(new byte[] { 7, 8, 9 }, sent.Message);
    }

    [Fact]
    public void BuiltInWebSocket_ReassemblesMessagesHandlesPingAndDrainsCoalescedFrames()
    {
        var (connection, socket, filter) = CreateWebSocketConnection();
        var frames = Concat(
            Frame(WebsocketPacket.WSOpcode.TextFrame, false, true, Encoding.UTF8.GetBytes("hel")),
            Frame(WebsocketPacket.WSOpcode.Ping, true, true, Encoding.ASCII.GetBytes("p")),
            Frame(WebsocketPacket.WSOpcode.ContinuationFrame, true, true, Encoding.UTF8.GetBytes("lo")),
            Frame(WebsocketPacket.WSOpcode.BinaryFrame, true, true, new byte[] { 1, 2 }));

        Receive(connection, socket, frames);

        Assert.Equal(SocketState.Established, socket.State);
        Assert.Collection(
            filter.Messages,
            message =>
            {
                Assert.Equal(WebsocketPacket.WSOpcode.TextFrame, message.Opcode);
                Assert.Equal("hello", Encoding.UTF8.GetString(message.Payload));
            },
            message =>
            {
                Assert.Equal(WebsocketPacket.WSOpcode.BinaryFrame, message.Opcode);
                Assert.Equal(new byte[] { 1, 2 }, message.Payload);
            });

        var pong = Assert.Single(socket.Sent);
        var parsedPong = ParseServerFrame(pong);
        Assert.Equal(WebsocketPacket.WSOpcode.Pong, parsedPong.Opcode);
        Assert.Equal("p", Encoding.ASCII.GetString(parsedPong.Message));
        Assert.Equal(WebsocketPacket.WSOpcode.BinaryFrame, connection.WSRequest.Opcode);
    }

    [Fact]
    public void BuiltInWebSocket_RetainsAnIncompleteFrameWithoutLosingTheNextFrame()
    {
        var (connection, socket, filter) = CreateWebSocketConnection();
        var first = Frame(
            WebsocketPacket.WSOpcode.BinaryFrame,
            true,
            true,
            new byte[] { 1, 2, 3, 4 });
        var second = Frame(
            WebsocketPacket.WSOpcode.BinaryFrame,
            true,
            true,
            new byte[] { 5, 6 });
        var buffer = new NetworkBuffer();
        var split = first.Length - 2;

        buffer.Write(first, 0, (uint)split);
        connection.NetworkReceive(socket, buffer);
        Assert.True(buffer.Protected);
        Assert.Empty(filter.Messages);

        buffer.Write(Concat(first.Skip(split).ToArray(), second));
        connection.NetworkReceive(socket, buffer);

        Assert.False(buffer.Protected);
        Assert.Collection(
            filter.Messages,
            message => Assert.Equal(new byte[] { 1, 2, 3, 4 }, message.Payload),
            message => Assert.Equal(new byte[] { 5, 6 }, message.Payload));
    }

    [Fact]
    public void BuiltInWebSocket_RejectsUnmaskedClientFramesWithProtocolClose()
    {
        var (connection, socket, filter) = CreateWebSocketConnection();

        Receive(connection, socket, Frame(
            WebsocketPacket.WSOpcode.BinaryFrame,
            true,
            false,
            new byte[] { 1 }));

        Assert.Equal(SocketState.Closed, socket.State);
        Assert.Empty(filter.Messages);
        AssertCloseCode(socket, 1002);
    }

    [Fact]
    public void BuiltInWebSocket_RejectsInvalidUtf8AcrossFragments()
    {
        var (connection, socket, filter) = CreateWebSocketConnection();

        Receive(connection, socket, Concat(
            Frame(WebsocketPacket.WSOpcode.TextFrame, false, true, new byte[] { 0xC3 }),
            Frame(WebsocketPacket.WSOpcode.ContinuationFrame, true, true, new byte[] { 0x28 })));

        Assert.Equal(SocketState.Closed, socket.State);
        Assert.Empty(filter.Messages);
        AssertCloseCode(socket, 1007);
    }

    [Fact]
    public void BuiltInWebSocket_EnforcesAggregateFragmentLimit()
    {
        var server = new HttpServer { MaximumWebSocketMessageLength = 4 };
        var (connection, socket, filter) = CreateWebSocketConnection(server);

        Receive(connection, socket, Concat(
            Frame(WebsocketPacket.WSOpcode.BinaryFrame, false, true, new byte[] { 1, 2, 3 }),
            Frame(WebsocketPacket.WSOpcode.ContinuationFrame, true, true, new byte[] { 4, 5 })));

        Assert.Equal(SocketState.Closed, socket.State);
        Assert.Empty(filter.Messages);
        AssertCloseCode(socket, 1009);
    }

    [Fact]
    public void BuiltInWebSocket_EchoesAValidClosePayloadThenCloses()
    {
        var (connection, socket, _) = CreateWebSocketConnection();
        var payload = new byte[] { 0x03, 0xE8 };

        Receive(connection, socket, Frame(
            WebsocketPacket.WSOpcode.ConnectionClose,
            true,
            true,
            payload));

        Assert.Equal(SocketState.Closed, socket.State);
        var close = ParseServerFrame(Assert.Single(socket.Sent));
        Assert.Equal(WebsocketPacket.WSOpcode.ConnectionClose, close.Opcode);
        Assert.Equal(payload, close.Message);
    }

    [Fact]
    public void BuiltInWebSocket_WaitsForCloseFrameSendBeforeClosingTransport()
    {
        var pendingSend = new AsyncReply<bool>();
        var server = new HttpServer();
        var filter = new CaptureFilter();
        SetFilters(server, filter);
        var socket = new TestSocket(pendingSend);
        var connection = new HttpConnection { Server = server, WSMode = true };
        connection.Assign(socket);

        Receive(connection, socket, Frame(
            WebsocketPacket.WSOpcode.ConnectionClose,
            true,
            true,
            new byte[] { 0x03, 0xE8 }));

        Assert.Equal(SocketState.Established, socket.State);
        Assert.Single(socket.Sent);

        pendingSend.Trigger(true);
        Assert.Equal(SocketState.Closed, socket.State);
    }

    [Fact]
    public void ExceptionDetails_AreGenericByDefaultAndEncodedWhenOptedIn()
    {
        var server = new HttpServer();
        var connection = new HttpConnection { Server = server };
        var exception = new InvalidOperationException("<script>secret</script>");

        var genericPage = connection.FormatError500Page(exception);
        Assert.Contains("An internal server error occurred.", genericPage);
        Assert.DoesNotContain("secret", genericPage, StringComparison.Ordinal);

        server.ExposeExceptionDetails = true;
        var detailedPage = connection.FormatError500Page(exception);
        Assert.Contains("&lt;script&gt;secret&lt;/script&gt;", detailedPage);
        Assert.DoesNotContain("<script>", detailedPage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SessionLookup_RestoresRefreshesAndStopsReturningDestroyedSessions()
    {
        var server = new HttpServer();
        var session = server.CreateSession("known-session", 0);
        var initialAction = session.LastAction;
        var request = new HttpRequestPacket { Cookies = new StringKeyList() };
        request.Cookies["SID"] = session.Id;
        var connection = new HttpConnection
        {
            Server = server,
            Request = request
        };

        Assert.True(server.TryGetSession(session.Id, out var found));
        Assert.Same(session, found);

        connection.RestoreSessionFromRequest();
        Assert.Same(session, connection.Session);
        Assert.True(session.LastAction >= initialAction);

        session.Destroy();
        Assert.False(server.TryGetSession(session.Id, out _));
        connection.RestoreSessionFromRequest();
        Assert.Null(connection.Session);
    }

    private static HttpRequestPacket ValidHandshake()
    {
        var headers = new StringKeyList();
        headers["Connection"] = "keep-alive, Upgrade";
        headers["Upgrade"] = "websocket";
        headers["Sec-WebSocket-Version"] = "13";
        headers["Sec-WebSocket-Key"] = Convert.ToBase64String(new byte[16]);
        return new HttpRequestPacket
        {
            Method = Esiur.Net.Packets.Http.HttpMethod.GET,
            Version = "HTTP/1.1",
            Headers = headers,
            URL = "/"
        };
    }

    private static (HttpConnection Connection, TestSocket Socket, CaptureFilter Filter)
        CreateWebSocketConnection(HttpServer? server = null)
    {
        var result = CreateHttpConnection(server);
        result.Connection.WSMode = true;
        return result;
    }

    private static (HttpConnection Connection, TestSocket Socket, CaptureFilter Filter)
        CreateHttpConnection(HttpServer? server = null)
    {
        server ??= new HttpServer();
        var filter = new CaptureFilter();
        SetFilters(server, filter);

        var socket = new TestSocket();
        var connection = new HttpConnection
        {
            Server = server
        };
        connection.Assign(socket);
        return (connection, socket, filter);
    }

    private static void SetFilters(HttpServer server, params HttpFilter[] filters)
        => typeof(HttpServer)
            .GetField("filters", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(server, filters);

    private static void Receive(
        HttpConnection connection,
        TestSocket socket,
        byte[] message)
    {
        var buffer = new NetworkBuffer();
        buffer.Write(message);
        connection.NetworkReceive(socket, buffer);
    }

    private static byte[] Frame(
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

    private static byte[] Concat(params byte[][] messages)
    {
        var length = messages.Sum(message => message.Length);
        var result = new byte[length];
        var offset = 0;
        foreach (var message in messages)
        {
            Buffer.BlockCopy(message, 0, result, offset, message.Length);
            offset += message.Length;
        }

        return result;
    }

    private static WebsocketPacket ParseServerFrame(byte[] data)
    {
        var packet = new WebsocketPacket { ExpectedMask = false };
        Assert.Equal(data.Length, packet.Parse(data, 0, (uint)data.Length));
        return packet;
    }

    private static void AssertCloseCode(TestSocket socket, int expected)
    {
        var close = ParseServerFrame(Assert.Single(socket.Sent));
        Assert.Equal(WebsocketPacket.WSOpcode.ConnectionClose, close.Opcode);
        Assert.Equal(expected, close.Message[0] << 8 | close.Message[1]);
    }

    private sealed class CaptureFilter : HttpFilter
    {
        public List<(WebsocketPacket.WSOpcode Opcode, byte[] Payload)> Messages { get; } = new();
        public int ExecutionCount { get; private set; }

        public override AsyncReply<bool> Execute(HttpConnection sender)
        {
            ExecutionCount++;
            var packet = sender.WSRequest;
            if (packet != null)
                Messages.Add((packet.Opcode, packet.Message.ToArray()));
            return new AsyncReply<bool>(true);
        }

        public override AsyncReply<bool> Handle(
            ResourceOperation operation,
            IResourceContext? context = null) => new(true);
    }

    private sealed class TestSocket : ISocket
    {
        private readonly AsyncReply<bool>? sendReply;

        public TestSocket(AsyncReply<bool>? sendReply = null)
            => this.sendReply = sendReply;

        public event DestroyedEvent? OnDestroy;
        public SocketState State { get; private set; } = SocketState.Established;
        public INetworkReceiver<ISocket> Receiver { get; set; } = null!;
        public IPEndPoint RemoteEndPoint { get; } = new(IPAddress.Loopback, 12345);
        public IPEndPoint LocalEndPoint { get; } = new(IPAddress.Loopback, 54321);
        public List<byte[]> Sent { get; } = new();

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
            return sendReply ?? new AsyncReply<bool>(true);
        }

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

        public AsyncReply<bool> Connect(string hostname, ushort port) => new(false);
        public bool Begin() => true;
        public AsyncReply<bool> BeginAsync() => new(true);
        public AsyncReply<ISocket> AcceptAsync() => new((ISocket)null!);
        public ISocket Accept() => null!;
        public void Hold() { }
        public void Unhold() { }
    }
}
