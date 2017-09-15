﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Esiur.Misc;
using Esiur.Data;

namespace Esiur.Net.Packets
{
    public class WebsocketPacket : Packet
    {
        public enum WSOpcode : byte
        {
            ContinuationFrame = 0x0, //  %x0 denotes a continuation frame

            TextFrame = 0x1, // %x1 denotes a text frame

            BinaryFrame = 0x2,            // %x2 denotes a binary frame

            // %x3-7 are reserved for further non-control frames

            ConnectionClose = 0x8,    // %x8 denotes a connection close

            Ping = 0x9, // %x9 denotes a ping

            Pong = 0xA,            // %xA denotes a pong

            //*  %xB-F are reserved for further control frames
        }


        public bool FIN;
        public bool RSV1;
        public bool RSV2;
        public bool RSV3;
        public WSOpcode Opcode;
        public bool Mask;
        public long PayloadLength;
        //        public UInt32 MaskKey;
        public byte[] MaskKey;

        public byte[] Message;

        public override string ToString()
        {
            return "WebsocketPacket"
                + "\n\tFIN: " + FIN
                + "\n\tOpcode: " + Opcode
                + "\n\tPayload: " + PayloadLength
                + "\n\tMaskKey: " + MaskKey
                + "\n\tMessage: " + (Message != null ? Message.Length.ToString() : "NULL");
        }

        public override bool Compose()
        {
            var pkt = new List<byte>();
            pkt.Add((byte)((FIN ? 0x80 : 0x0) |
                (RSV1 ? 0x40 : 0x0) |
                (RSV2 ? 0x20 : 0x0) |
                (RSV3 ? 0x10 : 0x0) |
                (byte)Opcode));

            // calculate length
            if (Message.Length > UInt16.MaxValue)
            // 4 bytes
            {
                pkt.Add((byte)((Mask ? 0x80 : 0x0) | 127));
                pkt.AddRange(DC.ToBytes((UInt64)Message.LongCount()));
            }
            else if (Message.Length > 125)
            // 2 bytes
            {
                pkt.Add((byte)((Mask ? 0x80 : 0x0) | 126));
                pkt.AddRange(DC.ToBytes((UInt16)Message.Length));
            }
            else
            {
                pkt.Add((byte)((Mask ? 0x80 : 0x0) | Message.Length));
            }

            if (Mask)
            {
                pkt.AddRange(MaskKey);
            }

            pkt.AddRange(Message);

            Data = pkt.ToArray();

            return true;
        }

        public override long Parse(byte[] data, uint offset, uint ends)
        {
            try
            {
                long needed = 2;
                var length = (ends - offset);
                if (length < needed)
                {
                    //Console.WriteLine("stage 1 " + needed);
                    return length - needed;
                }

                uint oOffset = offset;
                FIN = ((data[offset] & 0x80) == 0x80);
                RSV1 = ((data[offset] & 0x40) == 0x40);
                RSV2 = ((data[offset] & 0x20) == 0x20);
                RSV3 = ((data[offset] & 0x10) == 0x10);
                Opcode = (WSOpcode)(data[offset++] & 0xF);
                Mask = ((data[offset] & 0x80) == 0x80);
                PayloadLength = (long)(data[offset++] & 0x7F);

                if (Mask)
                    needed += 4;

                if (PayloadLength == 126)
                {
                    needed += 2;
                    if (length < needed)
                    {
                        //Console.WriteLine("stage 2 " + needed);
                        return length - needed;
                    }
                    PayloadLength = DC.GetUInt16(data, offset);
                    offset += 2;
                }
                else if (PayloadLength == 127)
                {
                    needed += 8;
                    if (length < needed)
                    {
                        //Console.WriteLine("stage 3 " + needed);
                        return length - needed;
                    }

                    PayloadLength = DC.GetInt64(data, offset);
                    offset += 8;
                }

                if (Mask)
                {
                    MaskKey = new byte[4];
                    MaskKey[0] = data[offset++];
                    MaskKey[1] = data[offset++];
                    MaskKey[2] = data[offset++];
                    MaskKey[3] = data[offset++];

                    //MaskKey = DC.GetUInt32(data, offset);
                    //offset += 4;
                }

                needed += PayloadLength;
                if (length < needed)
                {
                    //Console.WriteLine("stage 4");
                    return length - needed;
                }

                //                if ((int)PayloadLength > (ends - offset))
                //              {
                //                return -((int)PayloadLength - (ends - offset));
                //          }
                else
                {
                    Message = DC.Clip(data, offset, (uint)PayloadLength);

                    if (Mask)
                    {
                        //var aMask = BitConverter.GetBytes(MaskKey);
                        for (int i = 0; i < Message.Length; i++)
                        {
                            Message[i] = (byte)(Message[i] ^ MaskKey[i % 4]);
                        }
                    }

                    return (offset - oOffset) + (int)PayloadLength;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                Console.WriteLine(offset + "::" + DC.ToHex(data));
                throw ex;
            }
        }
    }
}
