/*
 
Copyright (c) 2017-2026 Ahmed Kh. Zamil

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
using Esiur.Security.Authority;
using Esiur.Security.Cryptography;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace Esiur.Net.Packets;

public class EpAuthPacket : Packet
{
    public EpAuthPacketCommand Command
    {
        get;
        set;
    }

    public EpAuthPacketAction Action
    {
        get;
        set;
    }

    public EpAuthPacketEvent Event
    {
        get;
        set;
    }

    public EpAuthPacketAcknowledgement Acknowledgement
    {
        get;
        set;
    }


    public AuthenticationMode AuthMode
    {
        get;
        set;
    }

    public EncryptionMode EncryptionMode
    {
        get;
        set;
    }

    //public AuthenticationMethod AuthenticationMethod
    //{
    //    get;
    //    set;
    //}


    public byte ErrorCode
    {
        get;
        set;
    }

    public string Message
    {
        get;
        set;
    }

    public byte[] SessionId
    {
        get;
        set;
    }


    public ParsedTdu? Tdu
    {
        get;
        set;
    }

    // IAuth Reference
    public uint Reference
    {
        get;
        set;
    }



    private uint dataLengthNeeded;

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

    public override string ToString()
    {
        return Command.ToString() + " " + Action.ToString();
    }

    public override long Parse(byte[] data, uint offset, uint ends)
    {
        var oOffset = offset;

        if (NotEnough(offset, ends, 1))
            return -dataLengthNeeded;

        Command = (EpAuthPacketCommand)(data[offset] >> 6);
        var hasTdu = (data[offset] & 0x20) != 0;

        if (Command == EpAuthPacketCommand.Initialize)
        {
            AuthMode = (AuthenticationMode)(data[offset] >> 3 & 0x7);
            EncryptionMode = (EncryptionMode)(data[offset++] & 0x7);
        }
        else if (Command == EpAuthPacketCommand.Acknowledge)
        {
            // remove last two reserved LSBs
            Acknowledgement = (EpAuthPacketAcknowledgement)(data[offset++]);// & 0xFC);
        }
        else if (Command == EpAuthPacketCommand.Action)
        {
            // remove last two reserved LSBs
            Action = (EpAuthPacketAction)(data[offset++]);// & 0xFC);
        }
        else if (Command == EpAuthPacketCommand.Event)
        {
            // remove last two reserved LSBs
            Event = (EpAuthPacketEvent)(data[offset++]);// & 0xFC);
        }
        else
        {
            return -1; // invalid command
        }

        if (hasTdu)
        {
            if (NotEnough(offset, ends, 1))
                return -dataLengthNeeded;

            Tdu = ParsedTdu.Parse(data, offset, ends);

            if (Tdu.Value.Class == TduClass.Invalid)
                return -(int)Tdu.Value.TotalLength;

            offset += (uint)Tdu.Value.TotalLength;

        }

        return offset - oOffset;

    }

}

