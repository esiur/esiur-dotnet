using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Esiur.Data.Gvwie;

public static class GroupUInt32Codec
{
    // Header layout:
    //   1 | ccccc | ww
    //
    // MSB = 1 => grouped form
    // ccccc = 0..30 => short count = ccccc + 1   (1..31)
    // ccccc = 31    => extended count, followed by varint(count - 32)
    // ww = 0..3     => width = ww + 1            (1..4)
    //
    // MSB = 0 => literal fast path for values in 7 bits

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
            int width = WidthFromValue(v, aligned);
            int count = 1;

            // Build a run of same-width non-literal values
            while ((i + count) < values.Count)
            {
                uint v2 = values[i + count];

                // Do not absorb literal-fast-path values into groups
                if (v2 <= 0x7Fu)
                    break;

                int w2 = WidthFromValue(v2, aligned);
                if (w2 != width)
                    break;

                count++;
            }

            if (count <= 31)
            {
                // Short group:
                // Header: 1 | (count-1)[5 bits] | (width-1)[2 bits]
                byte header = 0x80;
                header |= (byte)(((count - 1) & 0x1F) << 2);
                header |= (byte)((width - 1) & 0x03);
                dst.Add(header);
            }
            else
            {
                // Extended group:
                // Header: 1 | 11111 | (width-1)[2 bits]
                // Followed by varint(count - 32)
                byte header = 0x80;
                header |= 0x7C; // count bits = 11111
                header |= (byte)((width - 1) & 0x03);
                dst.Add(header);
                WriteVarUInt32(dst, (uint)(count - 32));
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
                // Fast path: literal 7-bit unsigned value
                result.Add((uint)(h & 0x7F));
                continue;
            }

            int countField = (h >> 2) & 0x1F;
            int width = (h & 0x03) + 1; // 1..4

            int count;
            if (countField == 31)
            {
                uint extra = ReadVarUInt32(src, ref pos);
                count = checked(32 + (int)extra);
            }
            else
            {
                count = countField + 1;
            }

            for (int j = 0; j < count; j++)
            {
                uint raw = (uint)ReadLE(src, ref pos, width);
                result.Add(raw);
            }
        }

        return result.ToArray();
    }

    // ----------------- Helpers -----------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WidthFromValue(uint v, bool aligned = false)
    {
        if (v <= 0xFFu) return 1;
        if (v <= 0xFFFFu) return 2;
        if (v <= 0xFFFFFFu) return aligned ? 4 : 3;
        return 4;
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