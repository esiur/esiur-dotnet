using System;
using Esiur.Resource;
using Esiur.Core;
using Esiur.Data;
using Esiur.Protocol;
using Google.Protobuf;
namespace RPC.EsiurTest
{
    [TypeId("4befaa686f038a2885268fca4cbf3c2c")]
    [Export]
    public class Attachment : IRecord
    {
        [Annotation("Byte[]")]
        public byte[] Data { get; set; }

        [Annotation("String")]
        public string MimeType { get; set; }

        [Annotation("String")]
        public string Name { get; set; }

        public override bool Equals(object? obj)
        {
            var other = obj as Attachment;
            if (Name != other.Name) return false;
            if (MimeType != other.MimeType) return false;
            if (!(Data.SequenceEqual(other.Data))) return false;

            return true;
        }

        public SharedModel.Attachment ToShared()
        {
            return new SharedModel.Attachment()
            {
                Data = Data,
                MimeType = MimeType,
                Name = Name,
            };
        }

        public Echo.Model.Grpc.Attachment ToGrpc()
        {
            return new Echo.Model.Grpc.Attachment()
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
