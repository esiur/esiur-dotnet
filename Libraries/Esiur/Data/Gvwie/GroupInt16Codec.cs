using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Esiur.Data.Gvwie;

public static class GroupInt16Codec
{
    // Header layout:
    //   1 | cccccc | w
    //
    // MSB = 1 => grouped form
    // cccccc = 0..62 => short count = cccccc + 1   (1..63)
    // cccccc = 63    => extended count, followed by varint(count - 64)
    // w = 0 => width = 1 byte
    // w = 1 => width = 2 bytes
    //
    // MSB = 0 => literal fast path for ZigZag values in 7 bits

    // ----------------- Encoder -----------------
    public static byte[] Encode(IList<short> values)
    {
        var dst = new List<byte>(values.Count * 2);
        int i = 0;

        while (i < values.Count)
        {
            ushort zz = ZigZag16(values[i]);

            // Fast path: single byte (MSB=0) when zigzag fits in 7 bits
            if (zz <= 0x7F)
            {
                dst.Add((byte)zz);
                i++;
                continue;
            }

            int start = i;
            int width = WidthFromZigZag(zz); // 1 or 2
            int count = 1;

            // Build a run of same-width non-literal values
            while ((i + count) < values.Count)
            {
                ushort z2 = ZigZag16(values[i + count]);

                // Do not absorb literal-fast-path values into groups
                if (z2 <= 0x7F)
                    break;

                int w2 = WidthFromZigZag(z2);
                if (w2 != width)
                    break;

                count++;
            }

            if (count <= 63)
            {
                // Short group:
                // Header: 1 | (count-1)[6 bits] | (width-1)[1 bit]
                byte header = 0x80;
                header |= (byte)(((count - 1) & 0x3F) << 1);
                header |= (byte)((width - 1) & 0x01);
                dst.Add(header);
            }
            else
            {
                // Extended group:
                // Header: 1 | 111111 | (width-1)[1 bit]
                // Followed by varint(count - 64)
                byte header = 0x80;
                header |= 0x7E; // count bits = 111111
                header |= (byte)((width - 1) & 0x01);
                dst.Add(header);
                WriteVarUInt32(dst, (uint)(count - 64));
            }

            // Payload: 'count' zigzag values, LE, 'width' bytes each
            for (int k = 0; k < count; k++)
                WriteLE(dst, ZigZag16(values[start + k]), width);

            i += count;
        }

        return dst.ToArray();
    }

    // ----------------- Decoder -----------------
    public static short[] Decode(ReadOnlySpan<byte> src)
    {
        var result = new List<short>();
        int pos = 0;

        while (pos < src.Length)
        {
            byte h = src[pos++];

            if ((h & 0x80) == 0)
            {
                // Fast path: 7-bit ZigZag in low bits
                ushort zz7 = (ushort)(h & 0x7F);
                result.Add(UnZigZag16(zz7));
                continue;
            }

            int countField = (h >> 1) & 0x3F;
            int width = (h & 0x01) + 1; // 1 or 2

            int count;
            if (countField == 63)
            {
                uint extra = ReadVarUInt32(src, ref pos);
                count = checked(64 + (int)extra);
            }
            else
            {
                count = countField + 1;
            }

            for (int j = 0; j < count; j++)
            {
                ushort raw = (ushort)ReadLE(src, ref pos, width);
                result.Add(UnZigZag16(raw));
            }
        }

        return result.ToArray();
    }

    // ----------------- Helpers -----------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort ZigZag16(short v) => (ushort)((v << 1) ^ (v >> 15));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static short UnZigZag16(ushort u) => (short)((u >> 1) ^ (ushort)-(short)(u & 1));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WidthFromZigZag(ushort z)
    {
        return z <= 0xFF ? 1 : 2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteLE(List<byte> dst, ushort value, int width)
    {
        for (int i = 0; i < width; i++)
            dst.Add((byte)((value >> (8 * i)) & 0xFF));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ReadLE(ReadOnlySpan<byte> src, ref int pos, int width)
    {
        if ((uint)(pos + width) > (uint)src.Length)
            throw new ArgumentException("Buffer underflow while reading group payload.");

        uint v = 0;
        for (int i = 0; i < width; i++)
            v |= (uint)src[pos++] << (8 * i);
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