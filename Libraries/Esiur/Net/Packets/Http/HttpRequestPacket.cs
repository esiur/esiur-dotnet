using Esiur.Data;
using Esiur.Misc;
using System;
using System.Globalization;
using System.Net;
using System.Text;

namespace Esiur.Net.Packets.Http;

public class HttpRequestPacket : Packet
{
    public StringKeyList Query;
    public HttpMethod Method;
    public string RawMethod;
    public StringKeyList Headers;
    public bool WSMode;
    public string Version;
    public StringKeyList Cookies;
    public string URL;
    public string Filename;
    public KeyList<string, object> PostForms;
    public byte[] Message;

    public uint MaximumHeaderLength { get; set; } = HttpPacketHelpers.DefaultMaximumHeaderLength;
    public uint MaximumContentLength { get; set; } = HttpPacketHelpers.DefaultMaximumContentLength;
    public int MaximumHeaderCount { get; set; } = HttpPacketHelpers.DefaultMaximumHeaderCount;
    public int MaximumFormFields { get; set; } = HttpPacketHelpers.DefaultMaximumFormFields;
    public int MaximumFormKeyLength { get; set; } = HttpPacketHelpers.DefaultMaximumFormKeyLength;
    public int MaximumFormValueLength { get; set; } = HttpPacketHelpers.DefaultMaximumFormValueLength;
    public int MaximumMultipartPartLength { get; set; } = HttpPacketHelpers.DefaultMaximumMultipartPartLength;

    public override string ToString()
        => $"HTTPRequestPacket\n\tVersion: {Version}\n\tMethod: {Method}\n\tURL: {URL}" +
           $"\n\tMessage: {(Message == null ? "NULL" : Message.Length.ToString())}";

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

        Cookies = new StringKeyList();
        PostForms = new KeyList<string, object>();
        Query = new StringKeyList();
        Headers = new StringKeyList();
        Message = null;

        var requestLine = lines[0].Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
        if (requestLine.Length != 3)
            return 0;

        RawMethod = requestLine[0];
        Method = GetMethod(RawMethod);
        Version = requestLine[2].Trim();

        var target = requestLine[1].Trim();
        if (Uri.TryCreate(target, UriKind.Absolute, out var absoluteUri))
            target = absoluteUri.PathAndQuery;

        var queryIndex = target.IndexOf('?');
        var rawFilename = queryIndex < 0 ? target : target.Substring(0, queryIndex);
        Filename = WebUtility.UrlDecode(rawFilename);
        URL = WebUtility.UrlDecode(target);

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

            Headers[name] = value;

            if (string.Equals(name, "cookie", StringComparison.OrdinalIgnoreCase))
                ParseCookies(value);
        }

        if (queryIndex >= 0 && queryIndex + 1 < target.Length)
            ParseQuery(target.Substring(queryIndex + 1));

        var contentLength = 0u;
        if (hasContentLength && !uint.TryParse(
                Headers["content-length"],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out contentLength))
            throw new InvalidDataException("HTTP Content-Length is invalid.");

        if (MaximumContentLength > 0 && contentLength > MaximumContentLength)
            throw new ParserLimitException(
                $"HTTP content length of {contentLength} bytes exceeds the {MaximumContentLength}-byte limit.");

        var availableBody = ends - bodyOffset;
        if (availableBody < contentLength)
            return -(long)(contentLength - availableBody);

        var contentType = Headers["content-type"];
        if (Method == HttpMethod.POST &&
            (string.IsNullOrEmpty(contentType) ||
             contentType.StartsWith("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase)))
        {
            ParseUrlEncodedForm(Encoding.UTF8.GetString(data, (int)bodyOffset, (int)contentLength));
        }
        else if (Method == HttpMethod.POST &&
                 contentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryParseMultipart(data, bodyOffset, contentLength, contentType))
                return 0;
        }
        else
        {
            Message = data.Clip(bodyOffset, contentLength);
        }

        return bodyOffset - originalOffset + contentLength;
    }

    private static HttpMethod GetMethod(string method)
    {
        if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase)) return HttpMethod.GET;
        if (string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase)) return HttpMethod.POST;
        if (string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase)) return HttpMethod.HEAD;
        if (string.Equals(method, "PUT", StringComparison.OrdinalIgnoreCase)) return HttpMethod.PUT;
        if (string.Equals(method, "DELETE", StringComparison.OrdinalIgnoreCase)) return HttpMethod.DELETE;
        if (string.Equals(method, "OPTIONS", StringComparison.OrdinalIgnoreCase)) return HttpMethod.OPTIONS;
        if (string.Equals(method, "TRACE", StringComparison.OrdinalIgnoreCase)) return HttpMethod.TRACE;
        if (string.Equals(method, "CONNECT", StringComparison.OrdinalIgnoreCase)) return HttpMethod.CONNECT;
        return HttpMethod.UNKNOWN;
    }

    private void ParseCookies(string header)
    {
        foreach (var segment in header.Split(';'))
        {
            var cookie = segment.Trim();
            if (cookie.Length == 0)
                continue;

            var separator = cookie.IndexOf('=');
            var name = separator < 0 ? cookie : cookie.Substring(0, separator).Trim();
            var value = separator < 0 ? string.Empty : cookie.Substring(separator + 1).Trim();
            if (!Cookies.ContainsKey(name))
                Cookies.Add(name, value);
        }
    }

    private void ParseQuery(string query)
    {
        foreach (var segment in query.Split('&'))
        {
            var separator = segment.IndexOf('=');
            var name = WebUtility.UrlDecode(separator < 0 ? segment : segment.Substring(0, separator));
            var value = separator < 0 ? null : WebUtility.UrlDecode(segment.Substring(separator + 1));
            if (!Query.ContainsKey(name))
                Query.Add(name, value);
        }
    }

    private void ParseUrlEncodedForm(string form)
    {
        if (form.Length == 0)
            return;

        var fieldCount = 0;
        var start = 0;
        StringBuilder unknown = null;
        var hasUnknownValue = false;

        while (start <= form.Length)
        {
            fieldCount++;
            EnsureWithinLimit(fieldCount, MaximumFormFields, "form fields");

            var end = form.IndexOf('&', start);
            if (end < 0)
                end = form.Length;

            var separator = form.IndexOf('=', start, end - start);
            if (separator >= 0)
            {
                var key = DecodeFormComponent(form.Substring(start, separator - start));
                var value = DecodeFormComponent(form.Substring(separator + 1, end - separator - 1));
                EnsureStringWithinLimit(key, MaximumFormKeyLength, "form key");
                EnsureStringWithinLimit(value, MaximumFormValueLength, "form value");

                if (string.Equals(key, "unknown", StringComparison.Ordinal))
                {
                    if (unknown == null)
                        unknown = new StringBuilder(value.Length);
                    else
                        unknown.Clear();
                    unknown.Append(value);
                    hasUnknownValue = true;
                }

                PostForms[key] = value;
            }
            else
            {
                var value = DecodeFormComponent(form.Substring(start, end - start));
                EnsureStringWithinLimit(value, MaximumFormValueLength, "form value");

                if (unknown == null)
                    unknown = new StringBuilder(value.Length);

                EnsureStringWithinLimit(
                    unknown.Length + (hasUnknownValue ? 1 : 0) + value.Length,
                    MaximumFormValueLength,
                    "combined form value");

                if (hasUnknownValue)
                    unknown.Append('&');
                unknown.Append(value);
                hasUnknownValue = true;
            }

            if (end == form.Length)
                break;
            start = end + 1;
        }

        if (unknown != null)
            PostForms["unknown"] = unknown.ToString();
    }

    private bool TryParseMultipart(
        byte[] data,
        uint bodyOffset,
        uint contentLength,
        string contentType)
    {
        if (!TryGetMultipartBoundary(contentType, out var boundary))
            return false;

        var delimiter = "--" + boundary;
        var body = Encoding.UTF8.GetString(data, (int)bodyOffset, (int)contentLength);
        var position = 0;
        var fieldCount = 0;

        while (position < body.Length)
        {
            var delimiterStart = body.IndexOf(delimiter, position, StringComparison.Ordinal);
            if (delimiterStart < 0)
                return false;

            position = delimiterStart + delimiter.Length;
            if (position + 2 <= body.Length &&
                string.CompareOrdinal(body, position, "--", 0, 2) == 0)
                return true;

            if (position + 2 > body.Length ||
                string.CompareOrdinal(body, position, "\r\n", 0, 2) != 0)
                return false;
            position += 2;

            var nextDelimiter = body.IndexOf("\r\n" + delimiter, position, StringComparison.Ordinal);
            if (nextDelimiter < 0)
                return false;

            var partLength = nextDelimiter - position;
            EnsureWithinLimit(partLength, MaximumMultipartPartLength, "multipart part length");

            var headerEnd = body.IndexOf("\r\n\r\n", position, partLength, StringComparison.Ordinal);
            if (headerEnd < 0)
                return false;

            var nameStart = body.IndexOf("name=\"", position, headerEnd - position, StringComparison.OrdinalIgnoreCase);
            if (nameStart < 0)
                return false;
            nameStart += 6;

            var nameEnd = body.IndexOf('"', nameStart, headerEnd - nameStart);
            if (nameEnd < 0 || nameEnd > headerEnd)
                return false;

            fieldCount++;
            EnsureWithinLimit(fieldCount, MaximumFormFields, "form fields");

            var name = body.Substring(nameStart, nameEnd - nameStart);
            EnsureStringWithinLimit(name, MaximumFormKeyLength, "form key");

            var valueStart = headerEnd + 4;
            PostForms[name] = body.Substring(valueStart, nextDelimiter - valueStart);
            position = nextDelimiter + 2;
        }

        return false;
    }

    private static string DecodeFormComponent(string value)
        => WebUtility.HtmlDecode(WebUtility.UrlDecode(value));

    private static bool TryGetMultipartBoundary(string contentType, out string boundary)
    {
        boundary = null;
        var parameterStart = contentType.IndexOf(';');
        while (parameterStart >= 0 && parameterStart + 1 < contentType.Length)
        {
            var parameterEnd = contentType.IndexOf(';', parameterStart + 1);
            if (parameterEnd < 0)
                parameterEnd = contentType.Length;

            var parameter = contentType.Substring(
                parameterStart + 1,
                parameterEnd - parameterStart - 1).Trim();
            var separator = parameter.IndexOf('=');
            if (separator > 0 && string.Equals(
                    parameter.Substring(0, separator).Trim(),
                    "boundary",
                    StringComparison.OrdinalIgnoreCase))
            {
                boundary = parameter.Substring(separator + 1).Trim().Trim('"');
                return boundary.Length > 0 && boundary.IndexOfAny(new[] { '\r', '\n' }) < 0;
            }

            parameterStart = parameterEnd < contentType.Length ? parameterEnd : -1;
        }

        return false;
    }

    private static void EnsureStringWithinLimit(string value, int limit, string kind)
        => EnsureStringWithinLimit(value?.Length ?? 0, limit, kind);

    private static void EnsureStringWithinLimit(int length, int limit, string kind)
        => EnsureWithinLimit(length, limit, kind);

    private static void EnsureWithinLimit(int value, int limit, string kind)
    {
        if (limit > 0 && value > limit)
            throw new ParserLimitException(
                $"HTTP {kind} of {value} exceeds the configured limit of {limit}.");
    }
}
