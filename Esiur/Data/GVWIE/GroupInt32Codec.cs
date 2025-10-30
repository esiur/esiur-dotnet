using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Collections;

namespace Esiur.Data.GVWIE;

public static class GroupInt32Codec
{
    // ----------------- Encoder -----------------
    public static byte[] Encode(IList<int> values)
    {
        //var values = value as int[];

        var dst = new List<byte>(values.Count * 2);
        int i = 0;

        while (i < values.Count)
        {
            uint zz = ZigZag32(values[i]);

            // Fast path: single byte (MSB=0) when zigzag fits in 7 bits
            if (zz <= 0x7Fu)
            {
                dst.Add((byte)zz);
                i++;
                continue;
            }

            // Group: up to 32 items sharing a common width (1..4 bytes)
            int start = i;
            int count = 1;
            int width = WidthFromZigZag(zz);

            while (count < 32 && (i + count) < values.Count)
            {
                uint z2 = ZigZag32(values[i + count]);
                int w2 = WidthFromZigZag(z2);
                width = Math.Max(width, w2); // widen as needed
                count++;
            }

            // Header: 1 | (count-1)[5 bits] | (width-1)[2 bits]
            byte header = 0x80;
            header |= (byte)(((count - 1) & 0x1F) << 2);
            header |= (byte)((width - 1) & 0x03);
            dst.Add(header);

            // Payload: 'count' zigzag values, LE, 'width' bytes each
            for (int k = 0; k < count; k++)
                WriteLE(dst, ZigZag32(values[start + k]), width);

            i += count;
        }

        return dst.ToArray();
    }

    // ----------------- Decoder -----------------
    public static int[] Decode(ReadOnlySpan<byte> src)
    {
        var result = new List<int>();
        int pos = 0;

        while (pos < src.Length)
        {
            byte h = src[pos++];

            if ((h & 0x80) == 0)
            {
                // Fast path: 7-bit ZigZag in low bits
                uint zz7 = (uint)(h & 0x7F);
                result.Add(UnZigZag32(zz7));
                continue;
            }

            int count = ((h >> 2) & 0x1F) + 1; // 1..32
            int width = (h & 0x03) + 1;        // 1..4

            for (int j = 0; j < count; j++)
            {
                uint raw = (uint)ReadLE(src, ref pos, width);
                int val = UnZigZag32(raw);
                result.Add(val);
            }
        }

        return result.ToArray();
    }

    // ----------------- Helpers -----------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ZigZag32(int v) => (uint)((v << 1) ^ (v >> 31));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int UnZigZag32(uint u) => (int)((u >> 1) ^ (uint)-(int)(u & 1));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WidthFromZigZag(uint z)
    {
        if (z <= 0xFFu) return 1;
        if (z <= 0xFFFFu) return 2;
        if (z <= 0xFFFFFFu) return 3;
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
}
