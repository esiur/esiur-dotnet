using System;
using Esiur.Resource;
using Esiur.Core;
using Esiur.Data;
using Esiur.Protocol;
namespace RPC.EsiurTest
{
    [TypeId("44fff9c7bd9b86f580bf479a64cb84af")]
    [Export]
    public class Party : IRecord
    {
        [Annotation("Address")]
        public RPC.EsiurTest.Address Address { get; set; }

        [Annotation("String")]
        public string Email { get; set; }

        [Annotation("UInt64")]
        public ulong Id { get; set; }

        [Annotation("String")]
        public string Name { get; set; }

        [Annotation("String")]
        public string Phone { get; set; }

        [Annotation("String")]
        public string PreferredLanguage { get; set; }

        [Annotation("String")]
        public string TaxId { get; set; }

        public SharedModel.Party ToShared()
        {
            return new SharedModel.Party()
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

        public Echo.Model.Grpc.Party ToGrpc()
        {
            return new Echo.Model.Grpc.Party()
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
