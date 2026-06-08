using Esiur.Core;
using Esiur.Data;
using Esiur.Protocol;
using Esiur.Resource;
using Google.Protobuf;
using System;
namespace Esiur.Tests.RPC.EsiurServer
{
    [Remote("Esiur.Tests.RPC.EsiurServer.Variant", "")]
    [Export]
    public class Variant : IRecord
    {
        [Annotation("", "Nullable`1?")]
        public bool? Bool { get; set; }

        [Annotation("", "Byte[]")]
        public byte[] Bytes { get; set; }

        [Annotation("", "Nullable`1?")]
        public DateTime? Dt { get; set; }

        [Annotation("", "Nullable`1?")]
        public double? F64 { get; set; }

        [Annotation("", "Byte[]")]
        public byte[] Guid { get; set; }

        [Annotation("", "Nullable`1?")]
        public long? I64 { get; set; }

        [Annotation("", "String")]
        public string Str { get; set; }

        [Annotation("", "Kind")]
        public Esiur.Tests.RPC.EsiurServer.Kind Tag { get; set; }

        [Annotation("", "Nullable`1?")]
        public ulong? U64 { get; set; }



        public Client.SharedModel.Variant ToShared()
        {
            return new Client.SharedModel.Variant()
            {

                Bool = Bool,
                Bytes = Bytes,
                Dt = Dt,
                F64 = F64,
                Guid = Guid,
                I64 = I64,
                Str = Str,
                Tag = Enum.Parse<Client.SharedModel.Kind>(Tag.ToString(), true),
                U64 = U64
            };
        }

        public Client.Grpc.Variant ToGrpc()
        {
            return new Client.Grpc.Variant()
            {
                BoolVal = Bool ?? false,
                BytesVal = ByteString.CopyFrom(Bytes ?? new byte[0]),
                DtVal = Dt?.Ticks ?? 0,
                F64Val = F64 ?? 0,
                GuidVal = ByteString.CopyFrom(Guid ?? new byte[0]),
                I64Val = I64 ?? 0,
                StrVal = Str,
                Tag = Enum.Parse<Client.Grpc.Kind>(Tag.ToString(), true),
                U64Val = U64 ?? 0,
            };
        }

        public Echo.ThriftModel.Variant ToThrift()
        {
            var rt = new Echo.ThriftModel.Variant()
            {
                Tag = Enum.Parse<Echo.ThriftModel.Kind>(Tag.ToString(), true),
            };

            if (Bool != null)
                rt.BoolVal = Bool.Value;
            if (Bytes != null)
                rt.BytesVal = Bytes;
            if (Dt != null)
                rt.DtVal = Dt.Value.Ticks;
            if (F64 != null)
                rt.F64Val = F64.Value;
            if (Guid != null)
                rt.GuidVal = Guid;
            if (I64 != null)
                rt.I64Val = I64.Value;
            return rt;
        }

        public override bool Equals(object? obj)
        {
            var other = obj as Variant;
            if (other == null) return false;

            if (other.I64 != I64) return false;
            if (other.U64 != U64) return false;
            if (other.Bool != Bool) return false;
            //if (other.Dec != Dec) return false;
            if (other.Str != Str) return false;
            if (Guid != null)
                if (!other.Guid.SequenceEqual(Guid)) return false;
            if (other.F64 != F64) return false;
            if (other.Tag != Tag) return false;
            if (Bytes != null)
                if (!other.Bytes.SequenceEqual(Bytes)) return false;

            if (other.Dt != Dt)
                return false;

            return true;
        }


    }
}
