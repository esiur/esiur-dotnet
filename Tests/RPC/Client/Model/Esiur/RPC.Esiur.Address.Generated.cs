using System;
using Esiur.Resource;
using Esiur.Core;
using Esiur.Data;
using Esiur.Protocol;
namespace RPC.EsiurTest
{
    [TypeId("0f8f447ee993847189b2c1ad6f83931a")]
    [Export]
    public class Address : IRecord
    {
        [Annotation("String")]
        public string City { get; set; }

        [Annotation("String")]
        public string Country { get; set; }

        [Annotation("String")]
        public string Line1 { get; set; }

        [Annotation("String")]
        public string? Line2 { get; set; }

        [Annotation("String")]
        public string? PostalCode { get; set; }

        [Annotation("String")]
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

        public SharedModel.Address ToShared()
        {
            return new SharedModel.Address()
            {
                City = City,
                Country = Country,
                Line1 = Line1,
                Line2 = Line2,
                PostalCode = PostalCode,
                Region = Region,

            };
        }

        public Echo.Model.Grpc.Address ToGrpc()
        {
            return new Echo.Model.Grpc.Address()
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
