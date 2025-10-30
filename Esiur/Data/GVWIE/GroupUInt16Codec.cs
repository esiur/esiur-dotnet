using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace Esiur.Data.GVWIE;


public static class GroupUInt16Codec
{
    // ----------------- Encoder -----------------
    public static byte[] Encode(IList<ushort> values)
    {
        if (values is null) throw new ArgumentNullException(nameof(values));

        var dst = new List<byte>(values.Count * 2);
        int i = 0;

        while (i < values.Count)
        {
            ushort v = values[i];

            // Fast path: single byte for 0..127
            if (v <= 0x7F)
            {
                dst.Add((byte)v); // MSB=0 implicitly
                i++;
                continue;
            }

            // Group path: up to 16 items sharing a common width (1..2 bytes for uint16)
            int start = i;
            int count = 1;
            int width = WidthFromUnsigned(v);

            while (count < 16 && (i + count) < values.Count)
            {
                ushort v2 = values[i + count];
                int w2 = WidthFromUnsigned(v2);
                if (w2 > width) width = w2; // widen group if needed
                count++;
            }

            // Header: 1 | (count-1)[4b] | (width-1)[3b]
            byte header = 0x80;
            header |= (byte)(((count - 1) & 0xF) << 3);
            header |= (byte)((width - 1) & 0x7);
            dst.Add(header);

            // Payload
            for (int k = 0; k < count; k++)
            {
                WriteLE(dst, values[start + k], width);
            }

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
                // Fast path byte (0..127)
                result.Add(h);
                continue;
            }

            int count = ((h >> 3) & 0xF) + 1;  // 1..16
            int width = (h & 0x7) + 1;        // 1..8 (expect 1..2)

            if (width > 2)
                throw new NotSupportedException($"Width {width} bytes exceeds uint16 capacity.");

            for (int j = 0; j < count; j++)
            {
                uint val = (uint)ReadLE(src, ref pos, width);
                if (val > 0xFFFFu)
                    throw new OverflowException("Decoded value exceeds UInt16 range.");
                result.Add((ushort)val);
            }
        }

        return result.ToArray();
    }

    // ----------------- Helpers -----------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WidthFromUnsigned(ushort v) => (v <= 0xFF) ? 1 : 2;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteLE(List<byte> dst, ushort value, int width)
    {
        // width is 1 or 2
        dst.Add((byte)(value & 0xFF));
        if (width == 2) dst.Add((byte)(value >> 8));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ReadLE(ReadOnlySpan<byte> src, ref int pos, int width)
    {
        if (pos + width > src.Length)
            throw new ArgumentException("Buffer underflow while reading payload.");

        ulong v = src[pos++]; // first byte (LSB)
        if (width == 2) v |= (ulong)src[pos++] << 8;
        return v;
    }
}
