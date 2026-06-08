using System;
using Esiur.Resource;
using Esiur.Core;
using Esiur.Data;
using Esiur.Protocol;
namespace Esiur.Tests.RPC.EsiurServer
{
    [Remote("Esiur.Tests.RPC.EsiurServer.LineItem", "")]
    [Export]
    public class LineItem : IRecord
    {
        [Annotation("", "String")]
        public string Description { get; set; }

        [Annotation("", "Nullable`1?")]
        public double? Discount { get; set; }

        [Annotation("", "Map`2")]
        public Map<string, Esiur.Tests.RPC.EsiurServer.Variant> Ext { get; set; }

        [Annotation("", "Int32")]
        public int LineNo { get; set; }

        [Annotation("", "Double")]
        public double Qty { get; set; }

        [Annotation("", "String")]
        public string QtyUnit { get; set; }

        [Annotation("", "String")]
        public string SKU { get; set; }

        [Annotation("", "LineType")]
        public Esiur.Tests.RPC.EsiurServer.LineType Type { get; set; }

        [Annotation("", "Double")]
        public double UnitPrice { get; set; }

        [Annotation("", "Nullable`1?")]
        public double? VatRate { get; set; }


        public Client.SharedModel.LineItem ToShared()
        {
            return new Client.SharedModel.LineItem()
            {
                Description = Description,
                Discount = Discount,
                Ext = Ext.ToDictionary(k => k.Key, v => v.Value.ToShared()),
                LineNo = LineNo,
                Qty = Qty,
                QtyUnit = QtyUnit,
                SKU = SKU,
                Type = Enum.Parse<Client.SharedModel.LineType>(Type.ToString(), true),
                UnitPrice = UnitPrice,
                VatRate = VatRate
            };
        }
        public Echo.ThriftModel.LineItem ToThrift()
        {
            var rt = new Echo.ThriftModel.LineItem()
            {
                Description = Description,
                LineNo = LineNo,
                Qty = Qty,
                UnitPrice = UnitPrice,
                QtyUnit = QtyUnit,
                Sku = SKU,
                Type = Enum.Parse<Echo.ThriftModel.LineType>(Type.ToString(), true),
            };

            if (Discount != null)
                rt.Discount = Discount.Value;
            if (VatRate != null)
                rt.VatRate = VatRate.Value;

            if (Ext != null)
                rt.Ext = Ext.ToDictionary(x => x.Key, v => v.Value.ToThrift());

            return rt;
        }

        public Esiur.Tests.RPC.Client.Grpc.LineItem ToGrpc()
        {
            var rt = new Esiur.Tests.RPC.Client.Grpc.LineItem()
            {
                Description = Description,
                Discount = Discount ?? 0,
                LineNo = LineNo,
                Qty = Qty,
                UnitPrice = UnitPrice,
                QtyUnit = QtyUnit,
                Sku = SKU,
                Type = Enum.Parse<Esiur.Tests.RPC.Client.Grpc.LineType>(Type.ToString(), true),
                VatRate = VatRate ?? 0,
            };

            if (Ext != null)
            {
                foreach (var kv in Ext)
                    rt.Ext.Add(kv.Key, kv.Value.ToGrpc());
            }

            return rt;
        }

        public override bool Equals(object? obj)
        {
            var other = obj as LineItem;
            if (other == null) return false;
            if (other.LineNo != LineNo) return false;
            if (other.SKU != SKU) return false;
            if (other.Description != Description) return false;
            if (other.Discount != Discount) return false;
            if (other.QtyUnit != QtyUnit) return false;
            if (other.Type != Type) return false;
            if (other.VatRate != VatRate) return false;
            if (other.UnitPrice != UnitPrice) return false;


            if (Ext != null)
            {
                foreach (var kv in Ext)
                    if (!other.Ext[kv.Key].Equals(kv.Value))
                        return false;
            }

            return true;
        }

    }
}
