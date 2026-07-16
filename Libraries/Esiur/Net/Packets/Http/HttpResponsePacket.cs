using Esiur.Data;
using Esiur.Misc;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Esiur.Net.Packets.Http;

public class HttpResponsePacket : Packet
{
    public StringKeyList Headers { get; } = new StringKeyList(true);
    public string Version { get; set; } = "HTTP/1.1";
    public byte[] Message;
    public HttpResponseCode Number { get; set; } = HttpResponseCode.OK;
    public string Text;
    public List<HttpCookie> Cookies { get; } = new List<HttpCookie>();
    public bool Handled;

    public uint MaximumHeaderLength { get; set; } = HttpPacketHelpers.DefaultMaximumHeaderLength;
    public uint MaximumContentLength { get; set; } = HttpPacketHelpers.DefaultMaximumContentLength;
    public int MaximumHeaderCount { get; set; } = HttpPacketHelpers.DefaultMaximumHeaderCount;

    /// <summary>
    /// Maximum response body accepted by <see cref="Compose(HttpComposeOption)"/>.
    /// Zero preserves the legacy behavior of allowing any body that fits in a managed array.
    /// </summary>
    public uint MaximumComposedContentLength { get; set; }

    public override string ToString()
        => $"HTTPResponsePacket\n\tVersion: {Version}" +
           $"\n\tMessage: {(Message == null ? "NULL" : Message.Length.ToString())}";

    private byte[] ComposeHeader(HttpComposeOption options)
    {
        if (options == HttpComposeOption.AllCalculateLength)
            Headers["Content-Length"] = Message?.Length.ToString(CultureInfo.InvariantCulture) ?? "0";

        var header = new StringBuilder(256);
        header.Append(Version)
              .Append(' ')
              .Append((int)Number)
              .Append(' ')
              .Append(Text ?? string.Empty)
              .Append("\r\nServer: Esiur ")
              .Append(Global.Version)
              .Append("\r\nDate: ")
              .Append(DateTime.UtcNow.ToString("r", CultureInfo.InvariantCulture))
              .Append("\r\n");

        foreach (var entry in Headers)
            header.Append(entry.Key).Append(": ").Append(entry.Value).Append("\r\n");
        foreach (var cookie in Cookies)
            header.Append("Set-Cookie: ").Append(cookie).Append("\r\n");

        header.Append("\r\n");
        return Encoding.ASCII.GetBytes(header.ToString());
    }

    public bool Compose(HttpComposeOption options)
    {
        var header = options == HttpComposeOption.DataOnly
            ? Array.Empty<byte>()
            : ComposeHeader(options);
        var body = options == HttpComposeOption.SpecifiedHeadersOnly || Message == null
            ? Array.Empty<byte>()
            : Message;

        if (MaximumComposedContentLength > 0 && body.LongLength > MaximumComposedContentLength)
            throw new ParserLimitException(
                $"HTTP content length of {body.LongLength} bytes exceeds the {MaximumComposedContentLength}-byte limit.");

        Data = new byte[checked(header.Length + body.Length)];
        if (header.Length > 0)
            Buffer.BlockCopy(header, 0, Data, 0, header.Length);
        if (body.Length > 0)
            Buffer.BlockCopy(body, 0, Data, header.Length, body.Length);

        return true;
    }

    public override bool Compose() => Compose(HttpComposeOption.AllDontCalculateLength);

    public override long Parse(byte[] data, uint offset, uint ends)
    {
        ValidateBounds(data, offset, ends);
        var originalOffset = offset;

        if (!HttpPacketHelpers.TryFindHeaderEnd(
                data, offset, ends, MaximumHeaderLength, out var bodyOffset))
            return -1;

        var lines = HttpPacketHelpers.ReadHeaderLines(
            data, offset, bodyOffset, MaximumHeaderCount);

        if (lines.Length == 0)
            return 0;

        var statusLine = lines[0].Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
        if (statusLine.Length < 2 ||
            !int.TryParse(statusLine[1], NumberStyles.None, CultureInfo.InvariantCulture, out var statusCode))
            return 0;

        Version = statusLine[0];
        Number = (HttpResponseCode)statusCode;
        Text = statusLine.Length == 3 ? statusLine[2] : string.Empty;
        Headers.Clear();
        Cookies.Clear();
        Message = null;

        var hasContentLength = false;

        for (var i = 1; i < lines.Length; i++)
        {
            var separator = lines[i].IndexOf(':');
            if (separator <= 0)
                return 0;

            var name = lines[i].Substring(0, separator).Trim();
            var value = lines[i].Substring(separator + 1).Trim();

            if (string.Equals(name, "transfer-encoding", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("HTTP Transfer-Encoding is not supported.");

            if (string.Equals(name, "content-length", StringComparison.OrdinalIgnoreCase))
            {
                if (hasContentLength)
                    throw new InvalidDataException("Duplicate HTTP Content-Length headers are not accepted.");

                hasContentLength = true;
            }

            Headers.Add(name, value);

            if (string.Equals(name, "set-cookie", StringComparison.OrdinalIgnoreCase) &&
                TryParseCookie(value, out var cookie))
                Cookies.Add(cookie);
        }

        var contentLengthHeader = Headers["content-length"];
        if (contentLengthHeader == null)
        {
            Message = Array.Empty<byte>();
            return bodyOffset - originalOffset;
        }

        if (!uint.TryParse(
                contentLengthHeader,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var contentLength))
            return 0;

        if (MaximumContentLength > 0 && contentLength > MaximumContentLength)
            throw new ParserLimitException(
                $"HTTP content length of {contentLength} bytes exceeds the {MaximumContentLength}-byte limit.");

        var availableBody = ends - bodyOffset;
        if (availableBody < contentLength)
            return -(long)(contentLength - availableBody);

        Message = data.Clip(bodyOffset, contentLength);
        return bodyOffset - originalOffset + contentLength;
    }

    private static bool TryParseCookie(string header, out HttpCookie cookie)
    {
        cookie = default;
        var segments = header.Split(';');
        if (segments.Length == 0)
            return false;

        var nameValueSeparator = segments[0].IndexOf('=');
        if (nameValueSeparator <= 0)
            return false;

        cookie = new HttpCookie(
            segments[0].Substring(0, nameValueSeparator).Trim(),
            segments[0].Substring(nameValueSeparator + 1).Trim());

        for (var i = 1; i < segments.Length; i++)
        {
            var segment = segments[i].Trim();
            var separator = segment.IndexOf('=');
            var name = separator < 0 ? segment : segment.Substring(0, separator).Trim();
            var value = separator < 0 ? string.Empty : segment.Substring(separator + 1).Trim();

            if (string.Equals(name, "domain", StringComparison.OrdinalIgnoreCase))
                cookie.Domain = value;
            else if (string.Equals(name, "path", StringComparison.OrdinalIgnoreCase))
                cookie.Path = value;
            else if (string.Equals(name, "httponly", StringComparison.OrdinalIgnoreCase))
                cookie.HttpOnly = true;
            else if (string.Equals(name, "secure", StringComparison.OrdinalIgnoreCase))
                cookie.Secure = true;
            else if (string.Equals(name, "samesite", StringComparison.OrdinalIgnoreCase) &&
                     Enum.TryParse(value, true, out HttpCookieSameSite sameSite))
                cookie.SameSite = sameSite;
            else if (string.Equals(name, "expires", StringComparison.OrdinalIgnoreCase) &&
                     DateTime.TryParse(
                         value,
                         CultureInfo.InvariantCulture,
                         DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                         out var expires))
                cookie.Expires = expires;
        }

        return true;
    }
}
