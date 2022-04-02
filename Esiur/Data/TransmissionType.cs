using Esiur.Net.IIP;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Data;

public enum TransmissionTypeIdentifier : byte
{
    Null = 0x0,
    False = 0x1,
    True = 0x2,
    NotModified = 0x3,
    UInt8 = 0x8,
    Int8 = 0x9,
    Char8 = 0xA,
    Int16 = 0x10,
    UInt16 = 0x11,
    Char16 = 0x12,
    Int32 = 0x18,
    UInt32 = 0x19,
    Float32 = 0x1A,
    Resource = 0x1B,
    ResourceLocal = 0x1C,
    Int64 = 0x20,
    UInt64 = 0x21,
    Float64 = 0x22,
    DateTime = 0x23,
    Int128 = 0x28,
    UInt128 = 0x29,
    Float128 = 0x2A,

    RawData = 0x40,
    String = 0x41,
    List = 0x42,
    ResourceList = 0x43,
    RecordList = 0x44,
    Map = 0x45,
    MapList = 0x46,
    //Tuple = 0x47,

    Record = 0x80,
    TypedList = 0x81,
    TypedMap = 0x82,
    Tuple = 0x83,
    Enum = 0x84,
    Constant = 0x85
    //TypedResourceList = 0x81,
    //TypedRecordList = 0x82,

}

public enum TransmissionTypeClass
{
    Fixed = 0,
    Dynamic = 1,
    Typed = 2
}

public struct TransmissionType
{
    public TransmissionTypeIdentifier Identifier;
    public int Index;
    public TransmissionTypeClass Class;
    public uint Offset;
    public ulong ContentLength;
    public byte Exponent;
    

    public TransmissionType(TransmissionTypeIdentifier identifier, TransmissionTypeClass cls, int index, uint offset, ulong contentLength, byte exponent = 0)
    {
        Identifier = identifier;
        Index = index;
        Class = cls;
        Offset=offset;
        ContentLength = contentLength;
        Exponent = exponent;
    }

    public static byte[] Compose(TransmissionTypeIdentifier identifier, byte[] data)
    {

        if (data == null || data.Length == 0)
            return new byte[] { (byte)identifier };

        var cls = (TransmissionTypeClass)((int)identifier >> 6);
        if (cls == TransmissionTypeClass.Fixed)
        {
            return DC.Combine(new byte[] { (byte)identifier }, 0, 1, data, 0, (uint)data.Length);
        }
        else
        {
            var len = (ulong)data.LongLength;

            if (len == 0)
            {
                return new byte[1] { (byte) identifier };
            }
            else if (len <= 0xFF)
            {
                var rt = new byte[2 + len];
                rt[0] = (byte)((byte)identifier | 0x8);
                rt[1] = (byte)len;
                Buffer.BlockCopy(data, 0, rt, 2, (int)len);
                return rt;
            }
            else if (len <= 0xFF_FF)
            {
                var rt = new byte[3 + len];
                rt[0] = (byte)((byte)identifier | 0x10);
                rt[1] = (byte)((len >> 8) & 0xFF);
                rt[2] = (byte)(len & 0xFF);   
                Buffer.BlockCopy(data, 0, rt, 3, (int)len);
                return rt;
            }
            else if (len <= 0xFF_FF_FF)
            {
                var rt = new byte[4 + len];
                rt[0] = (byte)((byte)identifier | 0x18);
                rt[1] = (byte)((len >> 16) & 0xFF);
                rt[2] = (byte)((len >> 8) & 0xFF);
                rt[3] = (byte)(len & 0xFF);
                Buffer.BlockCopy(data, 0, rt, 4, (int)len);
                return rt;
            }
            else if (len <= 0xFF_FF_FF_FF)
            {
                var rt = new byte[5 + len];
                rt[0] = (byte)((byte)identifier | 0x20);
                rt[1] = (byte)((len >> 24) & 0xFF);
                rt[2] = (byte)((len >> 16) & 0xFF);
                rt[3] = (byte)((len >> 8) & 0xFF);
                rt[4] = (byte)(len & 0xFF);
                Buffer.BlockCopy(data, 0, rt, 5, (int)len);
                return rt;
            }
            else if (len <= 0xFF_FF_FF_FF_FF)
            {
                var rt = new byte[6 + len];
                rt[0] = (byte)((byte)identifier | 0x28);
                rt[1] = (byte)((len >> 32) & 0xFF);
                rt[2] = (byte)((len >> 24) & 0xFF);
                rt[3] = (byte)((len >> 16) & 0xFF);
                rt[4] = (byte)((len >> 8) & 0xFF);
                rt[5] = (byte)(len & 0xFF);
                Buffer.BlockCopy(data, 0, rt, 6, (int)len);
                return rt;
            }
            else if (len <= 0xFF_FF_FF_FF_FF_FF)
            {
                var rt = new byte[7 + len];
                rt[0] = (byte)((byte)identifier | 0x30);
                rt[1] = (byte)((len >> 40) & 0xFF);
                rt[2] = (byte)((len >> 32) & 0xFF);
                rt[3] = (byte)((len >> 24) & 0xFF);
                rt[4] = (byte)((len >> 16) & 0xFF);
                rt[5] = (byte)((len >> 8) & 0xFF);
                rt[6] = (byte)(len & 0xFF);
                Buffer.BlockCopy(data, 0, rt, 7, (int)len);
                return rt;
            }
            else //if (len <= 0xFF_FF_FF_FF_FF_FF_FF)
            {
                var rt = new byte[8 + len];
                rt[0] = (byte)((byte)identifier | 0x38);
                rt[1] = (byte)((len >> 48) & 0xFF);
                rt[2] = (byte)((len >> 40) & 0xFF);
                rt[3] = (byte)((len >> 32) & 0xFF);
                rt[4] = (byte)((len >> 24) & 0xFF);
                rt[5] = (byte)((len >> 16) & 0xFF);
                rt[6] = (byte)((len >> 8) & 0xFF);
                rt[7] = (byte)(len & 0xFF);
                Buffer.BlockCopy(data, 0, rt, 8, (int)len);
                return rt;
            }
            //else // if (len <= 0xFF_FF_FF_FF_FF_FF_FF_FF)
            //{
            //    var rt = new byte[9 + len];
            //    rt[0] = (byte)((byte)identifier | 0x8);
            //    rt[1] = (byte)((len >> 56) & 0xFF);
            //    rt[2] = (byte)((len >> 48) & 0xFF);
            //    rt[3] = (byte)((len >> 40) & 0xFF);
            //    rt[4] = (byte)((len >> 32) & 0xFF);
            //    rt[5] = (byte)((len >> 24) & 0xFF);
            //    rt[6] = (byte)((len >> 16) & 0xFF);
            //    rt[7] = (byte)((len >> 8) & 0xFF);
            //    rt[8] = (byte)(len & 0xFF);
            //    Buffer.BlockCopy(data, 0, rt, 9, (int)len);
            //    return rt;
            //}


            //    // add length
            //    int bytes = 1;
            //for (var i = 56; i > 0; i -= 8, bytes++)
            //    if (len <= (0xFF_FF_FF_FF_FF_FF_FF_FF >> i))
            //        break;     

            //var rt = new byte[1 + bytes + data.Length];
            //rt[0] = (byte)((byte)identifier | (bytes << 3));

            //for (var i = 1; i <= bytes; i++)
            //    rt[i] = data.LongLength >> i * 8;

            //Buffer.BlockCopy(data, 0, rt, 1 + bytes, data.Length);
        }
    }

    public static (ulong, TransmissionType?) Parse(byte[] data, uint offset, uint ends)
    {
        var h = data[offset++];
 
        var cls = (TransmissionTypeClass)(h >> 6);

        if (cls == TransmissionTypeClass.Fixed)
        {
            var exp = (h & 0x38) >> 3;

            if (exp == 0)
                return (1, new TransmissionType((TransmissionTypeIdentifier)h, cls, h & 0x7, 0, (byte)exp));

            ulong cl = (ulong)(1 << (exp -1));

            if (ends - offset < cl)  
                return (cl - (ends - offset), null);

            //offset += (uint)cl;

            return (1 + cl, new TransmissionType((TransmissionTypeIdentifier)h, cls, h & 0x7, offset, cl, (byte)exp));
        }
        else
        {
            ulong cll = (ulong)(h >> 3) & 0x7;

            if (ends - offset < cll)
                return (cll - (ends - offset), null);

            ulong cl = 0;
             
            for (uint i = 0; i < cll; i++)
                cl = cl << 8 | data[offset++];

            if (ends - offset < cl)
                return (cl - (ends - offset), null);

            return (1 + cl + cll, new TransmissionType((TransmissionTypeIdentifier)(h & 0xC7), cls, h & 0x7, offset, cl));
        }
    }

}
