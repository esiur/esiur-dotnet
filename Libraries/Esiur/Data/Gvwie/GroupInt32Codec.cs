//using System;
//using System.Collections.Generic;
//using System.Runtime.CompilerServices;

//namespace Esiur.Data.Gvwie;

//public static class GroupInt32Codec
//{

//    // ----------------- Encoder -----------------
//    public static byte[] Encode(IList<int> values, bool aligned = false)
//    {
//        var dst = new List<byte>(values.Count * 2);
//        int i = 0;

//        while (i < values.Count)
//        {
//            uint zz = ZigZag32(values[i]);

//            // Fast path: single byte (MSB=0) when zigzag fits in 7 bits
//            if (zz <= 0x7Fu)
//            {
//                dst.Add((byte)zz);
//                i++;
//                continue;
//            }

//            int start = i;
//            int width = WidthFromZigZag(zz, aligned);
//            int count = 1;

//            // Build a run of same-width non-literal values
//            while ((i + count) < values.Count)
//            {
//                uint z2 = ZigZag32(values[i + count]);

//                // Do not absorb literal-fast-path values into groups
//                if (z2 <= 0x7Fu)
//                    break;

//                int w2 = WidthFromZigZag(z2, aligned);
//                if (w2 != width)
//                    break;

//                count++;
//            }

//            if (count <= 31)
//            {
//                // Short group:
//                // Header: 1 | (count-1)[5 bits] | (width-1)[2 bits]
//                byte header = 0x80;
//                header |= (byte)(((count - 1) & 0x1F) << 2);
//                header |= (byte)((width - 1) & 0x03);
//                dst.Add(header);
//            }
//            else
//            {
//                // Extended group:
//                // Header: 1 | 11111 | (width-1)[2 bits]
//                // Followed by varint(count - 32)
//                byte header = 0x80;
//                header |= 0x7C; // count bits = 11111
//                header |= (byte)((width - 1) & 0x03);
//                dst.Add(header);
//                WriteVarUInt32(dst, (uint)(count - 32));
//            }

//            // Payload: 'count' zigzag values, LE, 'width' bytes each
//            for (int k = 0; k < count; k++)
//                WriteLE(dst, ZigZag32(values[start + k]), width);

//            i += count;
//        }

//        return dst.ToArray();
//    }

//    // ----------------- Decoder -----------------
//    public static int[] Decode(ReadOnlySpan<byte> src)
//    {
//        var result = new List<int>();
//        int pos = 0;

//        while (pos < src.Length)
//        {
//            byte h = src[pos++];

//            if ((h & 0x80) == 0)
//            {
//                // Fast path: 7-bit ZigZag in low bits
//                uint zz7 = (uint)(h & 0x7F);
//                result.Add(UnZigZag32(zz7));
//                continue;
//            }

//            int countField = (h >> 2) & 0x1F;
//            int width = (h & 0x03) + 1;

//            int count;
//            if (countField == 31)
//            {
//                // Extended group length
//                uint extra = ReadVarUInt32(src, ref pos);
//                count = checked(32 + (int)extra);
//            }
//            else
//            {
//                count = countField + 1;
//            }

//            for (int j = 0; j < count; j++)
//            {
//                uint raw = (uint)ReadLE(src, ref pos, width);
//                int val = UnZigZag32(raw);
//                result.Add(val);
//            }
//        }

//        return result.ToArray();
//    }

//    // ----------------- Helpers -----------------

//    [MethodImpl(MethodImplOptions.AggressiveInlining)]
//    private static uint ZigZag32(int v) => (uint)((v << 1) ^ (v >> 31));

//    [MethodImpl(MethodImplOptions.AggressiveInlining)]
//    private static int UnZigZag32(uint u) => (int)((u >> 1) ^ (uint)-(int)(u & 1));

//    [MethodImpl(MethodImplOptions.AggressiveInlining)]
//    private static int WidthFromZigZag(uint z, bool aligned = false)
//    {
//        if (z <= 0xFFu) return 1;
//        if (z <= 0xFFFFu) return 2;
//        if (z <= 0xFFFFFFu) return aligned ? 4 : 3;
//        return 4;
//    }

//    [MethodImpl(MethodImplOptions.AggressiveInlining)]
//    private static void WriteLE(List<byte> dst, uint value, int width)
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

//    [MethodImpl(MethodImplOptions.AggressiveInlining)]
//    private static void WriteVarUInt32(List<byte> dst, uint value)
//    {
//        while (value >= 0x80)
//        {
//            dst.Add((byte)((value & 0x7F) | 0x80));
//            value >>= 7;
//        }

//        dst.Add((byte)value);
//    }

//    [MethodImpl(MethodImplOptions.AggressiveInlining)]
//    private static uint ReadVarUInt32(ReadOnlySpan<byte> src, ref int pos)
//    {
//        uint result = 0;
//        int shift = 0;

//        while (true)
//        {
//            if (pos >= src.Length)
//                throw new ArgumentException("Buffer underflow while reading varint.");

//            byte b = src[pos++];
//            result |= (uint)(b & 0x7F) << shift;

//            if ((b & 0x80) == 0)
//                return result;

//            shift += 7;
//            if (shift >= 35)
//                throw new ArgumentException("Varint is too long for UInt32.");
//        }
//    }
//}



using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Esiur.Data.Gvwie;

public static class GroupInt32Codec
{
    // ----------------- Encoder -----------------
    public static byte[] Encode(IList<int> values, bool aligned = false)
    {
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

            int start = i;
            int width = WidthFromZigZag(zz, aligned);
            int count = 1;

            // Build a run of same-width non-literal values
            while ((i + count) < values.Count)
            {
                uint z2 = ZigZag32(values[i + count]);

                // Do not absorb literal-fast-path values into groups
                if (z2 <= 0x7Fu)
                    break;

                int w2 = WidthFromZigZag(z2, aligned);
                if (w2 != width)
                    break;

                count++;
            }

            if (count <= 28)
            {
                // Short group:
                // Header: 1 | (count-1)[5 bits] | (width-1)[2 bits]
                // count field values:
                //   00000..11011 => count = 1..28
                byte header = 0x80;
                header |= (byte)(((count - 1) & 0x1F) << 2);
                header |= (byte)((width - 1) & 0x03);
                dst.Add(header);
            }
            else
            {
                // Extended group:
                // Header: 1 | g[5 bits] | (width-1)[2 bits]
                //
                // g = 11100 => LoL = 1 byte
                // g = 11101 => LoL = 2 bytes
                // g = 11110 => LoL = 3 bytes
                // g = 11111 => LoL = 4 bytes
                //
                // LoL stores (count - 29) in little-endian form.

                uint extra = checked((uint)(count - 29));
                int lol = LengthOfLength(extra); // 1..4

                byte groupBits = lol switch
                {
                    1 => 0b11100,
                    2 => 0b11101,
                    3 => 0b11110,
                    4 => 0b11111,
                    _ => throw new InvalidOperationException("Invalid LoL.")
                };

                byte header = 0x80;
                header |= (byte)(groupBits << 2);
                header |= (byte)((width - 1) & 0x03);
                dst.Add(header);

                WriteLE(dst, extra, lol);
            }

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

            int countField = (h >> 2) & 0x1F;
            int width = (h & 0x03) + 1;

            int count;

            if (countField <= 27)
            {
                // Short group: 0..27 => count 1..28
                count = countField + 1;
            }
            else
            {
                // Extended group:
                // 28 => LoL=1
                // 29 => LoL=2
                // 30 => LoL=3
                // 31 => LoL=4
                int lol = countField - 27;

                uint extra = checked((uint)ReadLE(src, ref pos, lol));
                count = checked(29 + (int)extra);
            }

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
    private static int WidthFromZigZag(uint z, bool aligned = false)
    {
        if (z <= 0xFFu) return 1;
        if (z <= 0xFFFFu) return 2;
        if (z <= 0xFFFFFFu) return aligned ? 4 : 3;
        return 4;
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
    private static void WriteLE(List<byte> dst, uint value, int width)
    {
        for (int i = 0; i < width; i++)
            dst.Add((byte)((value >> (8 * i)) & 0xFF));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ReadLE(ReadOnlySpan<byte> src, ref int pos, int width)
    {
        if ((uint)(pos + width) > (uint)src.Length)
            throw new ArgumentException("Buffer underflow while reading payload.");

        ulong v = 0;
        for (int i = 0; i < width; i++)
            v |= (ulong)src[pos++] << (8 * i);

        return v;
    }
}