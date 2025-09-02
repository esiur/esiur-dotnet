using Esiur.Net.IIP;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Data;


public struct TransmissionDataUnit
{
    public TransmissionDataUnitIdentifier Identifier;
    public int Index;
    public TransmissionDataUnitClass Class;
    public uint Offset;
    public ulong ContentLength;
    public byte Exponent;
    public byte[] Data;
    
    public TransmissionDataUnit(byte[] data, TransmissionDataUnitIdentifier identifier, 
                                TransmissionDataUnitClass cls, int index, uint offset, 
                                ulong contentLength, byte exponent = 0)
    {
        Identifier = identifier;
        Index = index;
        Class = cls;
        Offset=offset;
        ContentLength = contentLength;
        Exponent = exponent;
        Data = data;
    }

    public byte[] GetTypeMetadata()
    {
        if (Class != TransmissionDataUnitClass.Typed)
            throw new Exception("Class has no metadata.");

        var size = Data[Offset];
        return Data.Clip(Offset + 1, size);

    }

    public static byte[] Compose(TransmissionDataUnitIdentifier identifier, byte[] data, byte[] typeMetadata)
    {

        if (data == null || data.Length == 0)
            return new byte[] { (byte)identifier };

        var cls = (TransmissionDataUnitClass)((int)identifier >> 6);
        if (cls == TransmissionDataUnitClass.Fixed)
        {
            return DC.Combine(new byte[] { (byte)identifier }, 0, 1, data, 0, (uint)data.Length);
        }
        else if (cls == TransmissionDataUnitClass.Dynamic
            || cls == TransmissionDataUnitClass.Extension)
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
        }
        else if (cls == TransmissionDataUnitClass.Typed)
        {
            
            var len = 1 + (ulong)typeMetadata.LongLength + (ulong)data.LongLength;

            if (len == 0)
            {
                return new byte[1] { (byte)identifier };
            }
            else if (len <= 0xFF)
            {
                var rt = new byte[2 + len];
                rt[0] = (byte)((byte)identifier | 0x8);
                rt[1] = (byte)len;

                Buffer.BlockCopy(typeMetadata, 0, rt, 2, typeMetadata.Length);
                Buffer.BlockCopy(data, 0, rt, 2 + typeMetadata.Length, data.Length);
                return rt;
            }
            else if (len <= 0xFF_FF)
            {
                var rt = new byte[3 + len];
                rt[0] = (byte)((byte)identifier | 0x10);
                rt[1] = (byte)((len >> 8) & 0xFF);
                rt[2] = (byte)(len & 0xFF);

                Buffer.BlockCopy(typeMetadata, 0, rt, 3, typeMetadata.Length);
                Buffer.BlockCopy(data, 0, rt, 3 + typeMetadata.Length, data.Length);
                return rt;
            }
            else if (len <= 0xFF_FF_FF)
            {
                var rt = new byte[4 + len];
                rt[0] = (byte)((byte)identifier | 0x18);
                rt[1] = (byte)((len >> 16) & 0xFF);
                rt[2] = (byte)((len >> 8) & 0xFF);
                rt[3] = (byte)(len & 0xFF);

                Buffer.BlockCopy(typeMetadata, 0, rt, 4, typeMetadata.Length);
                Buffer.BlockCopy(data, 0, rt, 4 + typeMetadata.Length, data.Length);

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

                Buffer.BlockCopy(typeMetadata, 0, rt, 5, typeMetadata.Length);
                Buffer.BlockCopy(data, 0, rt, 5 + typeMetadata.Length, data.Length);

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

                Buffer.BlockCopy(typeMetadata, 0, rt, 6, typeMetadata.Length);
                Buffer.BlockCopy(data, 0, rt, 6 + typeMetadata.Length, data.Length);

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

                Buffer.BlockCopy(typeMetadata, 0, rt, 7, typeMetadata.Length);
                Buffer.BlockCopy(data, 0, rt, 7 + typeMetadata.Length, data.Length);

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

                Buffer.BlockCopy(typeMetadata, 0, rt, 8, typeMetadata.Length);
                Buffer.BlockCopy(data, 0, rt, 8 + typeMetadata.Length, data.Length);

                return rt;
            }
        }

        throw new Exception("Not supported class type.");
    }

    public static (ulong, TransmissionDataUnit?) Parse(byte[] data, uint offset, uint ends)
    {
        var h = data[offset++];
 
        var cls = (TransmissionDataUnitClass)(h >> 6);

        if (cls == TransmissionDataUnitClass.Fixed)
        {
            var exp = (h & 0x38) >> 3;

            if (exp == 0)
                return (1, new TransmissionDataUnit(data, (TransmissionDataUnitIdentifier)h, cls, h & 0x7, 0, (byte)exp));

            ulong cl = (ulong)(1 << (exp -1));

            if (ends - offset < cl)  
                return (cl - (ends - offset), null);

            //offset += (uint)cl;

            return (1 + cl, new TransmissionDataUnit(data, (TransmissionDataUnitIdentifier)h, cls, h & 0x7, offset, cl, (byte)exp));
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

            
            return (1 + cl + cll, new TransmissionDataUnit(data, (TransmissionDataUnitIdentifier)(h & 0xC7), cls, h & 0x7, offset, cl));
        }
    }

}
