 using Esiur.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Net.Packets
{
    class IIPAuthPacket : Packet
    {
        public enum IIPAuthPacketCommand: byte
        {
            Action = 0,
            Declare,
            Acknowledge,
            Error,
        }

        public enum IIPAuthPacketAction: byte
        {
            // Authenticate
            AuthenticateHash,


            //Challenge,
            //CertificateRequest,
            //CertificateReply,
            //EstablishRequest,
            //EstablishReply

            NewConnection = 0x20,
            ResumeConnection,

            ConnectionEstablished = 0x28
        }


        public enum IIPAuthPacketMethod: byte
        {
            None,
            Certificate,
            Credentials,
            Token
        }


        public IIPAuthPacketCommand Command
        {
            get;
            set;
        }
        public IIPAuthPacketAction Action
        {
            get;
            set;
        }

        public byte ErrorCode { get; set; }
        public string ErrorMessage { get; set; }

        public IIPAuthPacketMethod LocalMethod
        {
            get;
            set;
        }

        public byte[] SourceInfo
        {
            get;
            set;
        }

        public byte[] Hash
        {
            get;
            set;
        }

        public byte[] SessionId
        {
            get;
            set;
        }

        public IIPAuthPacketMethod RemoteMethod
        {
            get;
            set;
        }

        public string Domain
        {
            get;
            set;
        }

        public long CertificateId
        {
            get;set;
        }

        public string LocalUsername
        {
            get;
            set;
        }

        public string RemoteUsername
        {
            get;
            set;
        }

        public byte[] LocalPassword
        {
            get;
            set;
        }
        public byte[] RemotePassword
        {
            get;
            set;
        }

        public byte[] LocalToken
        {
            get;
            set;
        }

        public byte[] RemoteToken
        {
            get;
            set;
        }

        public byte[] AsymetricEncryptionKey
        {
            get;
            set;
        }

        public byte[] LocalNonce
        {
            get;
            set;
        }

        public byte[] RemoteNonce
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

            if (Command == IIPAuthPacketCommand.Action)
            {
                Action = (IIPAuthPacketAction)(data[offset++] & 0x3f);

                if (Action == IIPAuthPacketAction.AuthenticateHash)
                {
                    if (NotEnough(offset, ends, 32))
                        return -dataLengthNeeded;

                    Hash = data.Clip(offset, 32);

                    //var hash = new byte[32];
                    //Buffer.BlockCopy(data, (int)offset, hash, 0, 32);
                    //Hash = hash;

                    offset += 32;
                }
                else if (Action == IIPAuthPacketAction.NewConnection)
                {
                    if (NotEnough(offset, ends, 2))
                        return -dataLengthNeeded;

                    var length = data.GetUInt16(offset);

                    offset += 2;

                    if (NotEnough(offset, ends, length))
                        return -dataLengthNeeded;

                    SourceInfo = data.Clip(offset, length);

                    //var sourceInfo = new byte[length];
                    //Buffer.BlockCopy(data, (int)offset, sourceInfo, 0, length);
                    //SourceInfo = sourceInfo;

                    offset += 32;
                }
                else if (Action == IIPAuthPacketAction.ResumeConnection
                     || Action == IIPAuthPacketAction.ConnectionEstablished)
                {
                    //var sessionId = new byte[32];

                    if (NotEnough(offset, ends, 32))
                        return -dataLengthNeeded;

                    SessionId = data.Clip(offset, 32);

                    //Buffer.BlockCopy(data, (int)offset, sessionId, 0, 32);
                    //SessionId = sessionId;

                    offset += 32;
                }
            }
            else if (Command == IIPAuthPacketCommand.Declare)
            {
                RemoteMethod = (IIPAuthPacketMethod)((data[offset] >> 4) & 0x3);
                LocalMethod = (IIPAuthPacketMethod)((data[offset] >> 2) & 0x3);
                var encrypt = ((data[offset++] & 0x2) == 0x2);


                if (NotEnough(offset, ends, 1))
                    return -dataLengthNeeded;

                var domainLength = data[offset++];
                if (NotEnough(offset, ends, domainLength))
                    return -dataLengthNeeded;

                var domain = data.GetString(offset, domainLength);

                Domain = domain;

                offset += domainLength;
           

                if (RemoteMethod == IIPAuthPacketMethod.Credentials)
                {
                    if (LocalMethod == IIPAuthPacketMethod.None)
                    {
                        if (NotEnough(offset, ends, 33))
                            return -dataLengthNeeded;

                        //var remoteNonce = new byte[32];
                        //Buffer.BlockCopy(data, (int)offset, remoteNonce, 0, 32);
                        //RemoteNonce = remoteNonce;

                        RemoteNonce = data.Clip(offset, 32);

                        offset += 32;
                        
                        var length = data[offset++];

                        if (NotEnough(offset, ends, length))
                            return -dataLengthNeeded;

                         RemoteUsername = data.GetString(offset, length);

                         
                        offset += length;
                    }
                }

                if (encrypt)
                {
                    if (NotEnough(offset, ends, 2))
                        return -dataLengthNeeded;

                    var keyLength = data.GetUInt16(offset);

                    offset += 2;

                    if (NotEnough(offset, ends, keyLength))
                        return -dataLengthNeeded;

                    //var key = new byte[keyLength];
                    //Buffer.BlockCopy(data, (int)offset, key, 0, keyLength);
                    //AsymetricEncryptionKey = key;

                    AsymetricEncryptionKey = data.Clip(offset, keyLength);

                    offset += keyLength;
                }
            }
            else if (Command == IIPAuthPacketCommand.Acknowledge)
            {
                RemoteMethod  = (IIPAuthPacketMethod)((data[offset] >> 4) & 0x3);
                LocalMethod = (IIPAuthPacketMethod)((data[offset] >> 2) & 0x3);
                var encrypt = ((data[offset++] & 0x2) == 0x2);

                if (NotEnough(offset, ends, 1))
                    return -dataLengthNeeded;
 
    
                if (RemoteMethod == IIPAuthPacketMethod.Credentials)
                {
                    if (LocalMethod == IIPAuthPacketMethod.None)
                    {
                        if (NotEnough(offset, ends, 32))
                            return -dataLengthNeeded;

                        /*
                        var remoteNonce = new byte[32];
                        Buffer.BlockCopy(data, (int)offset, remoteNonce, 0, 32);
                        RemoteNonce = remoteNonce;
                        */

                        RemoteNonce = data.Clip(offset, 32);
                        offset += 32;

                     }
                }

                if (encrypt)
                {
                    if (NotEnough(offset, ends, 2))
                        return -dataLengthNeeded;

                    var keyLength = data.GetUInt16(offset);

                    offset += 2;

                    if (NotEnough(offset, ends, keyLength))
                        return -dataLengthNeeded;

                    //var key = new byte[keyLength];
                    //Buffer.BlockCopy(data, (int)offset, key, 0, keyLength);
                    //AsymetricEncryptionKey = key;

                    AsymetricEncryptionKey = data.Clip(offset, keyLength);

                    offset += keyLength;
                }
            }
            else if (Command == IIPAuthPacketCommand.Error)
            {
                if (NotEnough(offset, ends, 4))
                    return -dataLengthNeeded;

                offset++;
                ErrorCode = data[offset++];

 
                var cl = data.GetUInt16(offset);
                offset += 2;

                if (NotEnough(offset, ends, cl))
                    return -dataLengthNeeded;

                ErrorMessage = data.GetString(offset, cl);
                offset += cl;

            }


            return offset - oOffset;

        }

    }
 }
