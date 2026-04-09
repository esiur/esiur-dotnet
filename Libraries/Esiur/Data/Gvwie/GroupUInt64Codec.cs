using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Esiur.Data.Gvwie;

public static class GroupUInt64Codec
{
    // Header layout:
    //   1 | cccc | www
    //
    // MSB = 1 => grouped form
    // cccc = 0..14 => short count = cccc + 1   (1..15)
    // cccc = 15    => extended count, followed by varint(count - 16)
    // www  = 0..7  => width = www + 1          (1..8)
    //
    // MSB = 0 => literal fast path for values in 7 bits

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
            int width = WidthFromValue(v, aligned);
            int count = 1;

            // Build a run of same-width non-literal values
            while ((i + count) < values.Count)
            {
                ulong v2 = values[i + count];

                // Do not absorb literal-fast-path values into groups
                if (v2 <= 0x7Ful)
                    break;

                int w2 = WidthFromValue(v2, aligned);
                if (w2 != width)
                    break;

                count++;
            }

            if (count <= 15)
            {
                // Short group:
                // Header: 1 | (count-1)[4 bits] | (width-1)[3 bits]
                byte header = 0x80;
                header |= (byte)(((count - 1) & 0x0F) << 3);
                header |= (byte)((width - 1) & 0x07);
                dst.Add(header);
            }
            else
            {
                // Extended group:
                // Header: 1 | 1111 | (width-1)[3 bits]
                // Followed by varint(count - 16)
                byte header = 0x80;
                header |= 0x78; // count bits = 1111
                header |= (byte)((width - 1) & 0x07);
                dst.Add(header);
                WriteVarUInt32(dst, checked((uint)(count - 16)));
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
                // Fast path: literal 7-bit unsigned value
                result.Add((ulong)(h & 0x7F));
                continue;
            }

            int countField = (h >> 3) & 0x0F;
            int width = (h & 0x07) + 1; // 1..8

            int count;
            if (countField == 15)
            {
                uint extra = ReadVarUInt32(src, ref pos);
                count = checked(16 + (int)extra);
            }
            else
            {
                count = countField + 1;
            }

            for (int j = 0; j < count; j++)
            {
                ulong raw = ReadLE(src, ref pos, width);
                result.Add(raw);
            }
        }

        return result.ToArray();
    }

    // ----------------- Helpers -----------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WidthFromValue(ulong v, bool aligned = false)
    {
        if (v <= 0xFFul) return 1;
        if (v <= 0xFFFFul) return 2;
        if (v <= 0xFFFFFFul) return aligned ? 4 : 3;
        if (v <= 0xFFFFFFFFul) return 4;
        if (v <= 0xFFFFFFFFFFul) return aligned ? 8: 5;
        if (v <= 0xFFFFFFFFFFFFul) return aligned ? 8: 6;
        if (v <= 0xFFFFFFFFFFFFFFul) return aligned ? 8 : 7;
        return 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteLE(List<byte> dst, ulong value, int width)
    {
        for (int i = 0; i < width; i++)
            dst.Add((byte)((value >> (8 * i)) & 0xFF));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ReadLE(ReadOnlySpan<byte> src, ref int pos, int width)
    {
        if ((uint)(pos + width) > (uint)src.Length)
            throw new ArgumentException("Buffer underflow while reading group payload.");

        ulong v = 0;
        for (int i = 0; i < width; i++)
            v |= (ulong)src[pos++] << (8 * i);
        return v;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteVarUInt32(List<byte> dst, uint value)
    {
        while (value >= 0x80)
        {
            dst.Add((byte)((value & 0x7F) | 0x80));
            value >>= 7;
        }

        dst.Add((byte)value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ReadVarUInt32(ReadOnlySpan<byte> src, ref int pos)
    {
        uint result = 0;
        int shift = 0;

        while (true)
        {
            if (pos >= src.Length)
                throw new ArgumentException("Buffer underflow while reading varint.");

            byte b = src[pos++];
            result |= (uint)(b & 0x7F) << shift;

            if ((b & 0x80) == 0)
                return result;

            shift += 7;
            if (shift >= 35)
                throw new ArgumentException("Varint is too long for UInt32.");
        }
    }
}