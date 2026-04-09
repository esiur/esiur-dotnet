//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using System;
//using System.Collections.Generic;
//using System.Runtime.CompilerServices;

//namespace Esiur.Data.Gvwie;

//public static class GroupInt64Codec
//{
//    // ----------------- Encoder -----------------
//    public static byte[] Encode(IList<long> values)
//    {
//        var dst = new List<byte>(values.Count * 2);
//        int i = 0;

//        while (i < values.Count)
//        {
//            ulong zz = ZigZag64(values[i]);

//            // Fast path: 1 byte when ZigZag fits in 7 bits
//            if (zz <= 0x7Ful)
//            {
//                dst.Add((byte)zz); // MSB = 0 implicitly
//                i++;
//                continue;
//            }

//            // Group path: up to 16 items sharing a common width (1..8 bytes)
//            int start = i;
//            int count = 1;
//            int width = WidthFromZigZag(zz);

//            while (count < 16 && (i + count) < values.Count)
//            {
//                ulong z2 = ZigZag64(values[i + count]);
//                int w2 = WidthFromZigZag(z2);
//                width = Math.Max(width, w2);   // widen as needed
//                count++;
//            }

//            // Header: 1 | (count-1)[4 bits] | (width-1)[3 bits]
//            byte header = 0x80;
//            header |= (byte)(((count - 1) & 0x0F) << 3);
//            header |= (byte)((width - 1) & 0x07);
//            dst.Add(header);

//            // Payload: 'count' ZigZag values, LE, 'width' bytes each
//            for (int k = 0; k < count; k++)
//            {
//                ulong z = ZigZag64(values[start + k]);
//                WriteLE(dst, z, width);
//            }

//            i += count;
//        }

//        return dst.ToArray();
//    }

//    // ----------------- Decoder -----------------
//    public static long[] Decode(ReadOnlySpan<byte> src)
//    {
//        var result = new List<long>();
//        int pos = 0;

//        while (pos < src.Length)
//        {
//            byte h = src[pos++];

//            if ((h & 0x80) == 0)
//            {
//                // Fast path: 7-bit ZigZag
//                ulong zz7 = (ulong)(h & 0x7F);
//                result.Add(UnZigZag64(zz7));
//                continue;
//            }

//            int count = ((h >> 3) & 0x0F) + 1; // 1..16
//            int width = (h & 0x07) + 1;        // 1..8

//            for (int j = 0; j < count; j++)
//            {
//                ulong raw = ReadLE(src, ref pos, width);
//                long val = UnZigZag64(raw);
//                result.Add(val);
//            }
//        }

//        return result.ToArray();
//    }

//    // ----------------- Helpers -----------------

//    [MethodImpl(MethodImplOptions.AggressiveInlining)]
//    private static ulong ZigZag64(long v) => (ulong)((v << 1) ^ (v >> 63));

//    [MethodImpl(MethodImplOptions.AggressiveInlining)]
//    private static long UnZigZag64(ulong u) => (long)((u >> 1) ^ (ulong)-(long)(u & 1));

//    [MethodImpl(MethodImplOptions.AggressiveInlining)]
//    private static int WidthFromZigZag(ulong z)
//    {
//        if (z <= 0xFFUL) return 1;
//        if (z <= 0xFFFFUL) return 2;
//        if (z <= 0xFFFFFFUL) return 3;
//        if (z <= 0xFFFFFFFFUL) return 4;
//        if (z <= 0xFFFFFFFFFFUL) return 5;
//        if (z <= 0xFFFFFFFFFFFFUL) return 6;
//        if (z <= 0xFFFFFFFFFFFFFFUL) return 7;
//        return 8;
//    }

//    [MethodImpl(MethodImplOptions.AggressiveInlining)]
//    private static void WriteLE(List<byte> dst, ulong value, int width)
//    {
//        for (int i = 0; i < width; i++)
//            dst.Add((byte)((value >> (8 * i)) & 0xFF));
//    }

//    [MethodImpl(MethodImplOptions.AggressiveInlining)]
//    private static ulong ReadLE(ReadOnlySpan<byte> src, ref int pos, int width)
//    {
//        if ((uint)(pos + width) > (uint)src.Length)
//            throw new ArgumentException("Buffer underflow while reading group payload.");

//        ulong v = 0;
//        for (int i = 0; i < width; i++)
//            v |= (ulong)src[pos++] << (8 * i);
//        return v;
//    }
//}


using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Esiur.Data.Gvwie;

public static class GroupInt64Codec
{
    // Header layout for grouped values:
    //   1 | cccc | www
    //
    // MSB = 1 => grouped form
    // cccc = 0..14  => short count = cccc + 1   (1..15)
    // cccc = 15     => extended count, followed by varint(count - 16)
    // www  = 0..7   => width = www + 1          (1..8)
    //
    // MSB = 0 => literal fast path for ZigZag values in 7 bits

    // ----------------- Encoder -----------------
    public static byte[] Encode(IList<long> values, bool aligned = false)
    {
        var dst = new List<byte>(values.Count * 2);
        int i = 0;

        while (i < values.Count)
        {
            ulong zz = ZigZag64(values[i]);

            // Fast path: single byte (MSB=0) when zigzag fits in 7 bits
            if (zz <= 0x7Ful)
            {
                dst.Add((byte)zz);
                i++;
                continue;
            }

            int start = i;
            int width = WidthFromZigZag(zz, aligned);
            int count = 1;

            // Build a run of same-width non-literal values
            while ((i + count) < values.Count)
            {
                ulong z2 = ZigZag64(values[i + count]);

                // Do not absorb literal-fast-path values into groups
                if (z2 <= 0x7Ful)
                    break;

                int w2 = WidthFromZigZag(z2, aligned);
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

            // Payload: 'count' zigzag values, LE, 'width' bytes each
            for (int k = 0; k < count; k++)
                WriteLE(dst, ZigZag64(values[start + k]), width);

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
                // Fast path: 7-bit ZigZag in low bits
                ulong zz7 = (ulong)(h & 0x7F);
                result.Add(UnZigZag64(zz7));
                continue;
            }

            int countField = (h >> 3) & 0x0F;
            int width = (h & 0x07) + 1;

            int count;
            if (countField == 15)
            {
                // Extended group length
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
    private static int WidthFromZigZag(ulong z, bool aligned = false)
    {
        if (z <= 0xFFul) return 1;
        if (z <= 0xFFFFul) return 2;
        if (z <= 0xFFFFFFul) return aligned ? 4 : 3;
        if (z <= 0xFFFFFFFFul) return 4;
        if (z <= 0xFFFFFFFFFFul) return aligned ? 8 : 5;
        if (z <= 0xFFFFFFFFFFFFul) return aligned ? 8 : 6;
        if (z <= 0xFFFFFFFFFFFFFFul) return aligned ? 8 : 7;
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