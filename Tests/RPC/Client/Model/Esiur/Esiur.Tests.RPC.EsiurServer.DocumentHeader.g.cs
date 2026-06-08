using Esiur.Core;
using Esiur.Data;
using Esiur.Protocol;
using Esiur.Resource;
using Google.Protobuf;
using System;
namespace Esiur.Tests.RPC.EsiurServer
{
    [Remote("Esiur.Tests.RPC.EsiurServer.DocumentHeader", "localhost")]
    [Export]
    public class DocumentHeader : IRecord
    {
        [Annotation("", "DateTime")]
        public DateTime CreatedAt { get; set; }

        [Annotation("", "Currency")]
        public Esiur.Tests.RPC.EsiurServer.Currency Currency { get; set; }

        [Annotation("", "Byte[]")]
        public byte[] DocId { get; set; }

        [Annotation("", "Dictionary`2")]
        public Map<string, Esiur.Tests.RPC.EsiurServer.Variant> Meta { get; set; }

        [Annotation("", "String")]
        public string Notes { get; set; }

        [Annotation("", "DocType")]
        public Esiur.Tests.RPC.EsiurServer.DocType Type { get; set; }

        [Annotation("", "Nullable`1?")]
        public DateTime? UpdatedAt { get; set; }

        [Annotation("", "Int32")]
        public int Version { get; set; }


        public Client.SharedModel.DocumentHeader ToShared()
        {
            return new Client.SharedModel.DocumentHeader()
            {
                CreatedAt = CreatedAt,
                DocId = DocId,
                Meta = Meta.ToDictionary(x => x.Key, v => v.Value.ToShared()),
                Notes = Notes,
                Currency = Enum.Parse<Client.SharedModel.Currency>(Currency.ToString(), true),
                UpdatedAt = UpdatedAt,
                Version = Version,
                Type = Enum.Parse<Client.SharedModel.DocType>(Type.ToString(), true)
            };
        }

        public Echo.ThriftModel.DocumentHeader ToThrift()
        {
            var rt = new Echo.ThriftModel.DocumentHeader()
            {
                DocId = DocId,
                CreatedAt = CreatedAt.Ticks,
                Currency = Enum.Parse<Echo.ThriftModel.Currency>(Currency.ToString(), true),
                Type = Enum.Parse<Echo.ThriftModel.DocType>(Type.ToString(), true),
                Version = Version,
                Meta = Meta.ToDictionary(x => x.Key, x => x.Value.ToThrift())
            };

            if (UpdatedAt != null)
                rt.UpdatedAt = UpdatedAt.Value.Ticks;

            if (Notes != null)
                rt.Notes = Notes;

            return rt;
        }

        public Esiur.Tests.RPC.Client.Grpc.DocumentHeader ToGrpc()
        {
            var hdr = new Esiur.Tests.RPC.Client.Grpc.DocumentHeader();

            hdr.DocId = ByteString.CopyFrom(DocId);
            hdr.CreatedAt = CreatedAt.Ticks;
            hdr.Currency = Enum.Parse<Esiur.Tests.RPC.Client.Grpc.Currency>(Currency.ToString(), true);
            hdr.Version = Version;
            hdr.Notes = Notes;

            foreach (var mt in Meta)
                hdr.Meta.Add(mt.Key, mt.Value.ToGrpc());

            return hdr;
        }

        public override bool Equals(object? obj)
        {
            var other = obj as DocumentHeader;

            if (other == null) return false;
            if (!DocId.SequenceEqual(other.DocId)) return false;
            if (Type != other.Type) return false;
            if (Version != other.Version) return false;

            if (CreatedAt != other.CreatedAt) return false;
            if (UpdatedAt != other.UpdatedAt) return false;

            if (Currency != other.Currency) return false;
            if (Notes != other.Notes) return false;

            if (Meta != null)
                foreach (var kv in Meta)
                    if (!other.Meta[kv.Key].Equals(kv.Value))
                        return false;

            return true;
        }

    }
}
