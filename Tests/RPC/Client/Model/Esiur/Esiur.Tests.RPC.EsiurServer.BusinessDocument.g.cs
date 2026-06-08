using System;
using Esiur.Resource;
using Esiur.Core;
using Esiur.Data;
using Esiur.Protocol;
namespace Esiur.Tests.RPC.EsiurServer
{
    [Remote("Esiur.Tests.RPC.EsiurServer.BusinessDocument", "localhost")]
    [Export]
    public class BusinessDocument : IRecord
    {
        [Annotation("", "Attachment[]")]
        public Esiur.Tests.RPC.EsiurServer.Attachment[] Attachments { get; set; }

        [Annotation("", "Party")]
        public Esiur.Tests.RPC.EsiurServer.Party Buyer { get; set; }

        [Annotation("", "DocumentHeader")]
        public Esiur.Tests.RPC.EsiurServer.DocumentHeader Header { get; set; }

        [Annotation("", "LineItem[]")]
        public Esiur.Tests.RPC.EsiurServer.LineItem[] Items { get; set; }

        [Annotation("", "Payment[]")]
        public Esiur.Tests.RPC.EsiurServer.Payment[] Payments { get; set; }

        [Annotation("", "Int32[]")]
        public int[] RiskScores { get; set; }

        [Annotation("", "Party")]
        public Esiur.Tests.RPC.EsiurServer.Party Seller { get; set; }

        public override bool Equals(object? obj)
        {
            var other = obj as BusinessDocument;
            if (other == null)
                return false;


            if (!Header.Equals(other.Header))
                return false;
            if (!Seller.Equals(other.Seller))
                return false;
            if (!Buyer.Equals(other.Buyer))
                return false;

            if (Items != null)
                for (var i = 0; i < Items.Length; i++)
                    if (!Items[i].Equals(other.Items[i]))
                        return false;

            if (Payments != null)
                for (var i = 0; i < Payments.Length; i++)
                    if (!Payments[i].Equals(other.Payments[i]))
                        return false;

            if (Attachments != null)
                for (var i = 0; i < Attachments.Length; i++)
                    if (!Attachments[i].Equals(other.Attachments[i]))
                        return false;

            if (!RiskScores.SequenceEqual(other.RiskScores))
                return false;

            return true;
        }


        public Client.SharedModel.BusinessDocument ToShared()
        {
            return new Client.SharedModel.BusinessDocument()
            {
                Attachments = Attachments?.Select(x => x.ToShared()).ToArray() ?? null,
                Buyer = Buyer.ToShared(),
                Header = Header.ToShared(),
                Items = Items.Select(x => x.ToShared()).ToArray(),
                Payments = Payments.Select(x => x.ToShared()).ToArray(),
                RiskScores = RiskScores,
                Seller = Seller.ToShared(),
            };
        }

        public Echo.ThriftModel.BusinessDocument ToThrift()
        {
            var rt = new Echo.ThriftModel.BusinessDocument();

            if (Header != null)
                rt.Header = Header.ToThrift();

            if (Buyer != null)
                rt.Buyer = Buyer.ToThrift();

            if (Seller != null)
                rt.Seller = Seller.ToThrift();

            if (Attachments != null)
                rt.Attachments = Attachments.Select(x => x.ToThrift()).ToList();

            if (RiskScores != null)
                rt.RiskScores = RiskScores.ToList();

            if (Items != null)
                rt.Items = Items.Select(x => x.ToThrift()).ToList();

            if (Payments != null)
                rt.Payments = Payments.Select(x => x.ToThrift()).ToList();

            return rt;
        }

        public Esiur.Tests.RPC.Client.Grpc.BusinessDocument ToGrpc()
        {

            var rt = new Esiur.Tests.RPC.Client.Grpc.BusinessDocument()
            {
                Header = Header.ToGrpc(),
                Buyer = Buyer.ToGrpc(),
                Seller = Seller.ToGrpc(),
            };


            if (Payments != null)
                foreach (var p in Payments)
                    rt.Payments.Add(p.ToGrpc());

            if (Attachments != null)
                foreach (var p in Attachments)
                    rt.Attachments.Add(p.ToGrpc());

            if (Items != null)
                foreach (var p in Items)
                    rt.Items.Add(p.ToGrpc());

            if (RiskScores != null)
                foreach (var p in RiskScores)
                    rt.RiskScores.Add(p);

            return rt;
        }

    }
}
