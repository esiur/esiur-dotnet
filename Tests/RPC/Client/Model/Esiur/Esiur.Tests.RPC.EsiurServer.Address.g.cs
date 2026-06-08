using System;
using Esiur.Resource;
using Esiur.Core;
using Esiur.Data;
using Esiur.Protocol;

namespace Esiur.Tests.RPC.EsiurServer
{
    [Remote("Esiur.Tests.RPC.EsiurServer.Address", "")]
    [Export]
    public class Address : IRecord
    {
        [Annotation("", "String")]
        public string City { get; set; }

        [Annotation("", "String")]
        public string Country { get; set; }

        [Annotation("", "String")]
        public string Line1 { get; set; }

        [Annotation("", "String")]
        public string Line2 { get; set; }

        [Annotation("", "String")]
        public string PostalCode { get; set; }

        [Annotation("", "String")]
        public string Region { get; set; }

        public override bool Equals(object? obj)
        {
            var other = obj as Address;
            if (other == null) return false;
            if (other.Line1 != Line1) return false;
            if (other.Line2 != Line2) return false;
            if (other.PostalCode != PostalCode) return false;
            if (other.City != City) return false;
            if (other.Country != Country) return false;
            if (other.Region != Region) return false;

            return true;
        }

        public Client.SharedModel.Address ToShared()
        {
            return new Client.SharedModel.Address()
            {
                City = City,
                Country = Country,
                Line1 = Line1,
                Line2 = Line2,
                PostalCode = PostalCode,
                Region = Region,

            };
        }

        public Esiur.Tests.RPC.Client.Grpc.Address ToGrpc()
        {
            return new Esiur.Tests.RPC.Client.Grpc.Address()
            {
                City = City,
                Country = Country,
                Line1 = Line1,
                Line2 = Line2 ?? "",
                PostalCode = PostalCode ?? "",
                Region = Region,
            };
        }

        public Echo.ThriftModel.Address ToThrift()
        {
            return new Echo.ThriftModel.Address()
            {
                City = City,
                Country = Country,
                Line1 = Line1,
                Line2 = Line2,
                PostalCode = PostalCode,
                Region = Region,
            };
        }

    }
}
