using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Esiur.Data.Gvwie;

public static class GroupUInt16Codec
{
    // ----------------- Encoder -----------------
    public static byte[] Encode(IList<ushort> values)
    {
        var dst = new List<byte>(values.Count * 2);
        int i = 0;

        while (i < values.Count)
        {
            ushort v = values[i];

            // Fast path: single byte (MSB=0) when value fits in 7 bits
            if (v <= 0x7F)
            {
                dst.Add((byte)v);
                i++;
                continue;
            }

            int start = i;
            int width = WidthFromUInt16(v); // 1..2
            int count = 1;

            // Build a run of same-width non-literal values
            while ((i + count) < values.Count)
            {
                ushort v2 = values[i + count];

                // Do not absorb literal-fast-path values into groups
                if (v2 <= 0x7F)
                    break;

                int w2 = WidthFromUInt16(v2);
                if (w2 != width)
                    break;

                count++;
            }

            if (count <= 60)
            {
                // Short group:
                // Header: 1 | (count-1)[6 bits] | (width-1)[1 bit]
                // count field 000000..111011 => count 1..60
                byte header = 0x80;
                header |= (byte)(((count - 1) & 0x3F) << 1);
                header |= (byte)((width - 1) & 0x01);
                dst.Add(header);
            }
            else
            {
                // Extended group:
                // Header: 1 | g[6 bits] | (width-1)[1 bit]
                //
                // g = 111100 => LoL = 1 byte
                // g = 111101 => LoL = 2 bytes
                // g = 111110 => LoL = 3 bytes
                // g = 111111 => LoL = 4 bytes
                //
                // LoL stores (count - 61) in little-endian form.

                uint extra = checked((uint)(count - 61));
                int lol = LengthOfLength(extra);

                byte groupBits = lol switch
                {
                    1 => 0b111100,
                    2 => 0b111101,
                    3 => 0b111110,
                    4 => 0b111111,
                    _ => throw new InvalidOperationException("Invalid LoL.")
                };

                byte header = 0x80;
                header |= (byte)(groupBits << 1);
                header |= (byte)((width - 1) & 0x01);
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
    public static ushort[] Decode(ReadOnlySpan<byte> src)
    {
        var result = new List<ushort>();
        int pos = 0;

        while (pos < src.Length)
        {
            byte h = src[pos++];

            if ((h & 0x80) == 0)
            {
                // Fast path: 7-bit literal in low bits
                result.Add((ushort)(h & 0x7F));
                continue;
            }

            int countField = (h >> 1) & 0x3F;
            int width = (h & 0x01) + 1;

            int count;

            if (countField <= 59)
            {
                // Short group: 0..59 => count 1..60
                count = countField + 1;
            }
            else
            {
                // Extended group:
                // 60 => LoL=1
                // 61 => LoL=2
                // 62 => LoL=3
                // 63 => LoL=4
                int lol = countField - 59;

                uint extra = ReadLE(src, ref pos, lol);
                count = checked(61 + (int)extra);
            }

            for (int j = 0; j < count; j++)
                result.Add((ushort)ReadLE(src, ref pos, width));
        }

        return result.ToArray();
    }

    // ----------------- Helpers -----------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WidthFromUInt16(ushort v)
    {
        return v <= 0xFF ? 1 : 2;
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
    private static void WriteLE(List<byte> dst, ushort value, int width)
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