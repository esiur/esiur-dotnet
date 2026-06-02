using Esiur.Protocol;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;
using System.Xml.Schema;
using static Esiur.Data.Codec;

namespace Esiur.Data;

// Transmission Data Unit
public struct Tdu
{
    public TduIdentifier Identifier;
    //public int Index;
    public TduClass Class;
    //public ulong ContentLength;

    public byte[] Composed;
    //public uint Offset;

    public Tru Metadata;

    public uint ContentOffset;

    //public ulong Size
    //{
    //    get
    //    {
    //        if (TotalSize != ulong.MaxValue)
    //            return TotalSize;
    //        else
    //        {
    //            if (ContentLength <= 0xFF)
    //                return 2 + ContentLength;
    //            else if (ContentLength <= 0xFF_FF)
    //                return 3 + ContentLength;
    //            else if (ContentLength <= 0xFF_FF_FF)
    //                return 4 + ContentLength;
    //            else if (ContentLength <= 0xFF_FF_FF_FF)
    //                return 5 + ContentLength;
    //            else if (ContentLength <= 0xFF_FF_FF_FF_FF)
    //                return 6 + ContentLength;
    //            else if (ContentLength <= 0xFF_FF_FF_FF_FF_FF)
    //                return 7 + ContentLength;
    //            else if (ContentLength <= 0xFF_FF_FF_FF_FF_FF_FF)
    //                return 8 + ContentLength;
    //            else //if (ContentLength <= 0xFF_FF_FF_FF_FF_FF_FF_FF)
    //                return 9 + ContentLength;
    //        }
    //    }
    //}

    //private ulong TotalSize;

    public Tdu()
    {

    }

    public Tdu(TduIdentifier identifier)
    {
        Identifier = identifier;
        Composed = new byte[0];
    }

    public Tdu(TduIdentifier identifier,
                                byte[]? data, ulong length, Tru? metadata, EpConnection? connection)
    {
        Identifier = identifier;
        //Index = (byte)identifier & 0x7;
        Class = (TduClass)((byte)identifier >> 6);
        Metadata = metadata;


        if (Class == TduClass.Fixed)
        {
            if (length == 0)
                Composed = new byte[1] { (byte)identifier };
            else
                Composed = DC.Combine(new byte[] { (byte)Identifier }, 0, 1, data, 0, (uint)length);
        }
        else if (Class == TduClass.Dynamic
            || Class == TduClass.Extension)
        {

            if (length == 0)
            {
                Composed = new byte[1] { (byte)Identifier };
            }
            else if (length <= 0xFF)
            {
                Composed = new byte[2 + length];
                Composed[0] = (byte)((byte)Identifier | 0x8);
                Composed[1] = (byte)length;
                ContentOffset = 2;
                Buffer.BlockCopy(data, 0, Composed, 2, (int)length);
            }
            else if (length <= 0xFF_FF)
            {
                Composed = new byte[3 + length];
                Composed[0] = (byte)((byte)Identifier | 0x10);
                Composed[1] = (byte)((length >> 8) & 0xFF);
                Composed[2] = (byte)(length & 0xFF);
                ContentOffset = 3;

                Buffer.BlockCopy(data, 0, Composed, 3, (int)length);
            }
            else if (length <= 0xFF_FF_FF)
            {
                Composed = new byte[4 + length];
                Composed[0] = (byte)((byte)Identifier | 0x18);
                Composed[1] = (byte)((length >> 16) & 0xFF);
                Composed[2] = (byte)((length >> 8) & 0xFF);
                Composed[3] = (byte)(length & 0xFF);
                ContentOffset = 4;

                Buffer.BlockCopy(data, 0, Composed, 4, (int)length);
            }
            else if (length <= 0xFF_FF_FF_FF)
            {
                Composed = new byte[5 + length];
                Composed[0] = (byte)((byte)Identifier | 0x20);
                Composed[1] = (byte)((length >> 24) & 0xFF);
                Composed[2] = (byte)((length >> 16) & 0xFF);
                Composed[3] = (byte)((length >> 8) & 0xFF);
                Composed[4] = (byte)(length & 0xFF);
                ContentOffset = 5;

                Buffer.BlockCopy(data, 0, Composed, 5, (int)length);
            }
            else if (length <= 0xFF_FF_FF_FF_FF)
            {
                Composed = new byte[6 + length];
                Composed[0] = (byte)((byte)Identifier | 0x28);
                Composed[1] = (byte)((length >> 32) & 0xFF);
                Composed[2] = (byte)((length >> 24) & 0xFF);
                Composed[3] = (byte)((length >> 16) & 0xFF);
                Composed[4] = (byte)((length >> 8) & 0xFF);
                Composed[5] = (byte)(length & 0xFF);
                ContentOffset = 6;

                Buffer.BlockCopy(data, 0, Composed, 6, (int)length);
            }
            else if (length <= 0xFF_FF_FF_FF_FF_FF)
            {
                Composed = new byte[7 + length];
                Composed[0] = (byte)((byte)Identifier | 0x30);
                Composed[1] = (byte)((length >> 40) & 0xFF);
                Composed[2] = (byte)((length >> 32) & 0xFF);
                Composed[3] = (byte)((length >> 24) & 0xFF);
                Composed[4] = (byte)((length >> 16) & 0xFF);
                Composed[5] = (byte)((length >> 8) & 0xFF);
                Composed[6] = (byte)(length & 0xFF);
                ContentOffset = 7;
                Buffer.BlockCopy(data, 0, Composed, 7, (int)length);
            }
            else //if (len <= 0xFF_FF_FF_FF_FF_FF_FF)
            {
                Composed = new byte[8 + length];
                Composed[0] = (byte)((byte)Identifier | 0x38);
                Composed[1] = (byte)((length >> 48) & 0xFF);
                Composed[2] = (byte)((length >> 40) & 0xFF);
                Composed[3] = (byte)((length >> 32) & 0xFF);
                Composed[4] = (byte)((length >> 24) & 0xFF);
                Composed[5] = (byte)((length >> 16) & 0xFF);
                Composed[6] = (byte)((length >> 8) & 0xFF);
                Composed[7] = (byte)(length & 0xFF);
                ContentOffset = 8;
                Buffer.BlockCopy(data, 0, Composed, 8, (int)length);
            }
        }
        else if (Class == TduClass.Typed)
        {
            if (metadata == null)
                throw new Exception("Metadata must be provided for types.");

            var metadataData = metadata.Compose(connection);


            var len = (ulong)metadataData.Length + length;

            if (len <= 0xFF)
            {
                Composed = new byte[2 + len];
                Composed[0] = (byte)((byte)Identifier | 0x8);
                Composed[1] = (byte)len;
                ContentOffset = (uint)metadataData.Length + (uint)2;

                Buffer.BlockCopy(metadataData, 0, Composed, 2, metadataData.Length);
                Buffer.BlockCopy(data, 0, Composed, 2 + metadataData.Length, (int)length);
            }
            else if (len <= 0xFF_FF)
            {
                Composed = new byte[3 + len];
                Composed[0] = (byte)((byte)identifier | 0x10);
                Composed[1] = (byte)((len >> 8) & 0xFF);
                Composed[2] = (byte)(len & 0xFF);
                ContentOffset = (uint)metadataData.Length + (uint)3;

                Buffer.BlockCopy(metadataData, 0, Composed, 3, metadataData.Length);
                Buffer.BlockCopy(data, 0, Composed, 3 + metadataData.Length, (int)length);
            }
            else if (len <= 0xFF_FF_FF)
            {
                Composed = new byte[4 + len];
                Composed[0] = (byte)((byte)identifier | 0x18);
                Composed[1] = (byte)((len >> 16) & 0xFF);
                Composed[2] = (byte)((len >> 8) & 0xFF);
                Composed[3] = (byte)(len & 0xFF);
                ContentOffset = (uint)metadataData.Length + (uint)4;

                Buffer.BlockCopy(metadataData, 0, Composed, 4, metadataData.Length);
                Buffer.BlockCopy(data, 0, Composed, 4 + metadataData.Length, (int)length);

            }
            else if (len <= 0xFF_FF_FF_FF)
            {
                Composed = new byte[5 + len];
                Composed[0] = (byte)((byte)identifier | 0x20);
                Composed[1] = (byte)((len >> 24) & 0xFF);
                Composed[2] = (byte)((len >> 16) & 0xFF);
                Composed[3] = (byte)((len >> 8) & 0xFF);
                Composed[4] = (byte)(len & 0xFF);
                ContentOffset = (uint)metadataData.Length + (uint)5;

                Buffer.BlockCopy(metadataData, 0, Composed, 5, metadataData.Length);
                Buffer.BlockCopy(data, 0, Composed, 5 + metadataData.Length, (int)length);
            }
            else if (len <= 0xFF_FF_FF_FF_FF)
            {
                Composed = new byte[6 + len];
                Composed[0] = (byte)((byte)identifier | 0x28);
                Composed[1] = (byte)((len >> 32) & 0xFF);
                Composed[2] = (byte)((len >> 24) & 0xFF);
                Composed[3] = (byte)((len >> 16) & 0xFF);
                Composed[4] = (byte)((len >> 8) & 0xFF);
                Composed[5] = (byte)(len & 0xFF);
                ContentOffset = (uint)metadataData.Length + (uint)6;

                Buffer.BlockCopy(metadataData, 0, Composed, 6, metadataData.Length);
                Buffer.BlockCopy(data, 0, Composed, 6 + metadataData.Length, (int)length);
            }
            else if (len <= 0xFF_FF_FF_FF_FF_FF)
            {
                Composed = new byte[7 + len];
                Composed[0] = (byte)((byte)identifier | 0x30);
                Composed[1] = (byte)((len >> 40) & 0xFF);
                Composed[2] = (byte)((len >> 32) & 0xFF);
                Composed[3] = (byte)((len >> 24) & 0xFF);
                Composed[4] = (byte)((len >> 16) & 0xFF);
                Composed[5] = (byte)((len >> 8) & 0xFF);
                Composed[6] = (byte)(len & 0xFF);
                ContentOffset = (uint)metadataData.Length + (uint)7;

                Buffer.BlockCopy(metadataData, 0, Composed, 7, metadataData.Length);
                Buffer.BlockCopy(data, 0, Composed, 7 + metadataData.Length, (int)length);
            }
            else //if (len <= 0xFF_FF_FF_FF_FF_FF_FF)
            {
                Composed = new byte[8 + len];
                Composed[0] = (byte)((byte)identifier | 0x38);
                Composed[1] = (byte)((len >> 48) & 0xFF);
                Composed[2] = (byte)((len >> 40) & 0xFF);
                Composed[3] = (byte)((len >> 32) & 0xFF);
                Composed[4] = (byte)((len >> 24) & 0xFF);
                Composed[5] = (byte)((len >> 16) & 0xFF);
                Composed[6] = (byte)((len >> 8) & 0xFF);
                Composed[7] = (byte)(len & 0xFF);

                ContentOffset = (uint)metadataData.Length + (uint)8;

                Buffer.BlockCopy(metadataData, 0, Composed, 8, metadataData.Length);
                Buffer.BlockCopy(data, 0, Composed, 8 + metadataData.Length, (int)length);
            }
        }


    }


    public bool MatchType(Tdu with)
    {
        if (Identifier != with.Identifier)
            return false;

        if (Class != TduClass.Typed || with.Class != TduClass.Typed)
            return false;

        if (!Metadata.Match(with.Metadata))
            return false;

        return true;
    }
}
