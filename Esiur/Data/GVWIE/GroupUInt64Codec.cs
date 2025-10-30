using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Esiur.Data.GVWIE;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public static class GroupUInt64Codec
{
    // ----------------- Encoder -----------------
    public static byte[] Encode(IList<ulong> values)
    {
        if (values is null) throw new ArgumentNullException(nameof(values));

        var dst = new List<byte>(values.Count * 2);
        int i = 0;

        while (i < values.Count)
        {
            ulong v = values[i];

            // Fast path: single byte for 0..127
            if (v <= 0x7FUL)
            {
                dst.Add((byte)v); // MSB = 0 implicitly
                i++;
                continue;
            }

            // Group path: up to 16 items sharing max width (1..8 bytes)
            int start = i;
            int count = 1;
            int width = WidthFromUnsigned(v);

            while (count < 16 && (i + count) < values.Count)
            {
                ulong v2 = values[i + count];
                int w2 = WidthFromUnsigned(v2);
                if (w2 > width) width = w2;
                count++;
            }

            // Header: 1 | (count-1)[4b] | (width-1)[3b]
            byte header = 0x80;
            header |= (byte)(((count - 1) & 0xF) << 3);
            header |= (byte)((width - 1) & 0x7);
            dst.Add(header);

            // Payload
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
                // Fast path byte (0..127)
                result.Add(h);
                continue;
            }

            int count = ((h >> 3) & 0xF) + 1;  // 1..16
            int width = (h & 0x7) + 1;        // 1..8

            if (width < 1 || width > 8)
                throw new NotSupportedException($"Invalid width {width} in header.");

            for (int j = 0; j < count; j++)
            {
                ulong val = ReadLE(src, ref pos, width);
                result.Add(val);
            }
        }

        return result.ToArray();
    }

    // ----------------- Helpers -----------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WidthFromUnsigned(ulong v)
    {
        if (v <= 0xFFUL) return 1;
        if (v <= 0xFFFFUL) return 2;
        if (v <= 0xFFFFFFUL) return 3;
        if (v <= 0xFFFFFFFFUL) return 4;
        if (v <= 0xFFFFFFFFFFUL) return 5;
        if (v <= 0xFFFFFFFFFFFFUL) return 6;
        if (v <= 0xFFFFFFFFFFFFFFUL) return 7;
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
        if (pos + width > src.Length)
            throw new ArgumentException("Buffer underflow while reading payload.");

        ulong v = 0;
        for (int i = 0; i < width; i++)
            v |= (ulong)src[pos++] << (8 * i);

        return v;
    }
}
