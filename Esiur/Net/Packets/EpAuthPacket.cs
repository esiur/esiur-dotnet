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

using Esiur.Data;
using Esiur.Security.Authority;
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
    public EpAuthPacketInitialize Initialization
    {
        get;
        set;
    }

    public EpAuthPacketAcknowledge Acknowledgement
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

    public AuthenticationMethod LocalMethod
    {
        get;
        set;
    }

    public AuthenticationMethod RemoteMethod
    {
        get;
        set;
    }

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


    public EpAuthPacketPublicKeyAlgorithm PublicKeyAlgorithm
    {
        get;
        set;
    }

    public EpAuthPacketHashAlgorithm HashAlgorithm
    {
        get;
        set;
    }


    public byte[] Certificate
    {
        get;
        set;
    }

    public byte[] Challenge
    {
        get;
        set;
    }

    public byte[] AsymetricEncryptionKey
    {
        get;
        set;
    }


    public byte[] SessionId
    {
        get;
        set;
    }

    public byte[] AccountId
    {
        get;
        set;
    }

    public ParsedTDU? DataType
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

        if (Command == EpAuthPacketCommand.Initialize)
        {
            LocalMethod = (AuthenticationMethod)(data[offset] >> 4 & 0x3);
            RemoteMethod = (AuthenticationMethod)(data[offset] >> 2 & 0x3);

            Initialization = (EpAuthPacketInitialize)(data[offset++] & 0xFC); // remove last two reserved LSBs

            if (NotEnough(offset, ends, 1))
                return -dataLengthNeeded;

            DataType = ParsedTDU.Parse(data, offset, ends);

            if (DataType.Value.Class == TDUClass.Invalid)
                return -(int)DataType.Value.TotalLength;


            offset += (uint)DataType.Value.TotalLength;

        }
        else if (Command == EpAuthPacketCommand.Acknowledge)
        {

            LocalMethod = (AuthenticationMethod)(data[offset] >> 4 & 0x3);
            RemoteMethod = (AuthenticationMethod)(data[offset] >> 2 & 0x3);

            Acknowledgement = (EpAuthPacketAcknowledge)(data[offset++] & 0xFC); // remove last two reserved LSBs

            if (NotEnough(offset, ends, 1))
                return -dataLengthNeeded;

            DataType = ParsedTDU.Parse(data, offset, ends);

            if (DataType.Value.Class == TDUClass.Invalid)
                return -(int)DataType.Value.TotalLength;


            offset += (uint)DataType.Value.TotalLength;
        }
        else if (Command == EpAuthPacketCommand.Action)
        {
            Action = (EpAuthPacketAction)data[offset++]; // (EPAuthPacketAction)(data[offset++] & 0x3f);

            if (Action == EpAuthPacketAction.AuthenticateHash
                || Action == EpAuthPacketAction.AuthenticatePublicHash
                || Action == EpAuthPacketAction.AuthenticatePrivateHash
                || Action == EpAuthPacketAction.AuthenticatePublicPrivateHash)
            {
                if (NotEnough(offset, ends, 3))
                    return -dataLengthNeeded;

                HashAlgorithm = (EpAuthPacketHashAlgorithm)data[offset++];

                var hashLength = data.GetUInt16(offset, Endian.Little);
                offset += 2;


                if (NotEnough(offset, ends, hashLength))
                    return -dataLengthNeeded;

                Challenge = data.Clip(offset, hashLength);
                offset += hashLength;

            }
            else if (Action == EpAuthPacketAction.AuthenticatePrivateHashCert
                || Action == EpAuthPacketAction.AuthenticatePublicPrivateHashCert)
            {
                if (NotEnough(offset, ends, 3))
                    return -dataLengthNeeded;

                HashAlgorithm = (EpAuthPacketHashAlgorithm)data[offset++];

                var hashLength = data.GetUInt16(offset, Endian.Little);
                offset += 2;


                if (NotEnough(offset, ends, hashLength))
                    return -dataLengthNeeded;

                Challenge = data.Clip(offset, hashLength);
                offset += hashLength;

                if (NotEnough(offset, ends, 2))
                    return -dataLengthNeeded;

                var certLength = data.GetUInt16(offset, Endian.Little);
                offset += 2;

                if (NotEnough(offset, ends, certLength))
                    return -dataLengthNeeded;

                Certificate = data.Clip(offset, certLength);

                offset += certLength;
            }
            else if (Action == EpAuthPacketAction.IAuthPlain)
            {
                if (NotEnough(offset, ends, 5))
                    return -dataLengthNeeded;

                Reference = data.GetUInt32(offset, Endian.Little);
                offset += 4;

                DataType = ParsedTDU.Parse(data, offset, ends);

                if (DataType.Value.Class == TDUClass.Invalid)
                    return -(int)DataType.Value.TotalLength;

                offset += (uint)DataType.Value.TotalLength;

            }
            else if (Action == EpAuthPacketAction.IAuthHashed)
            {
                if (NotEnough(offset, ends, 7))
                    return -dataLengthNeeded;

                Reference = data.GetUInt32(offset, Endian.Little);
                offset += 4;

                HashAlgorithm = (EpAuthPacketHashAlgorithm)data[offset++];

                var cl = data.GetUInt16(offset, Endian.Little);
                offset += 2;

                if (NotEnough(offset, ends, cl))
                    return -dataLengthNeeded;

                Challenge = data.Clip(offset, cl);

                offset += cl;

            }
            else if (Action == EpAuthPacketAction.IAuthEncrypted)
            {
                if (NotEnough(offset, ends, 7))
                    return -dataLengthNeeded;

                Reference = data.GetUInt32(offset, Endian.Little);
                offset += 4;

                PublicKeyAlgorithm = (EpAuthPacketPublicKeyAlgorithm)data[offset++];

                var cl = data.GetUInt16(offset, Endian.Little);
                offset += 2;

                if (NotEnough(offset, ends, cl))
                    return -dataLengthNeeded;

                Challenge = data.Clip(offset, cl);

                offset += cl;

            }
            else if (Action == EpAuthPacketAction.EstablishNewSession)
            {
                // Nothing here
            }
            else if (Action == EpAuthPacketAction.EstablishResumeSession)
            {
                if (NotEnough(offset, ends, 1))
                    return -dataLengthNeeded;

                var sessionLength = data[offset++];

                if (NotEnough(offset, ends, sessionLength))
                    return -dataLengthNeeded;

                SessionId = data.Clip(offset, sessionLength);

                offset += sessionLength;
            }

            else if (Action == EpAuthPacketAction.EncryptKeyExchange)
            {
                if (NotEnough(offset, ends, 2))
                    return -dataLengthNeeded;

                var keyLength = data.GetUInt16(offset, Endian.Little);

                offset += 2;

                if (NotEnough(offset, ends, keyLength))
                    return -dataLengthNeeded;

                AsymetricEncryptionKey = data.Clip(offset, keyLength);

                offset += keyLength;
            }

            else if (Action == EpAuthPacketAction.RegisterEndToEndKey
                || Action == EpAuthPacketAction.RegisterHomomorphic)
            {
                if (NotEnough(offset, ends, 3))
                    return -dataLengthNeeded;

                PublicKeyAlgorithm = (EpAuthPacketPublicKeyAlgorithm)data[offset++];

                var keyLength = data.GetUInt16(offset, Endian.Little);

                offset += 2;

                if (NotEnough(offset, ends, keyLength))
                    return -dataLengthNeeded;

                AsymetricEncryptionKey = data.Clip(offset, keyLength);

                offset += keyLength;

            }
        }
        else if (Command == EpAuthPacketCommand.Event)
        {

            Event = (EpAuthPacketEvent)data[offset++];

            if (Event == EpAuthPacketEvent.ErrorTerminate
                || Event == EpAuthPacketEvent.ErrorMustEncrypt
                || Event == EpAuthPacketEvent.ErrorRetry)
            {
                if (NotEnough(offset, ends, 3))
                    return -dataLengthNeeded;

                ErrorCode = data[offset++];
                var msgLength = data.GetUInt16(offset, Endian.Little);
                offset += 2;

                if (NotEnough(offset, ends, msgLength))
                    return -dataLengthNeeded;


                Message = data.GetString(offset, msgLength);

                offset += msgLength;
            }
            else if (Event == EpAuthPacketEvent.IndicationEstablished)
            {
                if (NotEnough(offset, ends, 2))
                    return -dataLengthNeeded;

                var sessionLength = data[offset++];

                if (NotEnough(offset, ends, sessionLength))
                    return -dataLengthNeeded;

                SessionId = data.Clip(offset, sessionLength);

                offset += sessionLength;

                if (NotEnough(offset, ends, 1))
                    return -dataLengthNeeded;

                var accountLength = data[offset++];

                if (NotEnough(offset, ends, accountLength))
                    return -dataLengthNeeded;

                AccountId = data.Clip(offset, accountLength);

                offset += accountLength;
            }

            else if (Event == EpAuthPacketEvent.IAuthPlain
                || Event == EpAuthPacketEvent.IAuthHashed
                || Event == EpAuthPacketEvent.IAuthEncrypted)
            {
                if (NotEnough(offset, ends, 1))
                    return -dataLengthNeeded;

                DataType = ParsedTDU.Parse(data, offset, ends);

                if (DataType.Value.Class == TDUClass.Invalid)
                    return -(int)DataType.Value.TotalLength;

                offset += (uint)DataType.Value.TotalLength;

            }
        }


        return offset - oOffset;

    }

}

