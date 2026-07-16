using System.Collections;
using System.Reflection;
using System.Text;
using Esiur.Data;
using Esiur.Net.Http;
using Esiur.Net.Packets.Http;

namespace Esiur.Tests.Unit;

public class HttpHardeningTests
{
    [Theory]
    [InlineData("Content-Length: 1\r\nContent-Length: 1\r\n")]
    [InlineData("Content-Length: 1\r\nContent-Length: 2\r\n")]
    public void Request_RejectsDuplicateContentLength(string framingHeaders)
    {
        var data = RequestBytes("POST", framingHeaders, "x");

        Assert.Throws<InvalidDataException>(() =>
            new HttpRequestPacket().Parse(data, 0, (uint)data.Length));
    }

    [Theory]
    [InlineData("Transfer-Encoding: chunked\r\n")]
    [InlineData("Transfer-Encoding: identity\r\nContent-Length: 1\r\n")]
    public void Request_RejectsUnsupportedTransferEncoding(string framingHeaders)
    {
        var data = RequestBytes("POST", framingHeaders, "x");

        Assert.Throws<InvalidDataException>(() =>
            new HttpRequestPacket().Parse(data, 0, (uint)data.Length));
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("GET")]
    [InlineData("DELETE")]
    public void Request_ConsumesDeclaredBodiesForEveryMethod(string method)
    {
        var data = RequestBytes(
            method,
            "Content-Type: application/octet-stream\r\nContent-Length: 3\r\n",
            "abc");
        var packet = new HttpRequestPacket();

        Assert.Equal(data.Length, packet.Parse(data, 0, (uint)data.Length));
        Assert.Equal("abc", Encoding.ASCII.GetString(packet.Message));
    }

    [Fact]
    public void Request_AllowsPostWithoutABodyOrContentLength()
    {
        var data = RequestBytes("POST", string.Empty, string.Empty);
        var packet = new HttpRequestPacket();

        Assert.Equal(data.Length, packet.Parse(data, 0, (uint)data.Length));
        Assert.Empty(packet.PostForms);
    }

    [Fact]
    public void Request_EnforcesHeaderCountBeforeSplittingHeaders()
    {
        var data = RequestBytes("GET", "X-One: 1\r\nX-Two: 2\r\n", string.Empty);
        var packet = new HttpRequestPacket { MaximumHeaderCount = 1 };

        Assert.Throws<ParserLimitException>(() =>
            packet.Parse(data, 0, (uint)data.Length));
    }

    [Fact]
    public void UrlEncodedForm_EnforcesFieldKeyAndValueLimits()
    {
        AssertFormLimit("a=1&b=2", packet => packet.MaximumFormFields = 1);
        AssertFormLimit("long=1", packet => packet.MaximumFormKeyLength = 3);
        AssertFormLimit("a=long", packet => packet.MaximumFormValueLength = 3);
    }

    [Fact]
    public void UrlEncodedForm_CombinesUnkeyedFieldsWithinTheConfiguredBound()
    {
        const string body = "first&second&third";
        var data = RequestBytes(
            "POST",
            $"Content-Type: application/x-www-form-urlencoded\r\nContent-Length: {body.Length}\r\n",
            body);
        var packet = new HttpRequestPacket { MaximumFormValueLength = body.Length };

        Assert.Equal(data.Length, packet.Parse(data, 0, (uint)data.Length));
        Assert.Equal(body, packet.PostForms["unknown"]);

        packet = new HttpRequestPacket { MaximumFormValueLength = body.Length - 1 };
        Assert.Throws<ParserLimitException>(() =>
            packet.Parse(data, 0, (uint)data.Length));
    }

    [Fact]
    public void MultipartForm_EnforcesPartLength()
    {
        const string boundary = "test-boundary";
        const string body =
            "--test-boundary\r\n" +
            "Content-Disposition: form-data; name=\"value\"\r\n\r\n" +
            "payload\r\n" +
            "--test-boundary--\r\n";
        var data = RequestBytes(
            "POST",
            $"Content-Type: multipart/form-data; boundary=\"{boundary}\"\r\n" +
            $"Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\n",
            body);
        var packet = new HttpRequestPacket { MaximumMultipartPartLength = 8 };

        Assert.Throws<ParserLimitException>(() =>
            packet.Parse(data, 0, (uint)data.Length));
    }

    [Fact]
    public void MultipartForm_ParsesQuotedBoundaryIncrementally()
    {
        const string boundary = "test-boundary";
        const string body =
            "--test-boundary\r\n" +
            "Content-Disposition: form-data; name=\"first\"\r\n\r\n" +
            "one\r\n" +
            "--test-boundary\r\n" +
            "Content-Disposition: form-data; name=\"second\"\r\n\r\n" +
            "two\r\n" +
            "--test-boundary--\r\n";
        var data = RequestBytes(
            "POST",
            $"Content-Type: multipart/form-data; boundary=\"{boundary}\"\r\n" +
            $"Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\n",
            body);
        var packet = new HttpRequestPacket();

        Assert.Equal(data.Length, packet.Parse(data, 0, (uint)data.Length));
        Assert.Equal("one", packet.PostForms["first"]);
        Assert.Equal("two", packet.PostForms["second"]);
    }

    [Theory]
    [InlineData("Content-Length: 1\r\nContent-Length: 1\r\n")]
    [InlineData("Transfer-Encoding: chunked\r\n")]
    [InlineData("Transfer-Encoding: identity\r\nContent-Length: 1\r\n")]
    public void Response_RejectsAmbiguousOrUnsupportedFraming(string framingHeaders)
    {
        var data = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\n" + framingHeaders + "\r\nx");

        Assert.Throws<InvalidDataException>(() =>
            new HttpResponsePacket().Parse(data, 0, (uint)data.Length));
    }

    [Fact]
    public void Cookie_RoundTripsSecureAndSameSiteAttributes()
    {
        var cookie = new HttpCookie("sid", "value")
        {
            Path = "/",
            HttpOnly = true,
            Secure = true,
            SameSite = HttpCookieSameSite.Strict,
        };
        var serialized = cookie.ToString();
        Assert.Contains("; Secure", serialized, StringComparison.Ordinal);
        Assert.Contains("; SameSite=Strict", serialized, StringComparison.Ordinal);

        var data = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nContent-Length: 0\r\nSet-Cookie: " + serialized + "\r\n\r\n");
        var response = new HttpResponsePacket();

        Assert.Equal(data.Length, response.Parse(data, 0, (uint)data.Length));
        Assert.True(response.Cookies[0].Secure);
        Assert.Equal(HttpCookieSameSite.Strict, response.Cookies[0].SameSite);
    }

    [Fact]
    public void Response_ComposeUsesAnExplicitOutboundLimitOnly()
    {
        var response = new HttpResponsePacket
        {
            MaximumContentLength = 1,
            Message = new byte[] { 1, 2 },
        };

        Assert.True(response.Compose(HttpComposeOption.DataOnly));
        Assert.Equal(response.Message, response.Data);

        response.MaximumComposedContentLength = 1;
        Assert.Throws<ParserLimitException>(() =>
            response.Compose(HttpComposeOption.DataOnly));
    }

    [Fact]
    public async Task Session_TimerStartsAndServerRemovesExpiredSession()
    {
        var server = new HttpServer();
        var ended = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = server.CreateSession("expiring", 1);
        session.OnEnd += _ => ended.TrySetResult(true);

        await ended.Task.WaitAsync(TimeSpan.FromSeconds(3));

        var sessions = (IDictionary)typeof(HttpServer)
            .GetField("sessions", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(server)!;
        Assert.Empty(sessions);

        // Destruction remains safe after the expiry callback has already disposed the timer.
        session.Destroy();
    }

    [Fact]
    public void Session_WithoutATimerCanBeDestroyedIdempotently()
    {
        var session = new HttpSession();

        session.Destroy();
        session.Destroy();
    }

    [Fact]
    public void ExplicitlyDestroyedSession_IsRemovedFromServer()
    {
        var server = new HttpServer();
        var session = server.CreateSession("destroyed", 0);

        session.Destroy();

        var sessions = (IDictionary)typeof(HttpServer)
            .GetField("sessions", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(server)!;
        Assert.Empty(sessions);
    }

    [Fact]
    public void InternalServerErrorPage_HtmlEncodesExceptionText()
    {
        var page = HttpConnection.FormatError500Page("<script>alert('x')</script>&");

        Assert.DoesNotContain("<script>", page, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;script&gt;", page, StringComparison.Ordinal);
        Assert.Contains("&amp;", page, StringComparison.Ordinal);
    }

    private static void AssertFormLimit(string body, Action<HttpRequestPacket> configure)
    {
        var data = RequestBytes(
            "POST",
            $"Content-Type: application/x-www-form-urlencoded\r\nContent-Length: {body.Length}\r\n",
            body);
        var packet = new HttpRequestPacket();
        configure(packet);

        Assert.Throws<ParserLimitException>(() =>
            packet.Parse(data, 0, (uint)data.Length));
    }

    private static byte[] RequestBytes(string method, string headers, string body)
        => Encoding.ASCII.GetBytes($"{method} / HTTP/1.1\r\n{headers}\r\n{body}");
}
