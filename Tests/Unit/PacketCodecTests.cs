using Esiur.Data;
using Esiur.Net.Packets;
using Esiur.Net.Packets.Http;
using Esiur.Net.Packets.WebSocket;
using Esiur.Resource;
using System.Text;

namespace Esiur.Tests.Unit;

public class PacketCodecTests
{
    [Fact]
    public void EpParser_ValidatesSliceBounds()
    {
        var packet = new EpAuthPacket(new Warehouse());

        Assert.Throws<ArgumentOutOfRangeException>(() => packet.Parse(Array.Empty<byte>(), 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => packet.Parse(Array.Empty<byte>(), 0, 1));
    }

    [Fact]
    public void WebSocket_ComposesAndParsesUnmaskedFrame()
    {
        var packet = new WebsocketPacket
        {
            FIN = true,
            Opcode = WebsocketPacket.WSOpcode.BinaryFrame,
            Message = new byte[] { 1, 2, 3 }
        };

        Assert.True(packet.Compose());
        Assert.Equal(new byte[] { 0x82, 0x03, 1, 2, 3 }, packet.Data);

        var parsed = new WebsocketPacket();
        Assert.Equal(packet.Data.Length, parsed.Parse(packet.Data, 0, (uint)packet.Data.Length));
        Assert.Equal(packet.Message, parsed.Message);
    }

    [Fact]
    public void WebSocket_MasksOutboundPayloadAndRestoresItWhenParsed()
    {
        var message = Encoding.UTF8.GetBytes("hello");
        var packet = new WebsocketPacket
        {
            FIN = true,
            Mask = true,
            MaskKey = new byte[] { 1, 2, 3, 4 },
            Opcode = WebsocketPacket.WSOpcode.TextFrame,
            Message = message
        };

        packet.Compose();

        Assert.Equal(0x81, packet.Data[0]);
        Assert.Equal(0x85, packet.Data[1]);
        Assert.Equal(packet.MaskKey, packet.Data.Skip(2).Take(4));
        Assert.NotEqual(message, packet.Data.Skip(6).ToArray());

        var parsed = new WebsocketPacket();
        Assert.Equal(packet.Data.Length, parsed.Parse(packet.Data, 0, (uint)packet.Data.Length));
        Assert.Equal(message, parsed.Message);
    }

    [Fact]
    public void WebSocket_RejectsOversizedDeclarationBeforePayloadArrives()
    {
        var packet = new WebsocketPacket { MaximumPayloadLength = 1_024 };
        var header = new byte[] { 0x82, 126, 0x04, 0x01 };

        Assert.Throws<ParserLimitException>(() => packet.Parse(header, 0, (uint)header.Length));
    }

    [Fact]
    public void HttpRequest_ParsesOffsetBodyQueryCookiesAndForm()
    {
        const string request =
            "POST /submit?a=1 HTTP/1.1\r\n" +
            "Host: example.test\r\n" +
            "Cookie: sid=abc; flag\r\n" +
            "Content-Type: application/x-www-form-urlencoded\r\n" +
            "Content-Length: 7\r\n\r\n" +
            "x=hello";
        var packetBytes = Encoding.ASCII.GetBytes(request);
        var data = new byte[packetBytes.Length + 2];
        Buffer.BlockCopy(packetBytes, 0, data, 2, packetBytes.Length);

        var packet = new HttpRequestPacket();
        var consumed = packet.Parse(data, 2, (uint)data.Length);

        Assert.Equal(packetBytes.Length, consumed);
        Assert.Equal(Esiur.Net.Packets.Http.HttpMethod.POST, packet.Method);
        Assert.Equal("/submit", packet.Filename);
        Assert.Equal("1", packet.Query["A"]);
        Assert.Equal("abc", packet.Cookies["SID"]);
        Assert.Equal(string.Empty, packet.Cookies["flag"]);
        Assert.Equal("hello", packet.PostForms["x"]);
    }

    [Fact]
    public void HttpRequest_RejectsOversizedContentBeforeBuffering()
    {
        var data = Encoding.ASCII.GetBytes(
            "POST / HTTP/1.1\r\nContent-Length: 5\r\n\r\n");
        var packet = new HttpRequestPacket { MaximumContentLength = 4 };

        Assert.Throws<ParserLimitException>(() => packet.Parse(data, 0, (uint)data.Length));
    }

    [Fact]
    public void HttpResponse_ParsesBodyFromHeaderBoundaryAndUsesNegativeMissingCount()
    {
        var complete = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nContent-Length: 5\r\nSet-Cookie: sid=abc; Path=/; HttpOnly\r\n\r\nhello");
        var prefixed = new byte[complete.Length + 2];
        Buffer.BlockCopy(complete, 0, prefixed, 2, complete.Length);

        var packet = new HttpResponsePacket();
        Assert.Equal(complete.Length, packet.Parse(prefixed, 2, (uint)prefixed.Length));
        Assert.Equal("hello", Encoding.ASCII.GetString(packet.Message));
        Assert.Single(packet.Cookies);
        Assert.True(packet.Cookies[0].HttpOnly);

        var incomplete = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhe");
        Assert.Equal(-3, new HttpResponsePacket().Parse(incomplete, 0, (uint)incomplete.Length));
    }

    [Fact]
    public void HttpResponse_ComposeCalculatesLengthAndAppendsBody()
    {
        var packet = new HttpResponsePacket
        {
            Text = "OK",
            Message = Encoding.ASCII.GetBytes("hello")
        };

        packet.Compose(HttpComposeOption.AllCalculateLength);
        var response = Encoding.ASCII.GetString(packet.Data);

        Assert.Contains("Content-Length: 5\r\n", response, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("\r\n\r\nhello", response, StringComparison.Ordinal);
    }

    [Fact]
    public void StringKeyList_LookupsRemainCaseInsensitiveWithoutDuplicates()
    {
        var values = new StringKeyList();
        values.Add("Content-Type", "text/plain");
        values["CONTENT-TYPE"] = "application/json";

        Assert.Equal(1, values.Count);
        Assert.Equal("application/json", values["content-type"]);
        Assert.True(values.ContainsKey("Content-Type"));
    }
}
