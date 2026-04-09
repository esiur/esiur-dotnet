using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Esiur.Data.Gvwie;

public static class GroupUInt64Codec
{
    // ----------------- Encoder -----------------
    public static byte[] Encode(IList<ulong> values, bool aligned = false)
    {
        var dst = new List<byte>(values.Count * 2);
        int i = 0;

        while (i < values.Count)
        {
            ulong v = values[i];

            // Fast path: single byte (MSB=0) when value fits in 7 bits
            if (v <= 0x7Ful)
            {
                dst.Add((byte)v);
                i++;
                continue;
            }

            int start = i;
            int width = WidthFromUInt64(v, aligned);
            int count = 1;

            // Build a run of same-width non-literal values
            while ((i + count) < values.Count)
            {
                ulong v2 = values[i + count];

                // Do not absorb literal-fast-path values into groups
                if (v2 <= 0x7Ful)
                    break;

                int w2 = WidthFromUInt64(v2, aligned);
                if (w2 != width)
                    break;

                count++;
            }

            if (count <= 12)
            {
                // Short group:
                // Header: 1 | (count-1)[4 bits] | (width-1)[3 bits]
                // count field 0000..1011 => count 1..12
                byte header = 0x80;
                header |= (byte)(((count - 1) & 0x0F) << 3);
                header |= (byte)((width - 1) & 0x07);
                dst.Add(header);
            }
            else
            {
                // Extended group:
                // Header: 1 | g[4 bits] | (width-1)[3 bits]
                //
                // g = 1100 => LoL = 1 byte
                // g = 1101 => LoL = 2 bytes
                // g = 1110 => LoL = 3 bytes
                // g = 1111 => LoL = 4 bytes
                //
                // LoL stores (count - 13) in little-endian form.

                uint extra = checked((uint)(count - 13));
                int lol = LengthOfLength(extra);

                byte groupBits = lol switch
                {
                    1 => 0b1100,
                    2 => 0b1101,
                    3 => 0b1110,
                    4 => 0b1111,
                    _ => throw new InvalidOperationException("Invalid LoL.")
                };

                byte header = 0x80;
                header |= (byte)(groupBits << 3);
                header |= (byte)((width - 1) & 0x07);
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
    public static ulong[] Decode(ReadOnlySpan<byte> src)
    {
        var result = new List<ulong>();
        int pos = 0;

        while (pos < src.Length)
        {
            byte h = src[pos++];

            if ((h & 0x80) == 0)
            {
                // Fast path: 7-bit literal in low bits
                result.Add((ulong)(h & 0x7F));
                continue;
            }

            int countField = (h >> 3) & 0x0F;
            int width = (h & 0x07) + 1;

            int count;

            if (countField <= 11)
            {
                // Short group: 0..11 => count 1..12
                count = countField + 1;
            }
            else
            {
                // Extended group:
                // 12 => LoL=1
                // 13 => LoL=2
                // 14 => LoL=3
                // 15 => LoL=4
                int lol = countField - 11;

                uint extra = checked((uint)ReadLE(src, ref pos, lol));
                count = checked(13 + (int)extra);
            }

            for (int j = 0; j < count; j++)
                result.Add(ReadLE(src, ref pos, width));
        }

        return result.ToArray();
    }

    // ----------------- Helpers -----------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WidthFromUInt64(ulong v, bool aligned = false)
    {
        if (v <= 0xFFul) return 1;
        if (v <= 0xFFFFul) return 2;
        if (v <= 0xFFFFFFul) return aligned ? 4 : 3;
        if (v <= 0xFFFFFFFFul) return 4;
        if (v <= 0xFFFFFFFFFFul) return aligned ? 8 : 5;
        if (v <= 0xFFFFFFFFFFFFul) return aligned ? 8 : 6;
        if (v <= 0xFFFFFFFFFFFFFFul) return aligned ? 8 : 7;
        return 8;
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
    private static void WriteLE(List<byte> dst, ulong value, int width)
    {
        for (int i = 0; i < width; i++)
            dst.Add((byte)((value >> (8 * i)) & 0xFF));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteLE(List<byte> dst, uint value, int width)
    {
        for (int i = 0; i < width; i++)
            dst.Add((byte)((value >> (8 * i)) & 0xFF));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ReadLE(ReadOnlySpan<byte> src, ref int pos, int width)
    {
        if ((uint)(pos + width) > (uint)src.Length)
            throw new ArgumentException("Buffer underflow while reading payload.");

        ulong v = 0;
        for (int i = 0; i < width; i++)
            v |= (ulong)src[pos++] << (8 * i);

        return v;
    }
}