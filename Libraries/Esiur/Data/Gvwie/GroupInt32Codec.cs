////using System;
////using System.Collections.Generic;
////using System.Linq;
////using System.Text;
////using System.Threading.Tasks;
////using System.Runtime.CompilerServices;
////using System.Collections;

////namespace Esiur.Data.Gvwie;

////public static class GroupInt32Codec
////{
////    // ----------------- Encoder -----------------
////    public static byte[] Encode(IList<int> values)
////    {
////        //var values = value as int[];

////        var dst = new List<byte>(values.Count * 2);
////        int i = 0;

////        while (i < values.Count)
////        {
////            uint zz = ZigZag32(values[i]);

////            // Fast path: single byte (MSB=0) when zigzag fits in 7 bits
////            if (zz <= 0x7Fu)
////            {
////                dst.Add((byte)zz);
////                i++;
////                continue;
////            }

////            // Group: up to 32 items sharing a common width (1..4 bytes)
////            int start = i;
////            int count = 1;
////            int width = WidthFromZigZag(zz);

////            while (count < 32 && (i + count) < values.Count)
////            {
////                uint z2 = ZigZag32(values[i + count]);
////                int w2 = WidthFromZigZag(z2);
////                width = Math.Max(width, w2); // widen as needed
////                count++;
////            }

////            // Header: 1 | (count-1)[5 bits] | (width-1)[2 bits]
////            byte header = 0x80;
////            header |= (byte)(((count - 1) & 0x1F) << 2);
////            header |= (byte)((width - 1) & 0x03);
////            dst.Add(header);

////            // Payload: 'count' zigzag values, LE, 'width' bytes each
////            for (int k = 0; k < count; k++)
////                WriteLE(dst, ZigZag32(values[start + k]), width);

////            i += count;
////        }

////        return dst.ToArray();
////    }

////    // ----------------- Decoder -----------------
////    public static int[] Decode(ReadOnlySpan<byte> src)
////    {
////        var result = new List<int>();
////        int pos = 0;

////        while (pos < src.Length)
////        {
////            byte h = src[pos++];

////            if ((h & 0x80) == 0)
////            {
////                // Fast path: 7-bit ZigZag in low bits
////                uint zz7 = (uint)(h & 0x7F);
////                result.Add(UnZigZag32(zz7));
////                continue;
////            }

////            int count = ((h >> 2) & 0x1F) + 1; // 1..32
////            int width = (h & 0x03) + 1;        // 1..4

////            for (int j = 0; j < count; j++)
////            {
////                uint raw = (uint)ReadLE(src, ref pos, width);
////                int val = UnZigZag32(raw);
////                result.Add(val);
////            }
////        }

////        return result.ToArray();
////    }

////    // ----------------- Helpers -----------------

////    [MethodImpl(MethodImplOptions.AggressiveInlining)]
////    private static uint ZigZag32(int v) => (uint)((v << 1) ^ (v >> 31));

////    [MethodImpl(MethodImplOptions.AggressiveInlining)]
////    private static int UnZigZag32(uint u) => (int)((u >> 1) ^ (uint)-(int)(u & 1));

////    [MethodImpl(MethodImplOptions.AggressiveInlining)]
////    private static int WidthFromZigZag(uint z)
////    {
////        if (z <= 0xFFu) return 1;
////        if (z <= 0xFFFFu) return 2;
////        if (z <= 0xFFFFFFu) return 3;
////        return 4;
////    }

////    [MethodImpl(MethodImplOptions.AggressiveInlining)]
////    private static void WriteLE(List<byte> dst, uint value, int width)
////    {
////        for (int i = 0; i < width; i++)
////            dst.Add((byte)((value >> (8 * i)) & 0xFF));
////    }

////    [MethodImpl(MethodImplOptions.AggressiveInlining)]
////    private static ulong ReadLE(ReadOnlySpan<byte> src, ref int pos, int width)
////    {
////        if ((uint)(pos + width) > (uint)src.Length)
////            throw new ArgumentException("Buffer underflow while reading group payload.");

////        ulong v = 0;
////        for (int i = 0; i < width; i++)
////            v |= (ulong)src[pos++] << (8 * i);
////        return v;
////    }
////}
//using System;
//using System.Collections.Generic;
//using System.Runtime.CompilerServices;

//namespace Esiur.Data.Gvwie;

//public static class GroupInt32Codec
//{
//    private const byte RawInt32RunHeader = 0xFF;

//    // ----------------- Encoder -----------------
//    public static byte[] Encode(IList<int> values)
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
//            int width = WidthFromZigZag(zz);

//            // Detect long full-width run and emit raw Int32 block instead of grouped width=4
//            if (width == 4)
//            {
//                int runCount = 1;

//                while ((i + runCount) < values.Count)
//                {
//                    uint z2 = ZigZag32(values[i + runCount]);

//                    // keep literals separate
//                    if (z2 <= 0x7Fu)
//                        break;

//                    if (WidthFromZigZag(z2) != 4)
//                        break;

//                    runCount++;
//                }

//                // Threshold can be tuned; 33+ is a good starting point
//                if (runCount >= 33)
//                {
//                    dst.Add(RawInt32RunHeader);
//                    WriteVarUInt32(dst, (uint)runCount);

//                    for (int k = 0; k < runCount; k++)
//                        WriteInt32LE(dst, values[start + k]);

//                    i += runCount;
//                    continue;
//                }
//            }

//            // Normal group: up to 32 items sharing the same width (1..4 bytes)
//            int count = 1;

//            while (count < 32 && (i + count) < values.Count)
//            {
//                uint z2 = ZigZag32(values[i + count]);

//                // do not absorb literal-fast-path values into groups
//                if (z2 <= 0x7Fu)
//                    break;

//                int w2 = WidthFromZigZag(z2);
//                if (w2 != width)
//                    break;

//                count++;
//            }

//            // Header: 1 | (count-1)[5 bits] | (width-1)[2 bits]
//            byte header = 0x80;
//            header |= (byte)(((count - 1) & 0x1F) << 2);
//            header |= (byte)((width - 1) & 0x03);
//            dst.Add(header);

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

//            // Raw fixed-width Int32 run
//            if (h == RawInt32RunHeader)
//            {
//                uint countU = ReadVarUInt32(src, ref pos);
//                int count = checked((int)countU);

//                for (int j = 0; j < count; j++)
//                    result.Add(ReadInt32LE(src, ref pos));

//                continue;
//            }

//            int countNormal = ((h >> 2) & 0x1F) + 1; // 1..32
//            int width = (h & 0x03) + 1;              // 1..4

//            for (int j = 0; j < countNormal; j++)
//            {
//                uint raw = (uint)ReadLE(src, ref pos, width);
//                result.Add(UnZigZag32(raw));
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
//    private static int WidthFromZigZag(uint z)
//    {
//        if (z <= 0xFFu) return 1;
//        if (z <= 0xFFFFu) return 2;
//        if (z <= 0xFFFFFFu) return 3;
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
//    private static void WriteInt32LE(List<byte> dst, int value)
//    {
//        uint u = unchecked((uint)value);
//        dst.Add((byte)(u & 0xFF));
//        dst.Add((byte)((u >> 8) & 0xFF));
//        dst.Add((byte)((u >> 16) & 0xFF));
//        dst.Add((byte)((u >> 24) & 0xFF));
//    }

//    [MethodImpl(MethodImplOptions.AggressiveInlining)]
//    private static int ReadInt32LE(ReadOnlySpan<byte> src, ref int pos)
//    {
//        if ((uint)(pos + 4) > (uint)src.Length)
//            throw new ArgumentException("Buffer underflow while reading raw Int32 payload.");

//        uint u =
//            (uint)src[pos]
//            | ((uint)src[pos + 1] << 8)
//            | ((uint)src[pos + 2] << 16)
//            | ((uint)src[pos + 3] << 24);

//        pos += 4;
//        return unchecked((int)u);
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
    private const byte RawInt32RunHeader = 0xFF;
    private const int RawDecisionWindow = 256;

    // ----------------- Encoder -----------------
    public static byte[] Encode(IList<int> values)
    {
        var dst = new List<byte>(values.Count * 2);
        int i = 0;

        while (i < values.Count)
        {
            int remaining = values.Count - i;

            // Adaptive raw block decision on a bounded window
            if (remaining >= 32)
            {
                int candidateCount = Math.Min(RawDecisionWindow, remaining);

                int rawSize = 1 + VarUInt32Size((uint)candidateCount) + candidateCount * 4;
                int groupedSize = EstimateGroupedSize(values, i, candidateCount);

                if (rawSize < groupedSize)
                {
                    dst.Add(RawInt32RunHeader);
                    WriteVarUInt32(dst, (uint)candidateCount);

                    for (int k = 0; k < candidateCount; k++)
                        WriteInt32LE(dst, values[i + k]);

                    i += candidateCount;
                    continue;
                }
            }

            uint zz = ZigZag32(values[i]);

            // Fast path: single byte (MSB=0) when zigzag fits in 7 bits
            if (zz <= 0x7Fu)
            {
                dst.Add((byte)zz);
                i++;
                continue;
            }

            int start = i;
            int width = WidthFromZigZag(zz);
            int count = 1;

            // 0xFF is reserved for raw Int32 blocks, so width=4 groups max out at 31
            int maxGroupCount = (width == 4) ? 31 : 32;

            while (count < maxGroupCount && (i + count) < values.Count)
            {
                uint z2 = ZigZag32(values[i + count]);

                // keep literals separate
                if (z2 <= 0x7Fu)
                    break;

                int w2 = WidthFromZigZag(z2);
                if (w2 != width)
                    break;

                count++;
            }

            // Header: 1 | (count-1)[5 bits] | (width-1)[2 bits]
            byte header = 0x80;
            header |= (byte)(((count - 1) & 0x1F) << 2);
            header |= (byte)((width - 1) & 0x03);
            dst.Add(header);

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

            // Raw fixed-width Int32 run
            if (h == RawInt32RunHeader)
            {
                uint countU = ReadVarUInt32(src, ref pos);
                int count = checked((int)countU);

                for (int j = 0; j < count; j++)
                    result.Add(ReadInt32LE(src, ref pos));

                continue;
            }

            int countNormal = ((h >> 2) & 0x1F) + 1; // 1..32
            int width = (h & 0x03) + 1;              // 1..4

            for (int j = 0; j < countNormal; j++)
            {
                uint raw = (uint)ReadLE(src, ref pos, width);
                result.Add(UnZigZag32(raw));
            }
        }

        return result.ToArray();
    }

    // ----------------- Size Estimation -----------------

    private static int EstimateGroupedSize(IList<int> values, int start, int count)
    {
        int size = 0;
        int i = start;
        int end = start + count;

        while (i < end)
        {
            uint zz = ZigZag32(values[i]);

            if (zz <= 0x7Fu)
            {
                size += 1;
                i++;
                continue;
            }

            int width = WidthFromZigZag(zz);
            int groupCount = 1;
            int maxGroupCount = (width == 4) ? 31 : 32;

            while (groupCount < maxGroupCount && (i + groupCount) < end)
            {
                uint z2 = ZigZag32(values[i + groupCount]);

                if (z2 <= 0x7Fu)
                    break;

                int w2 = WidthFromZigZag(z2);
                if (w2 != width)
                    break;

                groupCount++;
            }

            size += 1 + groupCount * width;
            i += groupCount;
        }

        return size;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int VarUInt32Size(uint value)
    {
        int size = 1;
        while (value >= 0x80)
        {
            value >>= 7;
            size++;
        }
        return size;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteInt32LE(List<byte> dst, int value)
    {
        uint u = unchecked((uint)value);
        dst.Add((byte)(u & 0xFF));
        dst.Add((byte)((u >> 8) & 0xFF));
        dst.Add((byte)((u >> 16) & 0xFF));
        dst.Add((byte)((u >> 24) & 0xFF));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ReadInt32LE(ReadOnlySpan<byte> src, ref int pos)
    {
        if ((uint)(pos + 4) > (uint)src.Length)
            throw new ArgumentException("Buffer underflow while reading raw Int32 payload.");

        uint u =
            (uint)src[pos]
            | ((uint)src[pos + 1] << 8)
            | ((uint)src[pos + 2] << 16)
            | ((uint)src[pos + 3] << 24);

        pos += 4;
        return unchecked((int)u);
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