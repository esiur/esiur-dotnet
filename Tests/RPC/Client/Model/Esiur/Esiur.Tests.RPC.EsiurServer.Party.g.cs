using System;
using Esiur.Resource;
using Esiur.Core;
using Esiur.Data;
using Esiur.Protocol;
namespace Esiur.Tests.RPC.EsiurServer
{
    [Remote("Esiur.Tests.RPC.EsiurServer.Party", "")]
    [Export]
    public class Party : IRecord
    {
        [Annotation("", "Address")]
        public Esiur.Tests.RPC.EsiurServer.Address Address { get; set; }

        [Annotation("", "String")]
        public string Email { get; set; }

        [Annotation("", "UInt64")]
        public ulong Id { get; set; }

        [Annotation("", "String")]
        public string Name { get; set; }

        [Annotation("", "String")]
        public string Phone { get; set; }

        [Annotation("", "String")]
        public string PreferredLanguage { get; set; }

        [Annotation("", "String")]
        public string TaxId { get; set; }

        public Client.SharedModel.Party ToShared()
        {
            return new Client.SharedModel.Party()
            {
                Address = Address.ToShared(),
                Email = Email,
                Id = Id,
                Name = Name,
                Phone = Phone,
                PreferredLanguage = PreferredLanguage,
                TaxId = TaxId,
            };
        }

        public Echo.ThriftModel.Party ToThrift()
        {
            return new Echo.ThriftModel.Party()
            {
                Address = Address.ToThrift(),
                Email = Email,
                Id = (long)Id,
                Name = Name,
                Phone = Phone,
                PreferredLanguage = PreferredLanguage,
                TaxId = TaxId
            };
        }

        public Esiur.Tests.RPC.Client.Grpc.Party ToGrpc()
        {
            return new Esiur.Tests.RPC.Client.Grpc.Party()
            {
                Address = Address.ToGrpc(),
                Email = Email,
                Id = Id,
                Name = Name,
                Phone = Phone,
                PreferredLanguage = PreferredLanguage ?? "",
                TaxId = TaxId ?? "",
            };
        }

        public override bool Equals(object? obj)
        {
            var other = obj as Party;
            if (other == null) return false;

            if (other.Id != Id) return false;

            if (other.TaxId != TaxId) return false;
            if (!other.Address.Equals(Address)) return false;
            if (other.Email != Email) return false;
            if (other.Name != Name) return false;
            if (other.Phone != Phone) return false;
            if (other.PreferredLanguage != PreferredLanguage) return false;

            return true;
        }

    }
}
