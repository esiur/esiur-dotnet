using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Esiur.Data.GVWIE;

public static class GroupInt64Codec
{
    // ----------------- Encoder -----------------
    public static byte[] Encode(IList<long> values)
    {
        var dst = new List<byte>(values.Count * 2);
        int i = 0;

        while (i < values.Count)
        {
            ulong zz = ZigZag64(values[i]);

            // Fast path: 1 byte when ZigZag fits in 7 bits
            if (zz <= 0x7Ful)
            {
                dst.Add((byte)zz); // MSB = 0 implicitly
                i++;
                continue;
            }

            // Group path: up to 16 items sharing a common width (1..8 bytes)
            int start = i;
            int count = 1;
            int width = WidthFromZigZag(zz);

            while (count < 16 && (i + count) < values.Count)
            {
                ulong z2 = ZigZag64(values[i + count]);
                int w2 = WidthFromZigZag(z2);
                width = Math.Max(width, w2);   // widen as needed
                count++;
            }

            // Header: 1 | (count-1)[4 bits] | (width-1)[3 bits]
            byte header = 0x80;
            header |= (byte)(((count - 1) & 0x0F) << 3);
            header |= (byte)((width - 1) & 0x07);
            dst.Add(header);

            // Payload: 'count' ZigZag values, LE, 'width' bytes each
            for (int k = 0; k < count; k++)
            {
                ulong z = ZigZag64(values[start + k]);
                WriteLE(dst, z, width);
            }

            i += count;
        }

        return dst.ToArray();
    }

    // ----------------- Decoder -----------------
    public static long[] Decode(ReadOnlySpan<byte> src)
    {
        var result = new List<long>();
        int pos = 0;

        while (pos < src.Length)
        {
            byte h = src[pos++];

            if ((h & 0x80) == 0)
            {
                // Fast path: 7-bit ZigZag
                ulong zz7 = (ulong)(h & 0x7F);
                result.Add(UnZigZag64(zz7));
                continue;
            }

            int count = ((h >> 3) & 0x0F) + 1; // 1..16
            int width = (h & 0x07) + 1;        // 1..8

            for (int j = 0; j < count; j++)
            {
                ulong raw = ReadLE(src, ref pos, width);
                long val = UnZigZag64(raw);
                result.Add(val);
            }
        }

        return result.ToArray();
    }

    // ----------------- Helpers -----------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ZigZag64(long v) => (ulong)((v << 1) ^ (v >> 63));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long UnZigZag64(ulong u) => (long)((u >> 1) ^ (ulong)-(long)(u & 1));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WidthFromZigZag(ulong z)
    {
        if (z <= 0xFFUL) return 1;
        if (z <= 0xFFFFUL) return 2;
        if (z <= 0xFFFFFFUL) return 3;
        if (z <= 0xFFFFFFFFUL) return 4;
        if (z <= 0xFFFFFFFFFFUL) return 5;
        if (z <= 0xFFFFFFFFFFFFUL) return 6;
        if (z <= 0xFFFFFFFFFFFFFFUL) return 7;
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
}
