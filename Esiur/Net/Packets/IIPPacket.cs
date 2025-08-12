/*
 
Copyright (c) 2017-2025 Ahmed Kh. Zamil

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

using Esiur.Data;
using Esiur.Core;
using Esiur.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Net.Packets;
class IIPPacket : Packet
{

    public uint CallbackId { get; set; }
    public IIPPacketMethod Method { get; set; }
    public IIPPacketRequest Request {  get; set; }  
    public IIPPacketReply Reply {  get; set; }  

    public IIPPacketNotification Notification {  get; set; }

    public byte Extension { get; set; }

    public TransmissionType? DataType { get; set; }


    private uint dataLengthNeeded;
    private uint originalOffset;

    public override bool Compose()
    {
        return base.Compose();
    }

    bool NotEnough(uint offset, uint ends, uint needed)
    {
        if (offset + needed > ends)
        {
            dataLengthNeeded = needed - (ends - offset);

            return true;
        }
        else
            return false;
    }

    public override long Parse(byte[] data, uint offset, uint ends)
    {
        originalOffset = offset;

        if (NotEnough(offset, ends, 1))
            return -dataLengthNeeded;

        var hasDTU = (data[offset] & 0x20) == 0x20;

        Method = (IIPPacketMethod)(data[offset] >> 6);

        if (Method == IIPPacketMethod.Notification)
        {
            Notification = (IIPPacketNotification)(data[offset++] & 0x3f);
        }
        else if (Method == IIPPacketMethod.Request)
        {
            Request = (IIPPacketRequest)(data[offset++] & 0x3f);

            if (NotEnough(offset, ends, 4))
                return -dataLengthNeeded;

            CallbackId = data.GetUInt32(offset, Endian.Little);
            offset += 4;
        }
        else if (Method == IIPPacketMethod.Reply)
        {
            Reply = (IIPPacketReply)(data[offset++] & 0x3f);

            if (NotEnough(offset, ends, 4))
                return -dataLengthNeeded;

            CallbackId = data.GetUInt32(offset, Endian.Little);
            offset += 4;
        }
        else if (Method == IIPPacketMethod.Extension)
        {
            Extension = (byte)(data[offset++] & 0x3f);
        }

        if (hasDTU)
        {
            if (NotEnough(offset, ends, 1))
                return -dataLengthNeeded;

            (var size, DataType) = TransmissionType.Parse(data, offset, ends);

            if (DataType == null)
                return -(int)size;

            offset += (uint)size;
        }

        return offset - originalOffset;
    }
}
