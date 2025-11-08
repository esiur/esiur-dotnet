using Esiur.Net.IIP;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;
using System.Xml.Schema;
using static Esiur.Data.Codec;

namespace Esiur.Data;

// Transmission Data Unit
public struct TDU
{
    public TDUIdentifier Identifier;
    //public int Index;
    public TDUClass Class;
    //public ulong ContentLength;

    public byte[] Composed;
    //public uint Offset;

    public byte[] Metadata;

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

    public TDU()
    {

    }

    public TDU(TDUIdentifier identifier)
    {
        Identifier = identifier;
        Composed = new byte[0];
    }

    public TDU(TDUIdentifier identifier,
                                byte[] data, ulong length, byte[] metadata = null)
    {
        Identifier = identifier;
        //Index = (byte)identifier & 0x7;
        Class = (TDUClass)((byte)identifier >> 6);
        Metadata = metadata;


        if (Class == TDUClass.Fixed)
        {
            if (length == 0)
                Composed = new byte[1] { (byte)identifier };
            else
                Composed = DC.Combine(new byte[] { (byte)Identifier }, 0, 1, data, 0, (uint)length);
        }
        else if (Class == TDUClass.Dynamic
            || Class == TDUClass.Extension)
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
        else if (Class == TDUClass.Typed)
        {
            if (metadata == null)
                throw new Exception("Metadata must be provided for types.");


            if (metadata.Length > 0xFF)
                throw new Exception("Metadata can't exceed 255 bytes in length.");

            var metaLen = (byte)metadata.Length;

            var len = 1 + (ulong)metaLen + length;


            if (length == 0 && (metadata == null || metadata.Length == 0))
            {
                Composed = new byte[1] { (byte)Identifier };
                throw new Exception("Need check");
            }
            else if (metadata.Length > 0xFF)
            {
                throw new Exception("Metadata can't exceed 255 bytes in length.");
            }
            else if (length <= 0xFF)
            {
                Composed = new byte[2 + len];
                Composed[0] = (byte)((byte)Identifier | 0x8);
                Composed[1] = (byte)len;
                Composed[2] = metaLen;
                ContentOffset = metaLen + (uint)3;

                Buffer.BlockCopy(metadata, 0, Composed, 3, metaLen);
                Buffer.BlockCopy(data, 0, Composed, 3 + metaLen, (int)length);
            }
            else if (len <= 0xFF_FF)
            {
                Composed = new byte[3 + len];
                Composed[0] = (byte)((byte)identifier | 0x10);
                Composed[1] = (byte)((len >> 8) & 0xFF);
                Composed[2] = (byte)(len & 0xFF);
                Composed[3] = metaLen;
                ContentOffset = metaLen + (uint)4;

                Buffer.BlockCopy(metadata, 0, Composed, 4, metaLen);
                Buffer.BlockCopy(data, 0, Composed, 4 + metaLen, (int)length);
            }
            else if (len <= 0xFF_FF_FF)
            {
                Composed = new byte[4 + len];
                Composed[0] = (byte)((byte)identifier | 0x18);
                Composed[1] = (byte)((len >> 16) & 0xFF);
                Composed[2] = (byte)((len >> 8) & 0xFF);
                Composed[3] = (byte)(len & 0xFF);
                Composed[4] = metaLen;
                ContentOffset = metaLen + (uint)5;

                Buffer.BlockCopy(metadata, 0, Composed, 5, metaLen);
                Buffer.BlockCopy(data, 0, Composed, 5 + metaLen, (int)length);

            }
            else if (len <= 0xFF_FF_FF_FF)
            {
                Composed = new byte[5 + len];
                Composed[0] = (byte)((byte)identifier | 0x20);
                Composed[1] = (byte)((len >> 24) & 0xFF);
                Composed[2] = (byte)((len >> 16) & 0xFF);
                Composed[3] = (byte)((len >> 8) & 0xFF);
                Composed[4] = (byte)(len & 0xFF);
                Composed[5] = metaLen;
                ContentOffset = metaLen + (uint)6;

                Buffer.BlockCopy(metadata, 0, Composed, 6, metaLen);
                Buffer.BlockCopy(data, 0, Composed, 6 + metaLen, (int)length);
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
                Composed[6] = metaLen;
                ContentOffset = metaLen + (uint)7;

                Buffer.BlockCopy(metadata, 0, Composed, 7, metaLen);
                Buffer.BlockCopy(data, 0, Composed, 7 + metaLen, (int)length);
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
                Composed[7] = metaLen;
                ContentOffset = metaLen + (uint)8;

                Buffer.BlockCopy(metadata, 0, Composed, 8, metaLen);
                Buffer.BlockCopy(data, 0, Composed, 8 + metaLen, (int)length);
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
                Composed[8] = metaLen;
                ContentOffset = metaLen + (uint)9;

                Buffer.BlockCopy(metadata, 0, Composed, 9, metaLen);
                Buffer.BlockCopy(data, 0, Composed, 9 + metaLen, (int)length);
            }
        }


    }


    public bool MatchType(TDU with)
    {
        if (Identifier != with.Identifier)
            return false;

        if (Class != TDUClass.Typed || with.Class != TDUClass.Typed)
            return false;

        if (!Metadata.SequenceEqual(with.Metadata))
            return false;

        return true;
    }
}
