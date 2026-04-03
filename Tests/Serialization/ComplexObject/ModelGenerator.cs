using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

#nullable enable
namespace Esiur.Tests.Serialization;

public static class ModelGenerator
{
    public sealed class GenOptions
    {
        public int Lines { get; init; } = 20;         // items count
        public int Attachments { get; init; } = 0;    // 0..N
        public int AttachmentBytes { get; init; } = 0;// per attachment
        public int Payments { get; init; } = 1;       // 0..N
        public bool IncludeV2Fields { get; init; } = false;
        public bool IncludeUnicode { get; init; } = true;  // Arabic/emoji sprinkled in
        public int VariantPerLine { get; init; } = 1; // ad-hoc KV per line
        public Currency Currency { get; init; } = Currency.USD;

        public int RiskScores { get; set; } = 20;
        public int Seed { get; init; } = 12345;
    }

    public static BusinessDocument MakeBusinessDocument(GenOptions? options = null)
    {
        var opt = options ?? new GenOptions();
        var rng = new Random(opt.Seed);

        var seller = MakeParty(rng, opt.IncludeV2Fields, isSeller: true, opt.IncludeUnicode);
        var buyer = MakeParty(rng, opt.IncludeV2Fields, isSeller: false, opt.IncludeUnicode);

        var createdAt = DateTime.UtcNow.AddMinutes(-rng.Next(0, 60 * 24));
        var doc = new BusinessDocument
        {
            Header = new DocumentHeader
            {
                DocId = Guid.NewGuid().ToByteArray(),
                Type = (DocType)rng.Next(0, 4),
                Version = 1,
                CreatedAt = createdAt,
                UpdatedAt = null,
                Currency = opt.Currency,
                Notes = opt.IncludeUnicode ? SampleNoteUnicode(rng) : SampleNoteAscii(rng),
                Meta = new Dictionary<string, Variant>
                {
                    ["source"] = VStr("benchmark"),
                    ["region"] = VStr("ME"),
                    ["channel"] = VStr(rng.Next(0, 2) == 0 ? "online" : "pos"),
                }
            },
            Seller = seller,
            Buyer = buyer,
            Items = new LineItem[opt.Lines],
            Payments = opt.Payments > 0 ? new Payment[opt.Payments] : null,
            Attachments = opt.Attachments > 0 ? new Attachment[opt.Attachments] : null,

            RiskScores = RandomRiskScores(rng, opt.RiskScores),
            //RelatedDocs_v2 = opt.IncludeV2Fields ? new List<Guid> { Guid.NewGuid(), Guid.NewGuid() } : null
        };

        doc.Header.MetaValues = doc.Header.Meta.Values.ToArray();
        doc.Header.MetaKeys = doc.Header.Meta.Keys.ToArray();

        // Items
        for (int i = 0; i < opt.Lines; i++)
            doc.Items[i] = MakeLineItem(rng, i + 1, opt.IncludeV2Fields, opt.VariantPerLine, opt.IncludeUnicode);

        // Payments
        if (doc.Payments != null)
        {
            var grand = doc.Items.Sum(L => L.UnitPrice * L.Qty * (double)(1.0 - (L.Discount ?? 0.0) / 100.0));
            var remain = grand;
            for (int i = 0; i < opt.Payments; i++)
            {
                var last = (i == opt.Payments - 1);
                var amt = last ? remain : RoundMoney((double)rng.NextDouble() * remain * 0.7 + 1.0);
                remain = Math.Max(0.0, remain - amt);
                doc.Payments[i] = MakePayment(rng, amt, opt.IncludeV2Fields);
            }
        }

        // Attachments
        if (doc.Attachments != null)
        {
            for (int i = 0; i < opt.Attachments; i++)
                doc.Attachments[i] = MakeAttachment(rng, i, opt.AttachmentBytes);
        }

        return doc;
    }

    /// <summary>
    /// Create a slightly modified copy of an existing document to test delta/partial updates.
    /// Changes: tweak 5–10% of line items (qty/price), add or edit a payment, and bump UpdatedAt.
    /// </summary>
    public static BusinessDocument MakeDelta(BusinessDocument v1, int seed, double changeRatio = 0.07)
    {
        var rng = new Random(seed);
        var v2 = DeepClone(v1);

        v2.Header.UpdatedAt = DateTime.UtcNow;
        var toChange = Math.Max(1, (int)Math.Round(v2.Items.Length * changeRatio));

        // change random lines
        for (int i = 0; i < toChange; i++)
        {
            var idx = rng.Next(0, v2.Items.Length);
            var li = v2.Items[idx];
            li.Qty = RoundQty(li.Qty + (double)(rng.NextDouble() * 2.0 - 1.0)); // ±1
            li.UnitPrice = RoundMoney(li.UnitPrice * (double)(0.95 + rng.NextDouble() * 0.1)); // ±5%
            if (li.Ext == null) li.Ext = new Dictionary<string, Variant>();
            li.Ext["lastEdit"] = VDate(DateTime.UtcNow);
            li.ExtKeys = li.Ext.Keys.ToArray();
            li.ExtValues = li.Ext.Values.ToArray();
        }


        if (v2.Payments == null || rng.Next(0, 3) == 0)
        {
            if (v2.Payments == null)
            {
                if (v2.Payments == null) v2.Payments = new Payment[1];
                v2.Payments[0] = (MakePayment(rng, RoundMoney((double)rng.NextDouble() * 50.0 + 10.0), includeV2: true));

            }
            else
            {
                v2.Payments = v2.Payments.Append((MakePayment(rng, RoundMoney((double)rng.NextDouble() * 50.0 + 10.0), includeV2: true))).ToArray();
            }
        }
        else
        {
            var p = v2.Payments[rng.Next(0, v2.Payments.Length)];
            p.Fee = (p.Fee ?? 0.0) + 0.25;
            p.Reference = "ADJ-" + rng.Next(10000, 99999).ToString(CultureInfo.InvariantCulture);
        }

        return v2;
    }

    // -------------------------- Builders --------------------------

    private static int[] RandomRiskScores(Random rng, int count)
    {

        var rt = new int[count];// rng.Next(100, 1000)];
        for (var i = 0; i < rt.Length; i++)
            rt[i] = (int)rng.Next();
        return rt;
    }

    private static Party MakeParty(Random rng, bool includeV2, bool isSeller, bool unicode)
    {
        return new Party
        {
            Id = (ulong)rng.NextInt64(), //Guid.NewGuid().ToByteArray(),
            Name = unicode ? (isSeller ? "Delta Systems — دلتا" : "Client التجربة ✅") : (isSeller ? "Delta Systems" : "Client Demo"),
            TaxId = isSeller ? $"TX-{rng.Next(100000, 999999)}" : null,
            Email = (isSeller ? "sales" : "contact") + "@example.com",
            Phone = "+964-7" + rng.Next(100000000, 999999999).ToString(CultureInfo.InvariantCulture),
            Address = new Address
            {
                Line1 = rng.Next(0, 2) == 0 ? "Street 14" : "Tech Park",
                City = "Baghdad",
                Region = "BG",
                Country = "IQ",
                PostalCode = rng.Next(0, 2) == 0 ? "10001" : null
            },
            PreferredLanguage = includeV2 ? (rng.Next(0, 2) == 0 ? "ar-IQ" : "en-US") : null
        };
    }

    private static LineItem MakeLineItem(Random rng, int lineNo, bool includeV2, int variantKvp, bool unicode)
    {
        var isProduct = rng.Next(0, 100) < 80;
        var desc = unicode
            ? (isProduct ? $"وحدة قياس — Item {lineNo} 🔧" : $"خدمة دعم — Service {lineNo} ✨")
            : (isProduct ? $"Item {lineNo}" : $"Service {lineNo}");

        var li = new LineItem
        {
            LineNo = lineNo,
            Type = isProduct ? LineType.Product : LineType.Service,
            SKU = isProduct ? ("SKU-" + rng.Next(1000, 9999)) : "",
            Description = desc,
            Qty = RoundQty((double)(rng.NextDouble() * 9.0 + 1.0)), // 1..10
            QtyUnit = isProduct ? "pcs" : "h",
            UnitPrice = RoundMoney((double)(rng.NextDouble() * 90.0 + 10.0)),
            VatRate = rng.Next(0, 100) < 80 ? 5.0 : (double?)null,
            Ext = variantKvp > 0 ? new Dictionary<string, Variant>() : null,
            Discount= includeV2 && rng.Next(0, 3) == 0 ? Math.Round(rng.NextDouble() * 10.0, 2) : (double?)null
        };

        if (li.Ext != null)
        {
            li.ExtKeys = li.Ext.Keys.ToArray();
            li.ExtValues = li.Ext.Values.ToArray();
        } 
            

        for (int i = 0; i < variantKvp; i++)
        {
            var key = i switch { 0 => "color", 1 => "size", 2 => "batch", _ => "attr" + i };
            li.Ext!.TryAdd(key, i switch
            {
                0 => VStr(rng.Next(0, 3) switch { 0 => "red", 1 => "blue", _ => "green" }),
                1 => VStr(rng.Next(0, 3) switch { 0 => "S", 1 => "M", _ => "L" }),
                2 => VGuid(Guid.NewGuid()),
                _ => VInt(rng.Next(0, 1000))
            });
        }

        li.ExtValues = li.Ext.Values.ToArray();
        li.ExtKeys = li.Ext.Keys.ToArray();

        return li;
    }

    private static Payment MakePayment(Random rng, double amount, bool includeV2)
    {
        var p = new Payment
        {
            Method = (PaymentMethod)rng.Next(0, 5),
            Amount = RoundMoney(amount),
            Reference = "REF-" + rng.Next(100_000, 999_999),
            Timestamp = DateTime.UtcNow.AddMinutes(-rng.Next(0, 60 * 24)),
            Fee = includeV2 && rng.Next(0, 2) == 0 ? RoundMoney((double)rng.NextDouble() * 2.0) : null,
            //CurrencyOverride = includeV2 && rng.Next(0, 2) == 0 ? Currency.IQD : Currency.USD
        };

        p.TimestampAsLong = p.Timestamp.Ticks;

        return p;
    }

    private static Attachment MakeAttachment(Random rng, int index, int bytes)
    {
        var arr = bytes > 0 ? new byte[bytes] : Array.Empty<byte>();
        if (arr.Length > 0) rng.NextBytes(arr);
        return new Attachment
        {
            Name = $"att-{index + 1}.bin",
            MimeType = "application/octet-stream",
            Data = arr
        };
    }

    private static string SampleNoteUnicode(Random rng)
        => rng.Next(0, 2) == 0
            ? "ملاحظة: تم إنشاء هذا المستند لأغراض الاختبار 📦"
            : "Note: synthetic benchmark document 🧪";

    private static string SampleNoteAscii(Random rng)
        => rng.Next(0, 2) == 0 ? "Note: synthetic benchmark document" : "Internal use only";

    // -------------------------- Variant helpers --------------------------
    private static Variant VStr(string s) => new() { Tag = Variant.Kind.String, Str = s };
    private static Variant VInt(int v) => new() { Tag = Variant.Kind.Int64, I64 = v };
    private static Variant VGuid(Guid g) => new() { Tag = Variant.Kind.Guid, Guid = g.ToByteArray() };
    private static Variant VDate(DateTime d) => new() { Tag = Variant.Kind.DateTime, Dt = d, DtAsLong = d.Ticks };

    // -------------------------- Utils --------------------------
    private static double RoundMoney(double v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
    private static double RoundQty(double v) => Math.Round(v, 3, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Simple deep clone via manual copy to stay serializer-agnostic.
    /// (Good enough for benchmarks; switch to a fast serializer if you like.)
    /// </summary>
    private static BusinessDocument DeepClone(BusinessDocument s)
    {
        var d = new BusinessDocument
        {
            Header = new DocumentHeader
            {
                DocId = s.Header.DocId,
                Type = s.Header.Type,
                Version = s.Header.Version,
                CreatedAt = s.Header.CreatedAt,
                UpdatedAt = s.Header.UpdatedAt,
                //CreatedAtAsLong = s.Header.CreatedAtAsLong,
                //UpdatedAtAsLong = s.Header.UpdatedAtAsLong,
                Currency = s.Header.Currency,
                Notes = s.Header.Notes,
                Meta = s.Header.Meta?.ToDictionary(kv => kv.Key, kv => CloneVariant(kv.Value)),
                MetaKeys = s.Header.MetaKeys.ToArray(),
                MetaValues = s.Header.MetaValues.ToArray(),
            },
            Seller = CloneParty(s.Seller),
            Buyer = CloneParty(s.Buyer),
            Items = s.Items.Select(CloneLine).ToArray(),
            Payments = s.Payments?.Select(ClonePayment).ToArray(),
            Attachments = s.Attachments?.Select(CloneAttachment).ToArray(),
            RiskScores = s.RiskScores,
            //RelatedDocs_v2 = s.RelatedDocs_v2?.ToList()
        };

        
        return d;
    }

    private static Party CloneParty(Party p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        TaxId = p.TaxId,
        Email = p.Email,
        Phone = p.Phone,
        Address = p.Address is null ? null : new Address
        {
            Line1 = p.Address.Line1,
            Line2 = p.Address.Line2,
            City = p.Address.City,
            Region = p.Address.Region,
            Country = p.Address.Country,
            PostalCode = p.Address.PostalCode
        },
        PreferredLanguage = p.PreferredLanguage
    };

    private static LineItem CloneLine(LineItem s) => new()
    {
        LineNo = s.LineNo,
        Type = s.Type,
        SKU = s.SKU,
        Description = s.Description,
        Qty = s.Qty,
        QtyUnit = s.QtyUnit,
        UnitPrice = s.UnitPrice,
        VatRate = s.VatRate,
        Ext = s.Ext?.ToDictionary(kv => kv.Key, kv => CloneVariant(kv.Value)),
        Discount = s.Discount,
        ExtKeys = s.Ext?.Keys.ToArray(),
        ExtValues = s.Ext?.Values.ToArray(),
    };

    private static Payment ClonePayment(Payment s) => new()
    {
        Method = s.Method,
        Amount = s.Amount,
        Reference = s.Reference,
        Timestamp = s.Timestamp,
        TimestampAsLong = s.TimestampAsLong,
        Fee = s.Fee,
        //CurrencyOverride= s.CurrencyOverride
    };

    private static Attachment CloneAttachment(Attachment s) => new()
    {
        Name = s.Name,
        MimeType = s.MimeType,
        Data = s.Data.ToArray()
    };

    private static Variant CloneVariant(Variant v) => new()
    {
        Tag = v.Tag,
        Bool = v.Bool,
        I64 = v.I64,
        U64 = v.U64,
        F64 = v.F64,
        //Dec = v.Dec,
        Str = v.Str,
        Bytes = v.Bytes?.ToArray(),
        Dt = v.Dt,
        DtAsLong = v.DtAsLong,
        Guid = v.Guid
    };
}
