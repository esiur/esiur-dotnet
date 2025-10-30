using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Esiur.Data.GVWIE;

public static class GroupInt16Codec
{
    // ----------------- Encoder -----------------
    public static byte[] Encode(IList<short> values)
    {
        var dst = new List<byte>(values.Count); // close lower bound
        int i = 0;

        while (i < values.Count)
        {
            ushort zz = ZigZag16(values[i]);

            // Fast path: single byte with 7-bit ZigZag
            if (zz <= 0x7Fu)
            {
                dst.Add((byte)zz); // MSB=0 implicitly
                i++;
                continue;
            }

            // Group path: up to 64 items sharing width (1 or 2 bytes)
            int start = i;
            int count = 1;
            int width = (zz <= 0xFFu) ? 1 : 2;

            while (count < 64 && (i + count) < values.Count)
            {
                ushort z2 = ZigZag16(values[i + count]);
                int w2 = (z2 <= 0xFFu) ? 1 : 2;
                if (w2 > width) width = w2; // widen as needed
                count++;
            }

            // Header: 1 | (count-1)[6 bits] | (width-1)[1 bit]
            byte header = 0x80;
            header |= (byte)(((count - 1) & 0x3F) << 1);
            header |= (byte)((width - 1) & 0x01);
            dst.Add(header);

            // Payload: count ZigZag magnitudes, LE, 'width' bytes each
            for (int k = 0; k < count; k++)
            {
                ushort z = ZigZag16(values[start + k]);
                WriteLE(dst, z, width);
            }

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
                // Fast path: 7-bit ZigZag
                ushort zz7 = (ushort)(h & 0x7F);
                result.Add(UnZigZag16(zz7));
                continue;
            }

            int count = ((h >> 1) & 0x3F) + 1; // 1..64
            int width = (h & 0x01) + 1;        // 1..2

            for (int j = 0; j < count; j++)
            {
                uint raw = ReadLE(src, ref pos, width);
                if (width > 2 && (raw >> 16) != 0)
                    throw new OverflowException("Decoded ZigZag value exceeds 16-bit range.");

                ushort u = (ushort)raw;
                short val = UnZigZag16(u);
                result.Add(val);
            }
        }

        return result.ToArray();
    }

    // ----------------- Helpers -----------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort ZigZag16(short v)
    {
        // (v << 1) ^ (v >> 15), result as unsigned 16-bit
        return (ushort)(((uint)(ushort)v << 1) ^ (uint)((int)v >> 15));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static short UnZigZag16(ushort u)
    {
        // (u >> 1) ^ -(u & 1), narrowed to 16-bit signed
        return (short)((u >> 1) ^ (ushort)-(short)(u & 1));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteLE(List<byte> dst, ushort value, int width)
    {
        // width is 1 or 2
        dst.Add((byte)(value & 0xFF));
        if (width == 2) dst.Add((byte)(value >> 8));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ReadLE(ReadOnlySpan<byte> src, ref int pos, int width)
    {
        if ((uint)(pos + width) > (uint)src.Length)
            throw new ArgumentException("Buffer underflow while reading group payload.");

        uint v = src[pos++];
        if (width == 2)
        {
            v |= (uint)src[pos++] << 8;
        }
        return v;
    }
}
