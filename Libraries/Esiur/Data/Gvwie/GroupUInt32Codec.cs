using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Esiur.Data.Gvwie;

public static class GroupUInt32Codec
{
    // ----------------- Encoder -----------------
    public static byte[] Encode(IList<uint> values, bool aligned = false)
    {
        var dst = new List<byte>(values.Count * 2);
        int i = 0;

        while (i < values.Count)
        {
            uint v = values[i];

            // Fast path: single byte (MSB=0) when value fits in 7 bits
            if (v <= 0x7Fu)
            {
                dst.Add((byte)v);
                i++;
                continue;
            }

            int start = i;
            int width = WidthFromUInt32(v, aligned);
            int count = 1;

            // Build a run of same-width non-literal values
            while ((i + count) < values.Count)
            {
                uint v2 = values[i + count];

                // Do not absorb literal-fast-path values into groups
                if (v2 <= 0x7Fu)
                    break;

                int w2 = WidthFromUInt32(v2, aligned);
                if (w2 != width)
                    break;

                count++;
            }

            if (count <= 28)
            {
                // Short group:
                // Header: 1 | (count-1)[5 bits] | (width-1)[2 bits]
                // count field 00000..11011 => count 1..28
                byte header = 0x80;
                header |= (byte)(((count - 1) & 0x1F) << 2);
                header |= (byte)((width - 1) & 0x03);
                dst.Add(header);
            }
            else
            {
                // Extended group:
                // Header: 1 | g[5 bits] | (width-1)[2 bits]
                //
                // g = 11100 => LoL = 1 byte
                // g = 11101 => LoL = 2 bytes
                // g = 11110 => LoL = 3 bytes
                // g = 11111 => LoL = 4 bytes
                //
                // LoL stores (count - 29) in little-endian form.

                uint extra = checked((uint)(count - 29));
                int lol = LengthOfLength(extra);

                byte groupBits = lol switch
                {
                    1 => 0b11100,
                    2 => 0b11101,
                    3 => 0b11110,
                    4 => 0b11111,
                    _ => throw new InvalidOperationException("Invalid LoL.")
                };

                byte header = 0x80;
                header |= (byte)(groupBits << 2);
                header |= (byte)((width - 1) & 0x03);
                dst.Add(header);

                WriteLE(dst, extra, lol);
            }

            // Payload: 'count' values, LE, 'width' bytes each
            for (int k = 0; k < count; k++)
                WriteLE(dst, values[start + k], width);

            i += count;
        }

        return dst.ToArray();
    }

    // ----------------- Decoder -----------------
    public static uint[] Decode(ReadOnlySpan<byte> src)
    {
        var result = new List<uint>();
        int pos = 0;

        while (pos < src.Length)
        {
            byte h = src[pos++];

            if ((h & 0x80) == 0)
            {
                // Fast path: 7-bit literal in low bits
                result.Add((uint)(h & 0x7F));
                continue;
            }

            int countField = (h >> 2) & 0x1F;
            int width = (h & 0x03) + 1;

            int count;

            if (countField <= 27)
            {
                // Short group: 0..27 => count 1..28
                count = countField + 1;
            }
            else
            {
                // Extended group:
                // 28 => LoL=1
                // 29 => LoL=2
                // 30 => LoL=3
                // 31 => LoL=4
                int lol = countField - 27;

                uint extra = ReadLE(src, ref pos, lol);
                count = checked(29 + (int)extra);
            }

            for (int j = 0; j < count; j++)
                result.Add(ReadLE(src, ref pos, width));
        }

        return result.ToArray();
    }

    // ----------------- Helpers -----------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WidthFromUInt32(uint v, bool aligned = false)
    {
        if (v <= 0xFFu) return 1;
        if (v <= 0xFFFFu) return 2;
        if (v <= 0xFFFFFFu) return aligned ? 4 : 3;
        return 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int LengthOfLength(uint value)
    {
        if (value <= 0xFFu) return 1;
        if (value <= 0xFFFFu) return 2;
        if (value <= 0xFFFFFFu) return 3;
        return 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteLE(List<byte> dst, uint value, int width)
    {
        for (int i = 0; i < width; i++)
            dst.Add((byte)((value >> (8 * i)) & 0xFF));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ReadLE(ReadOnlySpan<byte> src, ref int pos, int width)
    {
        if ((uint)(pos + width) > (uint)src.Length)
            throw new ArgumentException("Buffer underflow while reading payload.");

        uint v = 0;
        for (int i = 0; i < width; i++)
            v |= (uint)src[pos++] << (8 * i);

        return v;
    }
}