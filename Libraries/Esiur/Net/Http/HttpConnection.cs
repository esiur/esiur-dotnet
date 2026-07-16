/*
 
Copyright (c) 2017 Ahmed Kh. Zamil

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/

using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Net;
using System.Collections;
using System.Collections.Generic;
using Esiur.Net.Sockets;
using Esiur.Data;
using Esiur.Misc;
using System.Security.Cryptography;
using Esiur.Core;
using Esiur.Net.Packets.WebSocket;
using Esiur.Net.Packets.Http;

namespace Esiur.Net.Http;
public class HttpConnection : NetworkConnection
{
    const long InvalidPacket = long.MinValue;
    const string WebSocketMagicString = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
    const string GenericInternalServerError = "An internal server error occurred.";

    MemoryStream websocketFragmentBuffer = new MemoryStream();
    WebsocketPacket.WSOpcode? websocketFragmentOpcode;
    ulong websocketFragmentLength;
    bool websocketCloseSent;
    uint parsedHttpPacketLength;

    public bool WSMode { get; internal set; }
    public HttpServer Server { get; internal set; }

    public WebsocketPacket WSRequest { get; set; }
    public string WebSocketSubprotocol { get; private set; }
    public HttpRequestPacket Request { get; set; }
    public HttpResponsePacket Response { get; } = new HttpResponsePacket();

    HttpSession session;

    public HttpSession Session => session;

    public KeyList<string, object> Variables { get; } = new KeyList<string, object>();



    internal long Parse(byte[] data)
    {
        parsedHttpPacketLength = 0;
        try
        {
            if (WSMode)
            {
                var ws = new WebsocketPacket
                {
                    ExpectedMask = true,
                    MaximumPayloadLength = Server?.MaximumWebSocketMessageLength
                        ?? WebsocketPacket.DefaultMaximumPayloadLength
                };
                var packetSize = ws.Parse(data, 0, (uint)data.Length);

                if (packetSize > 0)
                {
                    WSRequest = ws;
                    return 0;
                }

                return packetSize == 0 ? InvalidPacket : packetSize;
            }
            else
            {
                var request = new HttpRequestPacket();
                if (Server != null)
                {
                    request.MaximumContentLength = Server.MaxPost;
                    request.MaximumHeaderLength = Server.MaximumHeaderLength;
                    request.MaximumHeaderCount = Server.MaximumHeaderCount;
                    request.MaximumFormFields = Server.MaximumFormFields;
                    request.MaximumFormKeyLength = Server.MaximumFormKeyLength;
                    request.MaximumFormValueLength = Server.MaximumFormValueLength;
                    request.MaximumMultipartPartLength = Server.MaximumMultipartPartLength;
                }

                var packetSize = request.Parse(data, 0, (uint)data.Length);
                if (packetSize > 0)
                {
                    Request = request;
                    parsedHttpPacketLength = (uint)packetSize;
                    return 0;
                }

                return packetSize == 0 ? InvalidPacket : packetSize;
            }
        }
        catch (Exception exception) when (
            exception is InvalidDataException ||
            exception is ParserLimitException ||
            exception is ArgumentException)
        {
            Global.Log(exception);
            return InvalidPacket;
        }
    }


    public void Flush()
    {
        // close the connection
        if (!string.Equals(
                Request?.Headers?["connection"],
                "keep-alive",
                StringComparison.OrdinalIgnoreCase) && IsConnected)
            Close();
    }

    public bool Upgrade()
    {
        var ok = Upgrade(
            Request,
            Response,
            Server?.WebSocketSubprotocols,
            out var selectedSubprotocol);

        if (ok)
        {
            WebSocketSubprotocol = selectedSubprotocol;
            websocketCloseSent = false;
            ResetWebSocketFragment();
            WSMode = true;
            Send();
        }

        return ok;
    }

    public static bool Upgrade(HttpRequestPacket request, HttpResponsePacket response)
    {
        return Upgrade(request, response, null, out _);
    }

    /// <summary>
    /// Validates a WebSocket handshake and selects at most one mutually supported
    /// subprotocol. Subprotocol names are case-sensitive as required by RFC 6455.
    /// </summary>
    public static bool Upgrade(
        HttpRequestPacket request,
        HttpResponsePacket response,
        IEnumerable<string> supportedSubprotocols,
        out string selectedSubprotocol)
    {
        selectedSubprotocol = null;
        response?.Headers.RemoveAll("Sec-WebSocket-Protocol");

        if (response == null ||
            !TryValidateWebSocketRequest(request, out var requestedSubprotocols))
            return false;

        selectedSubprotocol = SelectSubprotocol(
            requestedSubprotocols,
            supportedSubprotocols);

        var challenge = request.Headers["Sec-WebSocket-Key"] + WebSocketMagicString;
        byte[] sha1Hash;
        using (var sha = SHA1.Create())
            sha1Hash = sha.ComputeHash(Encoding.ASCII.GetBytes(challenge));

        response.Headers["Upgrade"] = "websocket";
        response.Headers["Connection"] = "Upgrade";
        response.Headers["Sec-WebSocket-Accept"] = Convert.ToBase64String(sha1Hash);
        if (selectedSubprotocol != null)
            response.Headers["Sec-WebSocket-Protocol"] = selectedSubprotocol;

        response.Number = HttpResponseCode.Switching;
        response.Text = "Switching Protocols";
        return true;
    }

    public HttpServer Parent
    {
        get
        {
            return Server;
        }
    }

    public void Send(WebsocketPacket packet)
    {
        if (packet == null)
            return;

        // This class is always the server side of the built-in WebSocket path.
        // Recompose even prebuilt packets so caller-supplied masked data cannot be sent.
        packet.Mask = false;
        packet.MaximumPayloadLength = IsControlOpcode(packet.Opcode)
            ? 125
            : Server?.MaximumWebSocketMessageLength
                ?? WebsocketPacket.DefaultMaximumPayloadLength;
        if (packet.Compose())
        {
            if (packet.Opcode == WebsocketPacket.WSOpcode.ConnectionClose)
                websocketCloseSent = true;
            base.Send(packet.Data);
        }
    }

    public override void Send(string data)
    {
        Response.Message = Encoding.UTF8.GetBytes(data);
        Send();
    }

    public override void Send(byte[] msg, int offset, int length)
    {
        Response.Message = DC.Clip(msg, (uint)offset, (uint)length);
        Send();
    }

    public override void Send(byte[] message)
    {
        Response.Message = message;
        Send();
    }

    public void Send(HttpComposeOption Options = HttpComposeOption.AllCalculateLength)
    {
        if (Response.Handled)
            return;


        try
        {
            Response.Compose(Options);
            base.Send(Response.Data);

            // Refresh the current session
            if (session != null)
                session.Refresh();

        }
        catch
        {
            try
            {
                Close();
            }
            finally { }
        }
        finally
        {

        }
    }


    public void CreateNewSession()
    {
        if (session == null)
        {
            // Create a new one
            session = Server.CreateSession(Global.GenerateCode(12), 60 * 20);

            HttpCookie cookie = new HttpCookie("SID", session.Id);
            cookie.Expires = DateTime.MaxValue;
            cookie.Path = "/";
            cookie.HttpOnly = true;
            cookie.Secure = Server.SSL;
            cookie.SameSite = HttpCookieSameSite.Lax;

            Response.Cookies.Add(cookie);
        }
    }


    public bool IsWebsocketRequest()
    {
        return IsWebsocketRequest(this.Request);
    }

    public static bool IsWebsocketRequest(HttpRequestPacket request)
    {
        return TryValidateWebSocketRequest(request, out _);
    }

    private static bool TryValidateWebSocketRequest(
        HttpRequestPacket request,
        out string[] requestedSubprotocols)
    {
        requestedSubprotocols = Array.Empty<string>();
        if (request == null ||
            request.Headers == null ||
            request.Method != Packets.Http.HttpMethod.GET ||
            (request.RawMethod != null &&
             !string.Equals(request.RawMethod, "GET", StringComparison.Ordinal)) ||
            !string.Equals(request.Version, "HTTP/1.1", StringComparison.Ordinal))
            return false;

        if (!TryParseTokenList(request.Headers["Connection"], out var connectionTokens) ||
            !ContainsToken(connectionTokens, "Upgrade", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!TryParseUpgradeList(request.Headers["Upgrade"], out var hasWebSocket) ||
            !hasWebSocket)
            return false;

        if (!string.Equals(
                request.Headers["Sec-WebSocket-Version"],
                "13",
                StringComparison.Ordinal))
            return false;

        var key = request.Headers["Sec-WebSocket-Key"];
        if (!IsCanonicalWebSocketKey(key))
            return false;

        var protocols = request.Headers["Sec-WebSocket-Protocol"];
        if (protocols != null && !TryParseTokenList(protocols, out requestedSubprotocols))
            return false;

        return true;
    }

    private static bool IsWebSocketUpgradeAttempt(HttpRequestPacket request)
    {
        var upgrade = request?.Headers?["Upgrade"];
        if (string.IsNullOrEmpty(upgrade))
            return false;

        for (var index = 0; index < upgrade.Length;)
        {
            while (index < upgrade.Length && !IsHttpTokenCharacter(upgrade[index]))
                index++;
            var start = index;
            while (index < upgrade.Length && IsHttpTokenCharacter(upgrade[index]))
                index++;

            if (index > start &&
                string.Equals(
                    upgrade.Substring(start, index - start),
                    "websocket",
                    StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool IsCanonicalWebSocketKey(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;

        try
        {
            var decoded = Convert.FromBase64String(key);
            return decoded.Length == 16 &&
                   string.Equals(
                       Convert.ToBase64String(decoded),
                       key,
                       StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool TryParseTokenList(string value, out string[] tokens)
    {
        tokens = Array.Empty<string>();
        if (string.IsNullOrEmpty(value))
            return false;

        var parts = value.Split(',');
        for (var i = 0; i < parts.Length; i++)
        {
            parts[i] = parts[i].Trim();
            if (!IsHttpToken(parts[i]))
                return false;
        }

        tokens = parts;
        return true;
    }

    private static bool TryParseUpgradeList(string value, out bool hasWebSocket)
    {
        hasWebSocket = false;
        if (string.IsNullOrEmpty(value))
            return false;

        foreach (var entry in value.Split(','))
        {
            var protocol = entry.Trim();
            var slash = protocol.IndexOf('/');
            if (slash < 0)
            {
                if (!IsHttpToken(protocol))
                    return false;

                if (string.Equals(protocol, "websocket", StringComparison.OrdinalIgnoreCase))
                    hasWebSocket = true;
            }
            else
            {
                if (slash == 0 ||
                    slash == protocol.Length - 1 ||
                    protocol.IndexOf('/', slash + 1) >= 0 ||
                    !IsHttpToken(protocol.Substring(0, slash)) ||
                    !IsHttpToken(protocol.Substring(slash + 1)))
                    return false;
            }
        }

        return true;
    }

    private static bool IsHttpToken(string value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        foreach (var character in value)
        {
            if (IsHttpTokenCharacter(character))
                continue;

            return false;
        }

        return true;
    }

    private static bool IsHttpTokenCharacter(char character)
        => (character >= 'a' && character <= 'z') ||
           (character >= 'A' && character <= 'Z') ||
           (character >= '0' && character <= '9') ||
           character == '!' || character == '#' || character == '$' ||
           character == '%' || character == '&' || character == '\'' ||
           character == '*' || character == '+' || character == '-' ||
           character == '.' || character == '^' || character == '_' ||
           character == '`' || character == '|' || character == '~';

    private static bool ContainsToken(
        string[] tokens,
        string expected,
        StringComparison comparison)
    {
        foreach (var token in tokens)
            if (string.Equals(token, expected, comparison))
                return true;

        return false;
    }

    private static string SelectSubprotocol(
        string[] requestedSubprotocols,
        IEnumerable<string> supportedSubprotocols)
    {
        if (supportedSubprotocols == null)
            return null;

        foreach (var supported in supportedSubprotocols)
        {
            if (!IsHttpToken(supported))
                continue;

            if (ContainsToken(
                    requestedSubprotocols,
                    supported,
                    StringComparison.Ordinal))
                return supported;
        }

        return null;
    }

    protected override void DataReceived(NetworkBuffer data)
    {
        if (WSMode)
        {
            ProcessWebSocketData(data);
            return;
        }

        byte[] msg = data.Read();
        if (msg == null)
            return;

        var BL = Parse(msg);

        if (BL == InvalidPacket)
        {
            Close();
            return;
        }
        else if (BL == 0)
        {
            if (Request == null || Request.Method == Packets.Http.HttpMethod.UNKNOWN)
            {
                Close();
                return;
            }
            if (Request.URL == "")
            {
                Close();
                return;
            }
        }
        else if (BL == -1)
        {
            data.HoldForNextWrite(msg);
            return;
        }
        else if (BL < 0)
        {
            var requiredLength = (ulong)msg.Length + (ulong)(-BL);
            if (requiredLength > uint.MaxValue)
            {
                Close();
                return;
            }

            data.HoldFor(msg, (uint)requiredLength);
            return;
        }

        RestoreSessionFromRequest();

        if (!WSMode && IsWebSocketUpgradeAttempt(Request))
        {
            if (!IsWebsocketRequest(Request) || !Upgrade())
            {
                Response.Number = HttpResponseCode.BadRequest;
                Response.Text = "Bad Request";
                Response.Headers["Connection"] = "close";
                Send("Invalid WebSocket handshake.");
                Close();
                return;
            }
        }

        try
        {
            if (Server == null || !Server.Execute(this))
            {
                if (WSMode)
                {
                    FailWebSocket(1008, "No HTTP filter accepted the WebSocket connection.");
                    return;
                }

                Response.Number = HttpResponseCode.InternalServerError;
                Send("Bad Request");
                Close();
            }
        }
        catch (Exception ex)
        {
            if (ex.Message != "Thread was being aborted.")
            {

                Global.Log("HTTPServer", LogType.Error, ex.ToString());

                if (WSMode)
                {
                    FailWebSocket(1011, "A WebSocket filter failed.");
                    return;
                }

                Response.Number = HttpResponseCode.InternalServerError;
                Response.Headers["Content-Type"] = "text/html; charset=utf-8";
                Send(FormatError500Page(ex));
            }

        }

        if (WSMode &&
            IsConnected &&
            parsedHttpPacketLength > 0 &&
            parsedHttpPacketLength < msg.Length)
        {
            data.Write(
                msg,
                parsedHttpPacketLength,
                (uint)msg.Length - parsedHttpPacketLength);
            ProcessWebSocketData(data);
        }
    }

    internal void RestoreSessionFromRequest()
    {
        session = null;
        var sessionId = Request?.Cookies?["SID"];
        if (Server?.TryGetSession(sessionId, out var restored) != true)
            return;

        session = restored;
        session.Refresh();
    }

    private void ProcessWebSocketData(NetworkBuffer data)
    {
        var message = data.Read();
        if (message == null)
            return;

        var offset = 0u;
        var ends = (uint)message.Length;

        while (offset < ends && IsConnected)
        {
            var packet = new WebsocketPacket
            {
                ExpectedMask = true,
                MaximumPayloadLength = GetIncomingFrameLimit(message[offset])
            };

            long packetLength;
            try
            {
                packetLength = packet.Parse(message, offset, ends);
            }
            catch (ParserLimitException exception)
            {
                FailWebSocket(1009, exception.Message);
                return;
            }
            catch (Exception exception) when (
                exception is InvalidDataException ||
                exception is ArgumentException)
            {
                FailWebSocket(
                    IsInvalidUtf8(exception) ? (ushort)1007 : (ushort)1002,
                    exception.Message);
                return;
            }

            if (packetLength < 0)
            {
                var remaining = ends - offset;
                var required = (ulong)remaining + (ulong)(-packetLength);
                if (required > int.MaxValue)
                {
                    FailWebSocket(1009, "The incomplete WebSocket frame is too large to buffer.");
                    return;
                }

                data.HoldFor(message, offset, remaining, (uint)required);
                return;
            }

            if (packetLength == 0 || (ulong)packetLength > ends - offset)
            {
                FailWebSocket(1002, "The WebSocket frame parser returned an invalid length.");
                return;
            }

            offset += (uint)packetLength;
            if (!ProcessWebSocketFrame(packet))
                return;
        }
    }

    private ulong GetIncomingFrameLimit(byte firstHeaderByte)
    {
        var opcode = (WebsocketPacket.WSOpcode)(firstHeaderByte & 0x0F);
        if (IsControlOpcode(opcode))
            return 125;

        return Server?.MaximumWebSocketMessageLength
            ?? WebsocketPacket.DefaultMaximumPayloadLength;
    }

    private static bool IsControlOpcode(WebsocketPacket.WSOpcode opcode)
        => opcode == WebsocketPacket.WSOpcode.ConnectionClose ||
           opcode == WebsocketPacket.WSOpcode.Ping ||
           opcode == WebsocketPacket.WSOpcode.Pong;

    private bool ProcessWebSocketFrame(WebsocketPacket packet)
    {
        switch (packet.Opcode)
        {
            case WebsocketPacket.WSOpcode.Ping:
                SendWebSocketFrame(WebsocketPacket.WSOpcode.Pong, packet.Message);
                return IsConnected;

            case WebsocketPacket.WSOpcode.Pong:
                return true;

            case WebsocketPacket.WSOpcode.ConnectionClose:
                ResetWebSocketFragment();
                if (!websocketCloseSent)
                {
                    websocketCloseSent = true;
                    SendWebSocketCloseAndClose(packet.Message);
                }
                else
                {
                    Close();
                }
                return false;

            case WebsocketPacket.WSOpcode.TextFrame:
            case WebsocketPacket.WSOpcode.BinaryFrame:
                if (websocketFragmentOpcode.HasValue)
                {
                    FailWebSocket(
                        1002,
                        "A new WebSocket data frame arrived before the fragmented message completed.");
                    return false;
                }

                if (packet.FIN)
                    return DeliverWebSocketMessage(packet);

                websocketFragmentOpcode = packet.Opcode;
                websocketFragmentLength = 0;
                websocketFragmentBuffer.SetLength(0);
                return AppendWebSocketFragment(packet.Message);

            case WebsocketPacket.WSOpcode.ContinuationFrame:
                if (!websocketFragmentOpcode.HasValue)
                {
                    FailWebSocket(
                        1002,
                        "A WebSocket continuation frame arrived without an active fragmented message.");
                    return false;
                }

                if (!AppendWebSocketFragment(packet.Message))
                    return false;

                if (!packet.FIN)
                    return true;

                var opcode = websocketFragmentOpcode.Value;
                var completeMessage = websocketFragmentBuffer.ToArray();
                ResetWebSocketFragment();

                try
                {
                    if (opcode == WebsocketPacket.WSOpcode.TextFrame)
                        WebsocketPacket.ValidateTextPayload(completeMessage);
                }
                catch (InvalidDataException exception)
                {
                    FailWebSocket(1007, exception.Message);
                    return false;
                }

                return DeliverWebSocketMessage(new WebsocketPacket
                {
                    FIN = true,
                    Opcode = opcode,
                    Mask = true,
                    Message = completeMessage,
                    PayloadLength = completeMessage.LongLength
                });

            default:
                FailWebSocket(1002, "Unsupported WebSocket opcode.");
                return false;
        }
    }

    private bool AppendWebSocketFragment(byte[] payload)
    {
        payload ??= Array.Empty<byte>();
        var payloadLength = (ulong)payload.LongLength;
        if (payloadLength > ulong.MaxValue - websocketFragmentLength)
        {
            FailWebSocket(1009, "The fragmented WebSocket message length overflowed.");
            return false;
        }

        var nextLength = websocketFragmentLength + payloadLength;
        var maximumLength = Server?.MaximumWebSocketMessageLength
            ?? WebsocketPacket.DefaultMaximumPayloadLength;
        if (nextLength > int.MaxValue ||
            (maximumLength > 0 && nextLength > maximumLength))
        {
            FailWebSocket(
                1009,
                $"The fragmented WebSocket message exceeds the {maximumLength}-byte limit.");
            return false;
        }

        if (payload.Length > 0)
            websocketFragmentBuffer.Write(payload, 0, payload.Length);
        websocketFragmentLength = nextLength;
        return true;
    }

    private bool DeliverWebSocketMessage(WebsocketPacket packet)
    {
        WSRequest = packet;
        try
        {
            if (Server == null)
            {
                FailWebSocket(1011, "The WebSocket connection is no longer assigned to a server.");
                return false;
            }

            if (!Server.Execute(this))
            {
                FailWebSocket(1008, "No HTTP filter accepted the WebSocket message.");
                return false;
            }

            return IsConnected;
        }
        catch (Exception exception)
        {
            Global.Log("HTTPServer", LogType.Error, exception.ToString());
            FailWebSocket(1011, "A WebSocket filter failed.");
            return false;
        }
    }

    private void SendWebSocketFrame(
        WebsocketPacket.WSOpcode opcode,
        byte[] payload)
    {
        var packet = new WebsocketPacket
        {
            FIN = true,
            Mask = false,
            Opcode = opcode,
            Message = payload ?? Array.Empty<byte>(),
            MaximumPayloadLength = 0
        };

        if (packet.Compose())
            base.Send(packet.Data);
    }

    private void FailWebSocket(ushort closeCode, string message)
    {
        Global.Log("HTTPServer", LogType.Warning, message);
        ResetWebSocketFragment();

        if (IsConnected && !websocketCloseSent)
        {
            websocketCloseSent = true;
            SendWebSocketCloseAndClose(
                new[] { (byte)(closeCode >> 8), (byte)closeCode });
            return;
        }

        Close();
    }

    private void SendWebSocketCloseAndClose(byte[] payload)
    {
        var packet = new WebsocketPacket
        {
            FIN = true,
            Mask = false,
            Opcode = WebsocketPacket.WSOpcode.ConnectionClose,
            Message = payload ?? Array.Empty<byte>(),
            MaximumPayloadLength = 125
        };

        if (!packet.Compose())
        {
            Close();
            return;
        }

        base.SendAsync(packet.Data, 0, packet.Data.Length)
            .Then(_ => Close())
            .Error(_ => Close());
    }

    private void ResetWebSocketFragment()
    {
        websocketFragmentOpcode = null;
        websocketFragmentLength = 0;
        if (websocketFragmentBuffer.Capacity > 64 * 1024)
        {
            websocketFragmentBuffer.Dispose();
            websocketFragmentBuffer = new MemoryStream();
        }
        else
        {
            websocketFragmentBuffer.SetLength(0);
        }
    }

    private static bool IsInvalidUtf8(Exception exception)
    {
        for (var current = exception; current != null; current = current.InnerException)
            if (current is DecoderFallbackException)
                return true;

        return false;
    }

    internal static string FormatError500Page(string msg)
    {
        var encodedMessage = WebUtility.HtmlEncode(msg ?? string.Empty);
        return "<html><head><title>500 Internal Server Error</title></head><br>\r\n"
                 + "<body><br>\r\n"
                 + "<b>500</b> Internal Server Error<br>" + encodedMessage + "\r\n"
                 + "</body><br>\r\n"
                 + "</html><br>\r\n";
    }

    internal string FormatError500Page(Exception exception)
    {
        var message = Server?.ExposeExceptionDetails == true
            ? exception?.Message
            : GenericInternalServerError;
        return FormatError500Page(message);
    }

    public async AsyncReply<bool> SendFile(string filename)
    {
        if (Response.Handled == true)
            return false;


        try
        {

            //HTTP/1.1 200 OK
            //Server: Microsoft-IIS/5.0
            //Content-Location: http://127.0.0.1/index.html
            //Date: Wed, 10 Dec 2003 19:10:25 GMT
            //Content-Type: text/html
            //Accept-Ranges: bytes
            //Last-Modified: Mon, 22 Sep 2003 22:36:56 GMT
            //Content-Length: 1957

            if (!File.Exists(filename))
            {
                Response.Number = HttpResponseCode.NotFound;
                Send("File Not Found");
                return true;
            }


            var fileEditTime = File.GetLastWriteTime(filename).ToUniversalTime();
            if (Request.Headers.ContainsKey("if-modified-since"))
            {
                try
                {
                    var ims = DateTime.Parse(Request.Headers["if-modified-since"]);
                    if ((fileEditTime - ims).TotalSeconds < 2)
                    {
                        Response.Number = HttpResponseCode.NotModified;
                        Response.Headers.Clear();
                        //Response.Text = "Not Modified";
                        Send(HttpComposeOption.SpecifiedHeadersOnly);
                        return true;
                    }
                }
                catch
                {
                    return false;
                }
            }



            Response.Number = HttpResponseCode.OK;
            // Fri, 30 Oct 2007 14:19:41 GMT
            Response.Headers["Last-Modified"] = fileEditTime.ToString("ddd, dd MMM yyyy HH:mm:ss");
            FileInfo fi = new FileInfo(filename);
            Response.Headers["Content-Length"] = fi.Length.ToString();
            Send(HttpComposeOption.SpecifiedHeadersOnly);

            //var fd = File.ReadAllBytes(filename);

            //base.Send(fd);


            using (var fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
            {

                var buffer = new byte[60000];


                while (true)
                {
                    var n = await fs.ReadAsync(buffer, 0, buffer.Length)
                        .ConfigureAwait(false);

                    if (n <= 0)
                        break;

                    //Thread.Sleep(50);
                    await base.SendAsync(buffer, 0, n);

                }
            }

            return true;

        }
        catch
        {
            try
            {
                Close();
            }
            finally
            {

            }

            return false;
        }
    }

    protected override void Connected()
    {
        // do nothing
    }

    protected override void Disconnected()
    {
        ResetWebSocketFragment();
    }
}
