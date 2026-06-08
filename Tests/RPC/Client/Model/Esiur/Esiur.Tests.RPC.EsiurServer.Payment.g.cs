using System;
using Esiur.Resource;
using Esiur.Core;
using Esiur.Data;
using Esiur.Protocol;
namespace Esiur.Tests.RPC.EsiurServer
{
    [Remote("Esiur.Tests.RPC.EsiurServer.Payment", "")]
    [Export]
    public class Payment : IRecord
    {
        [Annotation("", "Double")]
        public double Amount { get; set; }

        [Annotation("", "Currency")]
        public Esiur.Tests.RPC.EsiurServer.Currency Currency { get; set; }

        [Annotation("", "Nullable`1?")]
        public double? Fee { get; set; }

        [Annotation("", "PaymentMethod")]
        public Esiur.Tests.RPC.EsiurServer.PaymentMethod Method { get; set; }

        [Annotation("", "String")]
        public string Reference { get; set; }

        [Annotation("", "DateTime")]
        public DateTime Timestamp { get; set; }


        public Client.SharedModel.Payment ToShared()
        {
            return new Client.SharedModel.Payment()
            {
                Amount = Amount,
                Currency = Enum.Parse<Client.SharedModel.Currency>(Currency.ToString(), true),
                Method = Enum.Parse<Client.SharedModel.PaymentMethod>(Method.ToString(), true),
                Reference = Reference,
                Timestamp = Timestamp,
                Fee = Fee,
            };
        }

        public Echo.ThriftModel.Payment ToThrift()
        {
            var rt = new Echo.ThriftModel.Payment()
            {
                Amount = Amount,
                Currency = Enum.Parse<Echo.ThriftModel.Currency>(Currency.ToString(), true),
                Method = Enum.Parse<Echo.ThriftModel.PaymentMethod>(Method.ToString(), true),
                Reference = Reference,
                Timestamp = Timestamp.Ticks,
            };

            if (Fee != null)
                rt.Fee = Fee.Value;

            return rt;
        }

        public Esiur.Tests.RPC.Client.Grpc.Payment ToGrpc()
        {
            return new Esiur.Tests.RPC.Client.Grpc.Payment()
            {
                Amount = Amount,
                Currency = Enum.Parse<Esiur.Tests.RPC.Client.Grpc.Currency>(Currency.ToString(), true),
                Fee = Fee ?? 0,
                Method = Enum.Parse<Esiur.Tests.RPC.Client.Grpc.PaymentMethod>(Method.ToString(), true),
                Reference = Reference,
                Timestamp = Timestamp.Ticks,
            };
        }
        public override bool Equals(object? obj)
        {
            var other = obj as Payment;

            if (other == null) return false;

            if (Method != other.Method) return false;
            if (Amount != other.Amount) return false;
            if (Reference != other.Reference) return false;
            if (Fee != other.Fee) return false;

            return true;
        }
    }
}
