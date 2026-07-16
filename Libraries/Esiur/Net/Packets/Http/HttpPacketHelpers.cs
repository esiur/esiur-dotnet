using Esiur.Data;
using System;
using System.Text;

namespace Esiur.Net.Packets.Http;

internal static class HttpPacketHelpers
{
    internal const uint DefaultMaximumHeaderLength = 64 * 1024;
    internal const uint DefaultMaximumContentLength = 8 * 1024 * 1024;
    internal const int DefaultMaximumHeaderCount = 100;
    internal const int DefaultMaximumFormFields = 1_024;
    internal const int DefaultMaximumFormKeyLength = 2_048;
    internal const int DefaultMaximumFormValueLength = 1024 * 1024;
    internal const int DefaultMaximumMultipartPartLength = 4 * 1024 * 1024;

    internal static bool TryFindHeaderEnd(
        byte[] data,
        uint offset,
        uint ends,
        uint maximumHeaderLength,
        out uint bodyOffset)
    {
        bodyOffset = 0;
        var available = ends - offset;
        var scanLength = maximumHeaderLength == 0
            ? available
            : available < maximumHeaderLength ? available : maximumHeaderLength;

        if (scanLength >= 4)
        {
            var scanEnds = offset + scanLength;
            for (var i = offset; i <= scanEnds - 4; i++)
            {
                if (data[i] == '\r' && data[i + 1] == '\n' &&
                    data[i + 2] == '\r' && data[i + 3] == '\n')
                {
                    bodyOffset = i + 4;
                    return true;
                }
            }
        }

        if (maximumHeaderLength > 0 && available >= maximumHeaderLength)
            throw new ParserLimitException(
                $"HTTP header exceeds the {maximumHeaderLength}-byte limit.");

        return false;
    }

    internal static string[] ReadHeaderLines(
        byte[] data,
        uint offset,
        uint bodyOffset,
        int maximumHeaderCount)
    {
        var headerContentLength = bodyOffset - offset - 4;
        var headerEnd = offset + headerContentLength;
        var headerCount = 0;

        // Count before Split allocates its result so a header made of thousands of
        // tiny lines is rejected without creating thousands of strings first.
        for (var i = offset; i + 1 < headerEnd; i++)
        {
            if (data[i] != '\r' || data[i + 1] != '\n')
                continue;

            headerCount++;
            if (maximumHeaderCount > 0 && headerCount > maximumHeaderCount)
                throw new ParserLimitException(
                    $"HTTP header count exceeds the {maximumHeaderCount}-header limit.");

            i++;
        }

        return Encoding.ASCII
            .GetString(data, (int)offset, (int)headerContentLength)
            .Split(new[] { "\r\n" }, StringSplitOptions.None);
    }
}
