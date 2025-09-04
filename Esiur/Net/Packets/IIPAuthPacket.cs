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

public class IIPAuthPacket : Packet
{

    public IIPAuthPacketCommand Command
    {
        get;
        set;
    }
    public IIPAuthPacketInitialize Initialization
    {
        get;
        set;
    }

    public IIPAuthPacketAcknowledge Acknowledgement
    {
        get;
        set;
    }

    public IIPAuthPacketAction Action
    {
        get;
        set;
    }

    public IIPAuthPacketEvent Event
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


    public IIPAuthPacketPublicKeyAlgorithm PublicKeyAlgorithm
    {
        get;
        set;
    }

    public IIPAuthPacketHashAlgorithm HashAlgorithm
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

        Command = (IIPAuthPacketCommand)(data[offset] >> 6);

        if (Command == IIPAuthPacketCommand.Initialize)
        {
            LocalMethod = (AuthenticationMethod)(data[offset] >> 4 & 0x3);
            RemoteMethod = (AuthenticationMethod)(data[offset] >> 2 & 0x3);

            Initialization = (IIPAuthPacketInitialize)(data[offset++] & 0xFC); // remove last two reserved LSBs

            if (NotEnough(offset, ends, 1))
                return -dataLengthNeeded;

            (var size, DataType) = ParsedTDU.Parse(data, offset, ends);

            if (DataType == null)
                return -(int)size;


            offset += (uint)size;

        }
        else if (Command == IIPAuthPacketCommand.Acknowledge)
        {

            LocalMethod = (AuthenticationMethod)(data[offset] >> 4 & 0x3);
            RemoteMethod = (AuthenticationMethod)(data[offset] >> 2 & 0x3);

            Acknowledgement = (IIPAuthPacketAcknowledge)(data[offset++] & 0xFC); // remove last two reserved LSBs

            if (NotEnough(offset, ends, 1))
                return -dataLengthNeeded;

            (var size, DataType) = ParsedTDU.Parse(data, offset, ends);

            if (DataType == null)
                return -(int)size;


            offset += (uint)size;
        }
        else if (Command == IIPAuthPacketCommand.Action)
        {
            Action = (IIPAuthPacketAction)data[offset++]; // (IIPAuthPacketAction)(data[offset++] & 0x3f);

            if (Action == IIPAuthPacketAction.AuthenticateHash
                || Action == IIPAuthPacketAction.AuthenticatePublicHash
                || Action == IIPAuthPacketAction.AuthenticatePrivateHash
                || Action == IIPAuthPacketAction.AuthenticatePublicPrivateHash)
            {
                if (NotEnough(offset, ends, 3))
                    return -dataLengthNeeded;

                HashAlgorithm = (IIPAuthPacketHashAlgorithm)data[offset++];

                var hashLength = data.GetUInt16(offset, Endian.Little);
                offset += 2;


                if (NotEnough(offset, ends, hashLength))
                    return -dataLengthNeeded;

                Challenge = data.Clip(offset, hashLength);
                offset += hashLength;

            }
            else if (Action == IIPAuthPacketAction.AuthenticatePrivateHashCert
                || Action == IIPAuthPacketAction.AuthenticatePublicPrivateHashCert)
            {
                if (NotEnough(offset, ends, 3))
                    return -dataLengthNeeded;

                HashAlgorithm = (IIPAuthPacketHashAlgorithm)data[offset++];

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
            else if (Action == IIPAuthPacketAction.IAuthPlain)
            {
                if (NotEnough(offset, ends, 5))
                    return -dataLengthNeeded;

                Reference = data.GetUInt32(offset, Endian.Little);
                offset += 4;

                (var size, DataType) = ParsedTDU.Parse(data, offset, ends);

                if (DataType == null)
                    return -(int)size;

                offset += (uint)size;

            }
            else if (Action == IIPAuthPacketAction.IAuthHashed)
            {
                if (NotEnough(offset, ends, 7))
                    return -dataLengthNeeded;

                Reference = data.GetUInt32(offset, Endian.Little);
                offset += 4;

                HashAlgorithm = (IIPAuthPacketHashAlgorithm)data[offset++];

                var cl = data.GetUInt16(offset, Endian.Little);
                offset += 2;

                if (NotEnough(offset, ends, cl))
                    return -dataLengthNeeded;

                Challenge = data.Clip(offset, cl);

                offset += cl;

            }
            else if (Action == IIPAuthPacketAction.IAuthEncrypted)
            {
                if (NotEnough(offset, ends, 7))
                    return -dataLengthNeeded;

                Reference = data.GetUInt32(offset, Endian.Little);
                offset += 4;

                PublicKeyAlgorithm = (IIPAuthPacketPublicKeyAlgorithm)data[offset++];

                var cl = data.GetUInt16(offset, Endian.Little);
                offset += 2;

                if (NotEnough(offset, ends, cl))
                    return -dataLengthNeeded;

                Challenge = data.Clip(offset, cl);

                offset += cl;

            }
            else if (Action == IIPAuthPacketAction.EstablishNewSession)
            {
                // Nothing here
            }
            else if (Action == IIPAuthPacketAction.EstablishResumeSession)
            {
                if (NotEnough(offset, ends, 1))
                    return -dataLengthNeeded;

                var sessionLength = data[offset++];

                if (NotEnough(offset, ends, sessionLength))
                    return -dataLengthNeeded;

                SessionId = data.Clip(offset, sessionLength);

                offset += sessionLength;
            }

            else if (Action == IIPAuthPacketAction.EncryptKeyExchange)
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

            else if (Action == IIPAuthPacketAction.RegisterEndToEndKey
                || Action == IIPAuthPacketAction.RegisterHomomorphic)
            {
                if (NotEnough(offset, ends, 3))
                    return -dataLengthNeeded;

                PublicKeyAlgorithm = (IIPAuthPacketPublicKeyAlgorithm)data[offset++];

                var keyLength = data.GetUInt16(offset, Endian.Little);

                offset += 2;

                if (NotEnough(offset, ends, keyLength))
                    return -dataLengthNeeded;

                AsymetricEncryptionKey = data.Clip(offset, keyLength);

                offset += keyLength;

            }
        }
        else if (Command == IIPAuthPacketCommand.Event)
        {

            Event = (IIPAuthPacketEvent)data[offset++];

            if (Event == IIPAuthPacketEvent.ErrorTerminate
                || Event == IIPAuthPacketEvent.ErrorMustEncrypt
                || Event == IIPAuthPacketEvent.ErrorRetry)
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
            else if (Event == IIPAuthPacketEvent.IndicationEstablished)
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

            else if (Event == IIPAuthPacketEvent.IAuthPlain
                || Event == IIPAuthPacketEvent.IAuthHashed
                || Event == IIPAuthPacketEvent.IAuthEncrypted)
            {
                if (NotEnough(offset, ends, 1))
                    return -dataLengthNeeded;

                (var size, DataType) = ParsedTDU.Parse(data, offset, ends);

                if (DataType == null)
                    return -(int)size;

                offset += (uint)size;

            }
        }


        return offset - oOffset;

    }

}

