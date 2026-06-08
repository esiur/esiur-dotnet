using Esiur.Core;
using Esiur.Data;
using Esiur.Protocol;
using Esiur.Resource;
using Google.Protobuf;
using System;
namespace Esiur.Tests.RPC.EsiurServer
{
    [Remote("Esiur.Tests.RPC.EsiurServer.Attachment", "localhost")]
    [Export]
    public class Attachment : IRecord
    {
        [Annotation("", "Byte[]")]
        public byte[] Data { get; set; }

        [Annotation("", "String")]
        public string MimeType { get; set; }

        [Annotation("", "String")]
        public string Name { get; set; }


        public override bool Equals(object? obj)
        {
            var other = obj as Attachment;
            if (Name != other.Name) return false;
            if (MimeType != other.MimeType) return false;
            if (!(Data.SequenceEqual(other.Data))) return false;

            return true;
        }

        public Client.SharedModel.Attachment ToShared()
        {
            return new Client.SharedModel.Attachment()
            {
                Data = Data,
                MimeType = MimeType,
                Name = Name,
            };
        }

        public Esiur.Tests.RPC.Client.Grpc.Attachment ToGrpc()
        {
            return new Esiur.Tests.RPC.Client.Grpc.Attachment()
            {
                Data = ByteString.CopyFrom(Data),
                MimeType = MimeType,
                Name = Name,
            };
        }

        public Echo.ThriftModel.Attachment ToThrift()
        {
            return new Echo.ThriftModel.Attachment()
            {
                Data = Data,
                MimeType = MimeType,
                Name = Name,
            };
        }
    }
}
