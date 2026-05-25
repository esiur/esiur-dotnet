using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

#nullable enable

namespace Esiur.Tests.ComplexModel;

// ============================================================================
// Xcdr2Stream.cs
// ----------------------------------------------------------------------------
// OMG Extended Common Data Representation (XCDR) Version 2 encoder/decoder.
//
// Implements PLAIN_CDR2 for FINAL-extensibility structures, the most compact
// XCDR2 mode defined by DDS-XTypes 1.3 (OMG formal/2024-04-01). This mode is
// the on-the-wire format used by every conformant DDS implementation when
// the @final annotation is applied (or no extensibility annotation is given
// and the implementation defaults to final).
//
// Implemented rules (DDS-XTypes 1.3, §7.4.3.5.3 Complete Serialization Rules):
//   - Encapsulation header (4 bytes): representation_identifier (2B) +
//     options (2B). We use CDR2_LE = 0x00 0x09 for the identifier and
//     0x00 0x00 for the options field.
//   - Maximum alignment is 4 bytes (vs 8 in XCDR1). 64-bit primitives align
//     to 4, not 8.
//   - Strings: uint32 length-including-null + UTF-8 bytes + 0x00 terminator.
//   - Sequences of primitives: uint32 length + elements (rule 14, no DHEADER).
//   - Sequences of non-primitives: DHEADER (uint32, bytes-remaining) +
//     uint32 length + elements (rule 15).
//   - Optionals: 1-byte is_present + value if present (rule 9).
//   - Unions (Variant): int32 discriminator aligned to 4 + selected branch.
//   - Octet arrays of fixed length: emitted as-is, no length prefix.
//
// Reference implementations consulted:
//   - foxglove/cdr  (https://github.com/foxglove/cdr)
//   - eclipse-cyclonedds dds_cdrstream.c
//   - eProsima Fast-CDR
// ============================================================================

internal sealed class Xcdr2Writer
{
    private byte[] _buf;
    private int _pos;

    public Xcdr2Writer(int capacity = 4096)
    {
        _buf = new byte[capacity];
        _pos = 0;
        WriteEncapsulationHeader();
    }

    public int Position => _pos;

    public byte[] ToArray()
    {
        var result = new byte[_pos];
        Buffer.BlockCopy(_buf, 0, result, 0, _pos);
        return result;
    }

    private void WriteEncapsulationHeader()
    {
        // CDR2_LE representation_identifier (DDS-RTPS table 10.3)
        Ensure(4);
        _buf[_pos++] = 0x00;
        _buf[_pos++] = 0x09;
        _buf[_pos++] = 0x00;
        _buf[_pos++] = 0x00;
    }

    // The encapsulation header is NOT counted when computing alignment, per
    // DDS-XTypes 1.3 §7.4.3.4: alignment is measured from the start of the
    // user payload (byte 4).
    private int PayloadPos => _pos - 4;

    private void Align(int n)
    {
        // XCDR2 caps max alignment at 4. Callers pass 1, 2, 4 only.
        int mis = PayloadPos & (n - 1);
        if (mis == 0) return;
        int pad = n - mis;
        Ensure(pad);
        for (int i = 0; i < pad; i++) _buf[_pos++] = 0x00;
    }

    private void Ensure(int extra)
    {
        if (_pos + extra <= _buf.Length) return;
        int newCap = _buf.Length * 2;
        while (newCap < _pos + extra) newCap *= 2;
        var nb = new byte[newCap];
        Buffer.BlockCopy(_buf, 0, nb, 0, _pos);
        _buf = nb;
    }

    // ---- primitive writers ----

    public void WriteByte(byte v)
    {
        Ensure(1);
        _buf[_pos++] = v;
    }

    public void WriteBool(bool v) => WriteByte(v ? (byte)1 : (byte)0);

    public void WriteInt16(short v)
    {
        Align(2);
        Ensure(2);
        BinaryPrimitives.WriteInt16LittleEndian(_buf.AsSpan(_pos), v);
        _pos += 2;
    }

    public void WriteUInt16(ushort v)
    {
        Align(2);
        Ensure(2);
        BinaryPrimitives.WriteUInt16LittleEndian(_buf.AsSpan(_pos), v);
        _pos += 2;
    }

    public void WriteInt32(int v)
    {
        Align(4);
        Ensure(4);
        BinaryPrimitives.WriteInt32LittleEndian(_buf.AsSpan(_pos), v);
        _pos += 4;
    }

    public void WriteUInt32(uint v)
    {
        Align(4);
        Ensure(4);
        BinaryPrimitives.WriteUInt32LittleEndian(_buf.AsSpan(_pos), v);
        _pos += 4;
    }

    // XCDR2: 64-bit primitives align to 4, NOT 8 (per max-alignment rule)
    public void WriteInt64(long v)
    {
        Align(4);
        Ensure(8);
        BinaryPrimitives.WriteInt64LittleEndian(_buf.AsSpan(_pos), v);
        _pos += 8;
    }

    public void WriteUInt64(ulong v)
    {
        Align(4);
        Ensure(8);
        BinaryPrimitives.WriteUInt64LittleEndian(_buf.AsSpan(_pos), v);
        _pos += 8;
    }

    public void WriteDouble(double v) => WriteInt64(BitConverter.DoubleToInt64Bits(v));

    public void WriteString(string s)
    {
        var bytes = Encoding.UTF8.GetBytes(s);
        WriteUInt32((uint)(bytes.Length + 1)); // includes null terminator
        Ensure(bytes.Length + 1);
        Buffer.BlockCopy(bytes, 0, _buf, _pos, bytes.Length);
        _pos += bytes.Length;
        _buf[_pos++] = 0x00; // null terminator
    }

    // Fixed-length octet array (e.g., 16-byte GUID).
    // No length prefix; just the bytes.
    public void WriteOctetArrayFixed(byte[] data, int expectedLen)
    {
        if (data.Length != expectedLen)
            throw new ArgumentException($"Expected {expectedLen} bytes, got {data.Length}");
        Ensure(expectedLen);
        Buffer.BlockCopy(data, 0, _buf, _pos, expectedLen);
        _pos += expectedLen;
    }

    // Variable-length octet sequence: uint32 length + bytes.
    // No DHEADER (octet is primitive).
    public void WriteOctetSequence(byte[] data)
    {
        WriteUInt32((uint)data.Length);
        Ensure(data.Length);
        Buffer.BlockCopy(data, 0, _buf, _pos, data.Length);
        _pos += data.Length;
    }

    // DHEADER for sequences of non-primitive types and for non-final structs
    // and for optionals containing complex types. Reserves 4 bytes now,
    // returns a token used by EndDHeader to backfill the size.
    public int BeginDHeader()
    {
        Align(4);
        Ensure(4);
        int token = _pos;
        // placeholder, will be backfilled
        _buf[_pos++] = 0; _buf[_pos++] = 0; _buf[_pos++] = 0; _buf[_pos++] = 0;
        return token;
    }

    public void EndDHeader(int token)
    {
        // Size = number of bytes after the DHEADER, exclusive of the DHEADER
        // itself. (DDS-XTypes 1.3 §7.4.3.5.1 D-HEADER definition.)
        int sizeAfter = _pos - (token + 4);
        BinaryPrimitives.WriteUInt32LittleEndian(_buf.AsSpan(token), (uint)sizeAfter);
    }
}

internal sealed class Xcdr2Reader
{
    private readonly byte[] _buf;
    private int _pos;
    private readonly bool _littleEndian;

    public Xcdr2Reader(byte[] data)
    {
        _buf = data;
        // Encapsulation header (4 bytes)
        if (data.Length < 4) throw new IOException("Truncated XCDR2 stream");
        if (data[0] != 0x00 || (data[1] != 0x09 && data[1] != 0x0A))
            throw new IOException(
                $"Not an XCDR2 stream (representation_identifier {data[0]:X2} {data[1]:X2})");
        _littleEndian = data[1] == 0x09;
        if (!_littleEndian)
            throw new NotSupportedException("Only CDR2_LE is implemented in this benchmark.");
        _pos = 4;
    }

    private int PayloadPos => _pos - 4;

    private void Align(int n)
    {
        int mis = PayloadPos & (n - 1);
        if (mis == 0) return;
        _pos += (n - mis);
    }

    public byte ReadByte() => _buf[_pos++];

    public bool ReadBool() => ReadByte() != 0;

    public short ReadInt16()
    {
        Align(2);
        var v = BinaryPrimitives.ReadInt16LittleEndian(_buf.AsSpan(_pos));
        _pos += 2;
        return v;
    }

    public ushort ReadUInt16()
    {
        Align(2);
        var v = BinaryPrimitives.ReadUInt16LittleEndian(_buf.AsSpan(_pos));
        _pos += 2;
        return v;
    }

    public int ReadInt32()
    {
        Align(4);
        var v = BinaryPrimitives.ReadInt32LittleEndian(_buf.AsSpan(_pos));
        _pos += 4;
        return v;
    }

    public uint ReadUInt32()
    {
        Align(4);
        var v = BinaryPrimitives.ReadUInt32LittleEndian(_buf.AsSpan(_pos));
        _pos += 4;
        return v;
    }

    public long ReadInt64()
    {
        Align(4); // XCDR2 max alignment
        var v = BinaryPrimitives.ReadInt64LittleEndian(_buf.AsSpan(_pos));
        _pos += 8;
        return v;
    }

    public ulong ReadUInt64()
    {
        Align(4);
        var v = BinaryPrimitives.ReadUInt64LittleEndian(_buf.AsSpan(_pos));
        _pos += 8;
        return v;
    }

    public double ReadDouble() => BitConverter.Int64BitsToDouble(ReadInt64());

    public string ReadString()
    {
        uint lenIncNull = ReadUInt32();
        if (lenIncNull == 0)
            throw new IOException("XCDR2 string length must include null terminator (>= 1)");
        int payloadLen = (int)lenIncNull - 1;
        var s = Encoding.UTF8.GetString(_buf, _pos, payloadLen);
        _pos += payloadLen;
        // consume null terminator
        if (_buf[_pos] != 0x00)
            throw new IOException("XCDR2 string missing null terminator");
        _pos += 1;
        return s;
    }

    public byte[] ReadOctetArrayFixed(int len)
    {
        var result = new byte[len];
        Buffer.BlockCopy(_buf, _pos, result, 0, len);
        _pos += len;
        return result;
    }

    public byte[] ReadOctetSequence()
    {
        uint len = ReadUInt32();
        var result = new byte[len];
        Buffer.BlockCopy(_buf, _pos, result, 0, (int)len);
        _pos += (int)len;
        return result;
    }

    public int ReadDHeader()
    {
        // We don't actually need to use the size for decoding because we know
        // the schema; we just consume the 4 bytes.
        return (int)ReadUInt32();
    }
}