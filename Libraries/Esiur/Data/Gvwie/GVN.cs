//////////using System;
//////////using System.Collections.Generic;
//////////using System.Runtime.CompilerServices;

//////////namespace Esiur.Data.Gvwie;

//////////public static class GroupInt32Codec
//////////{
//////////    private const byte ExtendedRaw32Header = 0xFF;

//////////    // ----------------- Encoder -----------------
//////////    public static byte[] Encode(IList<int> values)
//////////    {
//////////        var dst = new List<byte>(values.Count * 2);
//////////        int i = 0;

//////////        while (i < values.Count)
//////////        {
//////////            uint zz = ZigZag32(values[i]);

//////////            // Fast path: single byte (MSB=0) when zigzag fits in 7 bits
//////////            if (zz <= 0x7Fu)
//////////            {
//////////                dst.Add((byte)zz);
//////////                i++;
//////////                continue;
//////////            }

//////////            int start = i;
//////////            int width = WidthFromZigZag(zz);

//////////            // Extended raw 32-bit run:
//////////            // 0xFF + varint(count-1) + count * 4-byte LE zigzag payload
//////////            if (width == 4)
//////////            {
//////////                int count = 1;

//////////                while ((i + count) < values.Count)
//////////                {
//////////                    uint z2 = ZigZag32(values[i + count]);
//////////                    if (WidthFromZigZag(z2) != 4)
//////////                        break;

//////////                    count++;
//////////                }

//////////                dst.Add(ExtendedRaw32Header);
//////////                WriteVarUInt32(dst, (uint)(count - 1));

//////////                for (int k = 0; k < count; k++)
//////////                    WriteLE(dst, ZigZag32(values[start + k]), 4);

//////////                i += count;
//////////                continue;
//////////            }

//////////            // Normal group: up to 32 items sharing a common width (1..3 bytes)
//////////            int countNormal = 1;

//////////            while (countNormal < 32 && (i + countNormal) < values.Count)
//////////            {
//////////                uint z2 = ZigZag32(values[i + countNormal]);
//////////                int w2 = WidthFromZigZag(z2);

//////////                // Stop before 4-byte values so extended mode can take them
//////////                if (w2 == 4)
//////////                    break;

//////////                width = Math.Max(width, w2);
//////////                countNormal++;
//////////            }

//////////            // Header: 1 | (count-1)[5 bits] | (width-1)[2 bits]
//////////            byte header = 0x80;
//////////            header |= (byte)(((countNormal - 1) & 0x1F) << 2);
//////////            header |= (byte)((width - 1) & 0x03);
//////////            dst.Add(header);

//////////            for (int k = 0; k < countNormal; k++)
//////////                WriteLE(dst, ZigZag32(values[start + k]), width);

//////////            i += countNormal;
//////////        }

//////////        return dst.ToArray();
//////////    }

//////////    // ----------------- Decoder -----------------
//////////    public static int[] Decode(ReadOnlySpan<byte> src)
//////////    {
//////////        var result = new List<int>();
//////////        int pos = 0;

//////////        while (pos < src.Length)
//////////        {
//////////            byte h = src[pos++];

//////////            if ((h & 0x80) == 0)
//////////            {
//////////                uint zz7 = (uint)(h & 0x7F);
//////////                result.Add(UnZigZag32(zz7));
//////////                continue;
//////////            }

//////////            // Extended raw 32-bit run
//////////            if (h == ExtendedRaw32Header)
//////////            {
//////////                uint countMinus1 = ReadVarUInt32(src, ref pos);
//////////                int count = checked((int)countMinus1 + 1);

//////////                for (int j = 0; j < count; j++)
//////////                {
//////////                    uint raw = (uint)ReadLE(src, ref pos, 4);
//////////                    result.Add(UnZigZag32(raw));
//////////                }

//////////                continue;
//////////            }

//////////            int countNormal = ((h >> 2) & 0x1F) + 1; // 1..32
//////////            int width = (h & 0x03) + 1;              // 1..4 (though encoder uses 1..3 here)

//////////            for (int j = 0; j < countNormal; j++)
//////////            {
//////////                uint raw = (uint)ReadLE(src, ref pos, width);
//////////                result.Add(UnZigZag32(raw));
//////////            }
//////////        }

//////////        return result.ToArray();
//////////    }

//////////    // ----------------- Helpers -----------------

//////////    [MethodImpl(MethodImplOptions.AggressiveInlining)]
//////////    private static uint ZigZag32(int v) => (uint)((v << 1) ^ (v >> 31));

//////////    [MethodImpl(MethodImplOptions.AggressiveInlining)]
//////////    private static int UnZigZag32(uint u) => (int)((u >> 1) ^ (uint)-(int)(u & 1));

//////////    [MethodImpl(MethodImplOptions.AggressiveInlining)]
//////////    private static int WidthFromZigZag(uint z)
//////////    {
//////////        if (z <= 0xFFu) return 1;
//////////        if (z <= 0xFFFFu) return 2;
//////////        if (z <= 0xFFFFFFu) return 3;
//////////        return 4;
//////////    }

//////////    [MethodImpl(MethodImplOptions.AggressiveInlining)]
//////////    private static void WriteLE(List<byte> dst, uint value, int width)
//////////    {
//////////        for (int i = 0; i < width; i++)
//////////            dst.Add((byte)((value >> (8 * i)) & 0xFF));
//////////    }

//////////    [MethodImpl(MethodImplOptions.AggressiveInlining)]
//////////    private static ulong ReadLE(ReadOnlySpan<byte> src, ref int pos, int width)
//////////    {
//////////        if ((uint)(pos + width) > (uint)src.Length)
//////////            throw new ArgumentException("Buffer underflow while reading group payload.");

//////////        ulong v = 0;
//////////        for (int i = 0; i < width; i++)
//////////            v |= (ulong)src[pos++] << (8 * i);
//////////        return v;
//////////    }

//////////    [MethodImpl(MethodImplOptions.AggressiveInlining)]
//////////    private static void WriteVarUInt32(List<byte> dst, uint value)
//////////    {
//////////        while (value >= 0x80)
//////////        {
//////////            dst.Add((byte)(value | 0x80));
//////////            value >>= 7;
//////////        }

//////////        dst.Add((byte)value);
//////////    }

//////////    [MethodImpl(MethodImplOptions.AggressiveInlining)]
//////////    private static uint ReadVarUInt32(ReadOnlySpan<byte> src, ref int pos)
//////////    {
//////////        uint result = 0;
//////////        int shift = 0;

//////////        while (true)
//////////        {
//////////            if (pos >= src.Length)
//////////                throw new ArgumentException("Buffer underflow while reading varint.");

//////////            byte b = src[pos++];
//////////            result |= (uint)(b & 0x7F) << shift;

//////////            if ((b & 0x80) == 0)
//////////                return result;

//////////            shift += 7;
//////////            if (shift >= 35)
//////////                throw new ArgumentException("Varint is too long for UInt32.");
//////////        }
//////////    }
//////////}




////////using System;
////////using System.Collections.Generic;
////////using System.Runtime.CompilerServices;

////////namespace Esiur.Data.Gvwie;

////////public static class GroupInt32Codec
////////{
////////    // ----------------- Encoder -----------------
////////    public static byte[] Encode(IList<int> values)
////////    {
////////        var dst = new List<byte>(values.Count * 2);
////////        int i = 0;

////////        while (i < values.Count)
////////        {
////////            uint zz = ZigZag32(values[i]);

////////            // Fast path: single byte (MSB=0) when zigzag fits in 7 bits
////////            if (zz <= 0x7Fu)
////////            {
////////                dst.Add((byte)zz);
////////                i++;
////////                continue;
////////            }

////////            int start = i;
////////            int width = WidthFromZigZag(zz);
////////            int count = 1;

////////            // Build a run of same-width non-literal values
////////            while ((i + count) < values.Count)
////////            {
////////                uint z2 = ZigZag32(values[i + count]);

////////                // Do not absorb literal-fast-path values into groups
////////                if (z2 <= 0x7Fu)
////////                    break;

////////                int w2 = WidthFromZigZag(z2);
////////                if (w2 != width)
////////                    break;

////////                count++;
////////            }

////////            if (count <= 31)
////////            {
////////                // Short group:
////////                // Header: 1 | (count-1)[5 bits] | (width-1)[2 bits]
////////                byte header = 0x80;
////////                header |= (byte)(((count - 1) & 0x1F) << 2);
////////                header |= (byte)((width - 1) & 0x03);
////////                dst.Add(header);
////////            }
////////            else
////////            {
////////                // Extended group:
////////                // Header: 1 | 11111 | (width-1)[2 bits]
////////                // Followed by varint(count - 32)
////////                byte header = 0x80;
////////                header |= 0x7C; // count bits = 11111
////////                header |= (byte)((width - 1) & 0x03);
////////                dst.Add(header);
////////                WriteVarUInt32(dst, (uint)(count - 32));
////////            }

////////            // Payload: 'count' zigzag values, LE, 'width' bytes each
////////            for (int k = 0; k < count; k++)
////////                WriteLE(dst, ZigZag32(values[start + k]), width);

////////            i += count;
////////        }

////////        return dst.ToArray();
////////    }

////////    // ----------------- Decoder -----------------
////////    public static int[] Decode(ReadOnlySpan<byte> src)
////////    {
////////        var result = new List<int>();
////////        int pos = 0;

////////        while (pos < src.Length)
////////        {
////////            byte h = src[pos++];

////////            if ((h & 0x80) == 0)
////////            {
////////                // Fast path: 7-bit ZigZag in low bits
////////                uint zz7 = (uint)(h & 0x7F);
////////                result.Add(UnZigZag32(zz7));
////////                continue;
////////            }

////////            int countField = (h >> 2) & 0x1F;
////////            int width = (h & 0x03) + 1;

////////            int count;
////////            if (countField == 31)
////////            {
////////                // Extended group length
////////                uint extra = ReadVarUInt32(src, ref pos);
////////                count = checked(32 + (int)extra);
////////            }
////////            else
////////            {
////////                count = countField + 1;
////////            }

////////            for (int j = 0; j < count; j++)
////////            {
////////                uint raw = (uint)ReadLE(src, ref pos, width);
////////                int val = UnZigZag32(raw);
////////                result.Add(val);
////////            }
////////        }

////////        return result.ToArray();
////////    }

////////    // ----------------- Helpers -----------------

////////    [MethodImpl(MethodImplOptions.AggressiveInlining)]
////////    private static uint ZigZag32(int v) => (uint)((v << 1) ^ (v >> 31));

////////    [MethodImpl(MethodImplOptions.AggressiveInlining)]
////////    private static int UnZigZag32(uint u) => (int)((u >> 1) ^ (uint)-(int)(u & 1));

////////    [MethodImpl(MethodImplOptions.AggressiveInlining)]
////////    private static int WidthFromZigZag(uint z)
////////    {
////////        if (z <= 0xFFu) return 1;
////////        if (z <= 0xFFFFu) return 2;
////////        if (z <= 0xFFFFFFu) return 3;
////////        return 4;
////////    }

////////    [MethodImpl(MethodImplOptions.AggressiveInlining)]
////////    private static void WriteLE(List<byte> dst, uint value, int width)
////////    {
////////        for (int i = 0; i < width; i++)
////////            dst.Add((byte)((value >> (8 * i)) & 0xFF));
////////    }

////////    [MethodImpl(MethodImplOptions.AggressiveInlining)]
////////    private static ulong ReadLE(ReadOnlySpan<byte> src, ref int pos, int width)
////////    {
////////        if ((uint)(pos + width) > (uint)src.Length)
////////            throw new ArgumentException("Buffer underflow while reading group payload.");

////////        ulong v = 0;
////////        for (int i = 0; i < width; i++)
////////            v |= (ulong)src[pos++] << (8 * i);
////////        return v;
////////    }

////////    [MethodImpl(MethodImplOptions.AggressiveInlining)]
////////    private static void WriteVarUInt32(List<byte> dst, uint value)
////////    {
////////        while (value >= 0x80)
////////        {
////////            dst.Add((byte)((value & 0x7F) | 0x80));
////////            value >>= 7;
////////        }

////////        dst.Add((byte)value);
////////    }

////////    [MethodImpl(MethodImplOptions.AggressiveInlining)]
////////    private static uint ReadVarUInt32(ReadOnlySpan<byte> src, ref int pos)
////////    {
////////        uint result = 0;
////////        int shift = 0;

////////        while (true)
////////        {
////////            if (pos >= src.Length)
////////                throw new ArgumentException("Buffer underflow while reading varint.");

////////            byte b = src[pos++];
////////            result |= (uint)(b & 0x7F) << shift;

////////            if ((b & 0x80) == 0)
////////                return result;

////////            shift += 7;
////////            if (shift >= 35)
////////                throw new ArgumentException("Varint is too long for UInt32.");
////////        }
////////    }
////////}


//////using System;
//////using System.Collections.Generic;
//////using System.Runtime.CompilerServices;

//////namespace Esiur.Data.Gvwie;

//////public static class GroupInt32Codec
//////{
//////    private const byte ExtendedRaw32Header = 0xFF;

//////    // ----------------- Encoder -----------------
//////    public static byte[] Encode(IList<int> values)
//////    {
//////        var dst = new List<byte>(values.Count * 2);
//////        int i = 0;

//////        while (i < values.Count)
//////        {
//////            uint zz = ZigZag32(values[i]);

//////            // Fast path: single byte (MSB=0) when zigzag fits in 7 bits
//////            if (zz <= 0x7Fu)
//////            {
//////                dst.Add((byte)zz);
//////                i++;
//////                continue;
//////            }

//////            int start = i;
//////            int width = WidthFromZigZag(zz);

//////            // Extended mode only for long width-4 runs
//////            if (width == 4)
//////            {
//////                int runCount = 1;

//////                while ((i + runCount) < values.Count)
//////                {
//////                    uint z2 = ZigZag32(values[i + runCount]);

//////                    // Keep literals separate
//////                    if (z2 <= 0x7Fu)
//////                        break;

//////                    if (WidthFromZigZag(z2) != 4)
//////                        break;

//////                    runCount++;
//////                }

//////                // Use extended mode only when it is actually longer than normal max group
//////                if (runCount > 32)
//////                {
//////                    dst.Add(ExtendedRaw32Header);
//////                    WriteVarUInt32(dst, (uint)(runCount - 33)); // 33 -> 0, 34 -> 1, ...

//////                    for (int k = 0; k < runCount; k++)
//////                        WriteLE(dst, ZigZag32(values[start + k]), 4);

//////                    i += runCount;
//////                    continue;
//////                }
//////            }

//////            // Normal group: up to 32 items sharing a common width (1..4 bytes)
//////            int count = 1;

//////            while (count < 32 && (i + count) < values.Count)
//////            {
//////                uint z2 = ZigZag32(values[i + count]);

//////                // Do not absorb literal-fast-path values into groups
//////                if (z2 <= 0x7Fu)
//////                    break;

//////                int w2 = WidthFromZigZag(z2);
//////                if (w2 != width)
//////                    break;

//////                count++;
//////            }

//////            // Header: 1 | (count-1)[5 bits] | (width-1)[2 bits]
//////            byte header = 0x80;
//////            header |= (byte)(((count - 1) & 0x1F) << 2);
//////            header |= (byte)((width - 1) & 0x03);
//////            dst.Add(header);

//////            for (int k = 0; k < count; k++)
//////                WriteLE(dst, ZigZag32(values[start + k]), width);

//////            i += count;
//////        }

//////        return dst.ToArray();
//////    }

//////    // ----------------- Decoder -----------------
//////    public static int[] Decode(ReadOnlySpan<byte> src)
//////    {
//////        var result = new List<int>();
//////        int pos = 0;

//////        while (pos < src.Length)
//////        {
//////            byte h = src[pos++];

//////            if ((h & 0x80) == 0)
//////            {
//////                // Fast path: 7-bit ZigZag in low bits
//////                uint zz7 = (uint)(h & 0x7F);
//////                result.Add(UnZigZag32(zz7));
//////                continue;
//////            }

//////            // Extended raw width-4 run
//////            if (h == ExtendedRaw32Header)
//////            {
//////                uint extra = ReadVarUInt32(src, ref pos);
//////                int count = checked(33 + (int)extra);

//////                for (int j = 0; j < count; j++)
//////                {
//////                    uint raw = (uint)ReadLE(src, ref pos, 4);
//////                    result.Add(UnZigZag32(raw));
//////                }

//////                continue;
//////            }

//////            int countNormal = ((h >> 2) & 0x1F) + 1; // 1..32
//////            int width = (h & 0x03) + 1;              // 1..4

//////            for (int j = 0; j < countNormal; j++)
//////            {
//////                uint raw = (uint)ReadLE(src, ref pos, width);
//////                result.Add(UnZigZag32(raw));
//////            }
//////        }

//////        return result.ToArray();
//////    }

//////    // ----------------- Helpers -----------------

//////    [MethodImpl(MethodImplOptions.AggressiveInlining)]
//////    private static uint ZigZag32(int v) => (uint)((v << 1) ^ (v >> 31));

//////    [MethodImpl(MethodImplOptions.AggressiveInlining)]
//////    private static int UnZigZag32(uint u) => (int)((u >> 1) ^ (uint)-(int)(u & 1));

//////    [MethodImpl(MethodImplOptions.AggressiveInlining)]
//////    private static int WidthFromZigZag(uint z)
//////    {
//////        if (z <= 0xFFu) return 1;
//////        if (z <= 0xFFFFu) return 2;
//////        if (z <= 0xFFFFFFu) return 3;
//////        return 4;
//////    }

//////    [MethodImpl(MethodImplOptions.AggressiveInlining)]
//////    private static void WriteLE(List<byte> dst, uint value, int width)
//////    {
//////        for (int i = 0; i < width; i++)
//////            dst.Add((byte)((value >> (8 * i)) & 0xFF));
//////    }

//////    [MethodImpl(MethodImplOptions.AggressiveInlining)]
//////    private static ulong ReadLE(ReadOnlySpan<byte> src, ref int pos, int width)
//////    {
//////        if ((uint)(pos + width) > (uint)src.Length)
//////            throw new ArgumentException("Buffer underflow while reading group payload.");

//////        ulong v = 0;
//////        for (int i = 0; i < width; i++)
//////            v |= (ulong)src[pos++] << (8 * i);
//////        return v;
//////    }

//////    [MethodImpl(MethodImplOptions.AggressiveInlining)]
//////    private static void WriteVarUInt32(List<byte> dst, uint value)
//////    {
//////        while (value >= 0x80)
//////        {
//////            dst.Add((byte)((value & 0x7F) | 0x80));
//////            value >>= 7;
//////        }

//////        dst.Add((byte)value);
//////    }

//////    [MethodImpl(MethodImplOptions.AggressiveInlining)]
//////    private static uint ReadVarUInt32(ReadOnlySpan<byte> src, ref int pos)
//////    {
//////        uint result = 0;
//////        int shift = 0;

//////        while (true)
//////        {
//////            if (pos >= src.Length)
//////                throw new ArgumentException("Buffer underflow while reading varint.");

//////            byte b = src[pos++];
//////            result |= (uint)(b & 0x7F) << shift;

//////            if ((b & 0x80) == 0)
//////                return result;

//////            shift += 7;
//////            if (shift >= 35)
//////                throw new ArgumentException("Varint is too long for UInt32.");
//////        }
//////    }
//////}

////using System;
////using System.Collections.Generic;
////using System.Runtime.CompilerServices;

////namespace Esiur.Data.Gvwie;

////public static class GroupInt32Codec
////{
////    private const byte RawInt32RunHeader = 0xFF;

////    // ----------------- Encoder -----------------
////    public static byte[] Encode(IList<int> values)
////    {
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

////            int start = i;
////            int width = WidthFromZigZag(zz);

////            // Detect long full-width run and emit raw Int32 block instead of grouped width=4
////            if (width == 4)
////            {
////                int runCount = 1;

////                while ((i + runCount) < values.Count)
////                {
////                    uint z2 = ZigZag32(values[i + runCount]);

////                    // keep literals separate
////                    if (z2 <= 0x7Fu)
////                        break;

////                    if (WidthFromZigZag(z2) != 4)
////                        break;

////                    runCount++;
////                }

////                // Threshold can be tuned; 33+ is a good starting point
////                if (runCount >= 33)
////                {
////                    dst.Add(RawInt32RunHeader);
////                    WriteVarUInt32(dst, (uint)runCount);

////                    for (int k = 0; k < runCount; k++)
////                        WriteInt32LE(dst, values[start + k]);

////                    i += runCount;
////                    continue;
////                }
////            }

////            // Normal group: up to 32 items sharing the same width (1..4 bytes)
////            int count = 1;

////            while (count < 32 && (i + count) < values.Count)
////            {
////                uint z2 = ZigZag32(values[i + count]);

////                // do not absorb literal-fast-path values into groups
////                if (z2 <= 0x7Fu)
////                    break;

////                int w2 = WidthFromZigZag(z2);
////                if (w2 != width)
////                    break;

////                count++;
////            }

////            // Header: 1 | (count-1)[5 bits] | (width-1)[2 bits]
////            byte header = 0x80;
////            header |= (byte)(((count - 1) & 0x1F) << 2);
////            header |= (byte)((width - 1) & 0x03);
////            dst.Add(header);

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

////            // Raw fixed-width Int32 run
////            if (h == RawInt32RunHeader)
////            {
////                uint countU = ReadVarUInt32(src, ref pos);
////                int count = checked((int)countU);

////                for (int j = 0; j < count; j++)
////                    result.Add(ReadInt32LE(src, ref pos));

////                continue;
////            }

////            int countNormal = ((h >> 2) & 0x1F) + 1; // 1..32
////            int width = (h & 0x03) + 1;              // 1..4

////            for (int j = 0; j < countNormal; j++)
////            {
////                uint raw = (uint)ReadLE(src, ref pos, width);
////                result.Add(UnZigZag32(raw));
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

////    [MethodImpl(MethodImplOptions.AggressiveInlining)]
////    private static void WriteInt32LE(List<byte> dst, int value)
////    {
////        uint u = unchecked((uint)value);
////        dst.Add((byte)(u & 0xFF));
////        dst.Add((byte)((u >> 8) & 0xFF));
////        dst.Add((byte)((u >> 16) & 0xFF));
////        dst.Add((byte)((u >> 24) & 0xFF));
////    }

////    [MethodImpl(MethodImplOptions.AggressiveInlining)]
////    private static int ReadInt32LE(ReadOnlySpan<byte> src, ref int pos)
////    {
////        if ((uint)(pos + 4) > (uint)src.Length)
////            throw new ArgumentException("Buffer underflow while reading raw Int32 payload.");

////        uint u =
////            (uint)src[pos]
////            | ((uint)src[pos + 1] << 8)
////            | ((uint)src[pos + 2] << 16)
////            | ((uint)src[pos + 3] << 24);

////        pos += 4;
////        return unchecked((int)u);
////    }

////    [MethodImpl(MethodImplOptions.AggressiveInlining)]
////    private static void WriteVarUInt32(List<byte> dst, uint value)
////    {
////        while (value >= 0x80)
////        {
////            dst.Add((byte)((value & 0x7F) | 0x80));
////            value >>= 7;
////        }

////        dst.Add((byte)value);
////    }

////    [MethodImpl(MethodImplOptions.AggressiveInlining)]
////    private static uint ReadVarUInt32(ReadOnlySpan<byte> src, ref int pos)
////    {
////        uint result = 0;
////        int shift = 0;

////        while (true)
////        {
////            if (pos >= src.Length)
////                throw new ArgumentException("Buffer underflow while reading varint.");

////            byte b = src[pos++];
////            result |= (uint)(b & 0x7F) << shift;

////            if ((b & 0x80) == 0)
////                return result;

////            shift += 7;
////            if (shift >= 35)
////                throw new ArgumentException("Varint is too long for UInt32.");
////        }
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

    // ----------------- Encoder -----------------
    public static byte[] Encode(IList<int> values)
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
            int width = WidthFromZigZag(zz);

            // Raw fixed-width mode only for a consecutive width-4 run
            if (width == 4)
            {
                int runCount = 1;

                while ((i + runCount) < values.Count)
                {
                    uint z2 = ZigZag32(values[i + runCount]);

                    // keep literals separate
                    if (z2 <= 0x7Fu)
                        break;

                    if (WidthFromZigZag(z2) != 4)
                        break;

                    runCount++;
                }

                // Compare raw run vs grouped run for this exact width-4 span
                int rawSize = 1 + VarUInt32Size((uint)runCount) + runCount * 4;
                int groupedSize = EstimateWidth4GroupedSize(runCount);

                if (rawSize < groupedSize)
                {
                    dst.Add(RawInt32RunHeader);
                    WriteVarUInt32(dst, (uint)runCount);

                    for (int k = 0; k < runCount; k++)
                        WriteInt32LE(dst, values[start + k]);

                    i += runCount;
                    continue;
                }
            }

            // Normal group: up to 32 items sharing the same width
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
                uint zz7 = (uint)(h & 0x7F);
                result.Add(UnZigZag32(zz7));
                continue;
            }

            if (h == RawInt32RunHeader)
            {
                uint countU = ReadVarUInt32(src, ref pos);
                int count = checked((int)countU);

                for (int j = 0; j < count; j++)
                    result.Add(ReadInt32LE(src, ref pos));

                continue;
            }

            int countNormal = ((h >> 2) & 0x1F) + 1;
            int width = (h & 0x03) + 1;

            for (int j = 0; j < countNormal; j++)
            {
                uint raw = (uint)ReadLE(src, ref pos, width);
                result.Add(UnZigZag32(raw));
            }
        }

        return result.ToArray();
    }

    // ----------------- Size helpers -----------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int EstimateWidth4GroupedSize(int count)
    {
        // width=4 groups use max 31 items each because 0xFF is reserved
        int groups = count / 31;
        if ((count % 31) != 0)
            groups++;

        return groups + count * 4;
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