/*
 
Copyright (c) 2017 Ahmed Kh. Zamil

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/

using System;

namespace Esiur.Net;

public class NetworkBuffer
{
    private static readonly byte[] Empty = Array.Empty<byte>();

    private readonly object syncLock = new object();
    private byte[] buffer = Empty;
    private int start;
    private int length;
    private uint neededDataLength;

    public bool Protected
    {
        get
        {
            lock (syncLock)
                return (uint)length < neededDataLength;
        }
    }

    public uint Available
    {
        get
        {
            lock (syncLock)
                return (uint)length;
        }
    }

    public void HoldForNextWrite(byte[] src)
    {
        if (src == null)
            throw new ArgumentNullException(nameof(src));

        HoldFor(src, 0, (uint)src.Length, CheckedNextLength((uint)src.Length));
    }

    public void HoldForNextWrite(byte[] src, uint offset, uint size)
        => HoldFor(src, offset, size, CheckedNextLength(size));

    public void HoldFor(byte[] src, uint offset, uint size, uint needed)
    {
        ValidateRange(src, offset, size);
        ValidateNeededLength(needed);

        if (size >= needed)
            // Preserve the historical exception contract for this semantic error.
            throw new Exception("Size >= Needed !");

        lock (syncLock)
        {
            Prepend(src, (int)offset, (int)size);
            neededDataLength = needed;
        }
    }

    public void HoldFor(byte[] src, uint needed)
    {
        if (src == null)
            throw new ArgumentNullException(nameof(src));

        HoldFor(src, 0, (uint)src.Length, needed);
    }

    public bool Protect(byte[] data, uint offset, uint needed)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        if (offset > (uint)data.Length)
            throw new ArgumentOutOfRangeException(nameof(offset));

        ValidateNeededLength(needed);

        var dataLength = (uint)data.Length - offset;
        if (dataLength >= needed)
            return false;

        // HoldFor validates that dataLength is strictly less than needed.
        HoldFor(data, offset, dataLength, needed);
        return true;
    }

    public void Write(byte[] src)
    {
        if (src == null)
            throw new ArgumentNullException(nameof(src));

        Write(src, 0, (uint)src.Length);
    }

    public void Write(byte[] src, uint offset, uint length)
    {
        ValidateRange(src, offset, length);
        if (length == 0)
            return;

        lock (syncLock)
            Append(src, (int)offset, (int)length);
    }

    public bool CanRead
    {
        get
        {
            lock (syncLock)
                return length != 0 && (uint)length >= neededDataLength;
        }
    }

    public byte[] Read()
    {
        lock (syncLock)
        {
            if (length == 0 || (uint)length < neededDataLength)
                return null;

            byte[] result;

            // A single write, and geometrically grown power-of-two payloads, can transfer
            // ownership without one final copy. Otherwise return an exact-size array, as the
            // historical API did, and release the working buffer.
            if (start == 0 && length == buffer.Length)
            {
                result = buffer;
            }
            else
            {
                result = new byte[length];
                Buffer.BlockCopy(buffer, start, result, 0, length);
            }

            buffer = Empty;
            start = 0;
            length = 0;
            neededDataLength = 0;
            return result;
        }
    }

    private void Append(byte[] src, int offset, int count)
    {
        if (length == 0 && buffer.Length == 0)
        {
            // The overwhelmingly common path is one socket read followed by one Read().
            // Allocate exactly once so Read can transfer this array without copying it.
            buffer = new byte[count];
            Buffer.BlockCopy(src, offset, buffer, 0, count);
            start = 0;
            length = count;
            return;
        }

        var requiredLength = CheckedCombinedLength(length, count);
        var writeOffset = start + length;

        if (buffer.Length - writeOffset < count)
        {
            if (requiredLength <= buffer.Length)
            {
                // Reuse headroom left by a prepend operation.
                Buffer.BlockCopy(buffer, start, buffer, 0, length);
                start = 0;
            }
            else
            {
                Grow(requiredLength, prependLength: 0);
            }

            writeOffset = start + length;
        }

        Buffer.BlockCopy(src, offset, buffer, writeOffset, count);
        length = requiredLength;
    }

    private void Prepend(byte[] src, int offset, int count)
    {
        if (count == 0)
            return;

        if (length == 0 && buffer.Length == 0)
        {
            buffer = new byte[count];
            Buffer.BlockCopy(src, offset, buffer, 0, count);
            start = 0;
            length = count;
            return;
        }

        var requiredLength = CheckedCombinedLength(length, count);

        if (start >= count)
        {
            start -= count;
        }
        else if (requiredLength <= buffer.Length)
        {
            // Re-center the live region once and leave any remaining spare capacity around it.
            var combinedStart = (buffer.Length - requiredLength) / 2;
            Buffer.BlockCopy(buffer, start, buffer, combinedStart + count, length);
            start = combinedStart;
        }
        else
        {
            Grow(requiredLength, count);
        }

        Buffer.BlockCopy(src, offset, buffer, start, count);
        length = requiredLength;
    }

    private void Grow(int requiredLength, int prependLength)
    {
        var newCapacity = GetExpandedCapacity(buffer.Length, requiredLength);
        var replacement = new byte[newCapacity];

        if (prependLength == 0)
        {
            if (length > 0)
                Buffer.BlockCopy(buffer, start, replacement, 0, length);
            start = 0;
        }
        else
        {
            var combinedStart = (newCapacity - requiredLength) / 2;
            if (length > 0)
                Buffer.BlockCopy(buffer, start, replacement, combinedStart + prependLength, length);
            start = combinedStart;
        }

        buffer = replacement;
    }

    private static int GetExpandedCapacity(int currentCapacity, int requiredLength)
    {
        var capacity = currentCapacity == 0 ? 256 : currentCapacity;

        while (capacity < requiredLength)
        {
            if (capacity > int.MaxValue / 2)
                return requiredLength;

            capacity *= 2;
        }

        return capacity;
    }

    private static int CheckedCombinedLength(int currentLength, int additionalLength)
    {
        if (additionalLength > int.MaxValue - currentLength)
            throw new ArgumentOutOfRangeException(
                nameof(additionalLength),
                "The buffered data exceeds the maximum managed array length.");

        return currentLength + additionalLength;
    }

    private static uint CheckedNextLength(uint size)
    {
        if (size >= int.MaxValue)
            throw new ArgumentOutOfRangeException(
                nameof(size),
                "The requested held length exceeds the maximum managed array length.");

        return size + 1;
    }

    private static void ValidateNeededLength(uint needed)
    {
        if (needed > int.MaxValue)
            throw new ArgumentOutOfRangeException(
                nameof(needed),
                "The requested held length exceeds the maximum managed array length.");
    }

    private static void ValidateRange(byte[] src, uint offset, uint count)
    {
        if (src == null)
            throw new ArgumentNullException(nameof(src));
        if (offset > (uint)src.Length)
            throw new ArgumentOutOfRangeException(nameof(offset));
        if (count > (uint)src.Length - offset)
            throw new ArgumentOutOfRangeException(nameof(count));
    }
}
