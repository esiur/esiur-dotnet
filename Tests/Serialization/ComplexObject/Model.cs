using System;
using System.Collections.Generic;
using ProtoBuf;
using MessagePack;
using FlatSharp.Attributes;
using Esiur.Data;
using Esiur.Resource;

namespace Esiur.Tests.Serialization;

#nullable enable

// ========================= Enums =========================
// (Optional) You can add [ProtoContract]/[ProtoEnum] if you want explicit enum numbering.
// FlatSharp works fine with standard C# enums.

[FlatBufferEnum(typeof(int))]
public enum Currency { USD, EUR, IQD, JPY, GBP }
[FlatBufferEnum(typeof(int))]
public enum DocType { Quote, Order, Invoice, CreditNote }
[FlatBufferEnum(typeof(int))]
public enum PaymentMethod { Cash, Card, Wire, Crypto, Other }
[FlatBufferEnum(typeof(int))]
public enum LineType { Product, Service, Discount, Shipping }

// ========================= Variant =========================
// NOTE (FlatBuffers): a structured union in .fbs is preferable.
// Here we annotate as requested; FlatSharp will compile but you’d typically replace this with a union/table family.

[ProtoContract]
[MessagePackObject(true)] // keyAsPropertyName = true, to avoid manual [Key] on every field here
[FlatBufferTable]
[Export]
public class Variant : IRecord
{
    [ProtoMember(1)]
    [FlatBufferItem(0)]
    public Kind Tag { get; set; }

    [ProtoMember(2)]
    [FlatBufferItem(1)]
    public bool? Bool { get; set; }

    [ProtoMember(3)]
    [FlatBufferItem(2)]
    public long? I64 { get; set; }

    [ProtoMember(4)]
    [FlatBufferItem(3)]
    public ulong? U64 { get; set; }

    [ProtoMember(5)]
    [FlatBufferItem(4)]
    public double? F64 { get; set; }

    //[ProtoMember(6)]
    //[FlatBufferItem(5)]
    //public double? Dec { get; set; }

    [ProtoMember(7)]
    [FlatBufferItem(6)]
    public string? Str { get; set; }

    [ProtoMember(8)]
    [FlatBufferItem(7)]
    public byte[]? Bytes { get; set; }

    [ProtoMember(9)]
    public DateTime? Dt { get; set; }

    [FlatBufferItem(8)]
    [Ignore, IgnoreMember]
    public long DtAsLong { get; set; }

    [ProtoMember(10)]
    [FlatBufferItem(9)]
    public byte[]? Guid { get; set; }

    [FlatBufferEnum(typeof(int))]
    public enum Kind { Null, Bool, Int64, UInt64, Double, Decimal, String, Bytes, DateTime, Guid }

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

        if (other.DtAsLong != DtAsLong) 
            return false;
        if (other.Dt != Dt) 
            return false;

        return true;
    }
}

// ========================= Address =========================

[ProtoContract]
[MessagePackObject]
[FlatBufferTable]
[Export]
public class Address : IRecord
{
    [ProtoMember(1)]
    [Key(0)]
    [FlatBufferItem(0)]
    public string Line1 { get; set; } = "";

    [ProtoMember(2)]
    [Key(1)]
    [FlatBufferItem(1)]
    public string? Line2 { get; set; }

    [ProtoMember(3)]
    [Key(2)]
    [FlatBufferItem(2)]
    public string City { get; set; } = "";

    [ProtoMember(4)]
    [Key(3)]
    [FlatBufferItem(3)]
    public string Region { get; set; } = "";

    [ProtoMember(5)]
    [Key(4)]
    [FlatBufferItem(4)]
    public string Country { get; set; } = "IQ";

    [ProtoMember(6)]
    [Key(5)]
    [FlatBufferItem(5)]
    public string? PostalCode { get; set; }

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
}

// ========================= Party =========================

[ProtoContract]
[MessagePackObject]
[FlatBufferTable]
[Export]
public class Party : IRecord
{
    [ProtoMember(1)]
    [Key(0)]
    [FlatBufferItem(0)]
    public ulong Id { get; set; }

    [ProtoMember(2)]
    [Key(1)]
    [FlatBufferItem(1)]
    public string Name { get; set; } = "";

    [ProtoMember(3)]
    [Key(2)]
    [FlatBufferItem(2)]
    public string? TaxId { get; set; }

    [ProtoMember(4)]
    [Key(3)]
    [FlatBufferItem(3)]
    public string? Email { get; set; }

    [ProtoMember(5)]
    [Key(4)]
    [FlatBufferItem(4)]
    public string? Phone { get; set; }

    [ProtoMember(6)]
    [Key(5)]
    [FlatBufferItem(5)]
    public Address? Address { get; set; }

    // v2 field
    [ProtoMember(7)]
    [Key(6)]
    [FlatBufferItem(6)]
    public string? PreferredLanguage { get; set; }

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

// ========================= LineItem =========================

[ProtoContract]
[MessagePackObject]
[FlatBufferTable]
[Export]
public class LineItem : IRecord
{
    [ProtoMember(1)]
    [Key(0)]
    [FlatBufferItem(0)]
    public int LineNo { get; set; }

    [ProtoMember(2)]
    [Key(1)]
    [FlatBufferItem(1)]
    public LineType Type { get; set; }

    [ProtoMember(3)]
    [Key(2)]
    [FlatBufferItem(2)]
    public string SKU { get; set; } = "";

    [ProtoMember(4)]
    [Key(3)]
    [FlatBufferItem(3)]
    public string Description { get; set; } = "";

    [ProtoMember(5)]
    [Key(4)]
    [FlatBufferItem(4)]

    public double Qty { get; set; }

    //[Ignore, IgnoreMember]
    //public double QtyAsDouble { get; set; }

    [ProtoMember(6)]
    [Key(5)]
    [FlatBufferItem(5)]
    public string QtyUnit { get; set; } = "pcs";

    [ProtoMember(7)]
    [Key(6)]
    [FlatBufferItem(6)]
    public double UnitPrice { get; set; }

    [ProtoMember(8)]
    [Key(7)]
    [FlatBufferItem(7)]
    public double? VatRate { get; set; }

    // NOTE (FlatBuffers): Dictionary is not native. Consider mapping to a vector of {Key, Value(Variant)} entries for real FlatBuffers use.
    [ProtoMember(9)]
    [Key(8)]
    public Dictionary<string, Variant>? Ext { get; set; }

    // v2 field
    [ProtoMember(10)]
    [Key(9)]
    [FlatBufferItem(8)]
    public double? Discount { get; set; }

    [FlatBufferItem(9), Ignore, IgnoreMember]
    public string[]? ExtKeys { get; set; }

    [FlatBufferItem(10), Ignore, IgnoreMember]
    public Variant[]? ExtValues { get; set; }

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

        if (other.ExtKeys == null)
            other.ExtKeys = other.Ext.Keys.ToArray();

        if (other.ExtValues == null)
            other.ExtValues = other.Ext.Values.ToArray();


        if (!other.ExtKeys.SequenceEqual(ExtKeys)) return false;
        if (!other.ExtValues.SequenceEqual(ExtValues)) return false;

        return true;
    }
}

// ========================= Payment =========================

[ProtoContract]
[MessagePackObject]
[FlatBufferTable]
[Export]
public class Payment : IRecord
{
    [ProtoMember(1)]
    [Key(0)]
    [FlatBufferItem(0)]
    public PaymentMethod Method { get; set; }

    [ProtoMember(2)]
    [Key(1)]
    [FlatBufferItem(1)]
    public double Amount { get; set; }

    [ProtoMember(3)]
    [Key(2)]
    [FlatBufferItem(2)]
    public string? Reference { get; set; }

    [ProtoMember(4)]
    [Key(3)]
    public DateTime Timestamp { get; set; }

    [FlatBufferItem(3), Ignore, IgnoreMember]
    public long TimestampAsLong { get; set; }

    // v2 fields
    [ProtoMember(5)]
    [Key(4)]
    [FlatBufferItem(4)]
    public double? Fee { get; set; }

    //[ProtoMember(6)]
    //[Key(5)]
    //[FlatBufferItem(5)]
    //public Currency Currency { get; set; }

    public override bool Equals(object? obj)
    {
        var other = obj as Payment;

        if (other == null) return false;

        if (Method != other.Method) return false;
        if (Amount != other.Amount) return false;
        if (Reference != other.Reference) return false;
        //if (Timestamp != other.Timestamp) return false;
        //if (TimestampAsLong != other.TimestampAsLong) return false;
        if (Fee != other.Fee) return false;
        //if (CurrencyOverride != other.CurrencyOverride) return false;

        return true;
    }
}

// ========================= Attachment =========================

[ProtoContract]
[MessagePackObject]
[FlatBufferTable]
[Export]
public class Attachment : IRecord
{
    [ProtoMember(1)]
    [Key(0)]
    [FlatBufferItem(0)]
    public string Name { get; set; } = "";

    [ProtoMember(2)]
    [Key(1)]
    [FlatBufferItem(1)]
    public string MimeType { get; set; } = "application/pdf";

    [ProtoMember(3)]
    [Key(2)]
    [FlatBufferItem(2)]
    public byte[] Data { get; set; } = Array.Empty<byte>();

    public override bool Equals(object? obj)
    {
        var other = obj as Attachment;
        if (Name != other.Name) return false;
        if (MimeType != other.MimeType) return false;
        if (!(Data.SequenceEqual(other.Data))) return false;

        return true;
    }
}

// ========================= DocumentHeader =========================

[ProtoContract]
[MessagePackObject]
[FlatBufferTable]
[Export]
public class DocumentHeader : IRecord
{
    [ProtoMember(1)]
    [Key(0)]
    [FlatBufferItem(0)]
    public byte[] DocId { get; set; }

    [ProtoMember(2)]
    [Key(1)]
    [FlatBufferItem(1)]
    public DocType Type { get; set; }

    [ProtoMember(3)]
    [Key(2)]
    [FlatBufferItem(2)]
    public int Version { get; set; }

    [ProtoMember(4)]
    [Key(3)]
    public DateTime CreatedAt { get; set; }

    [FlatBufferItem(3), Ignore, IgnoreMember]
    public long CreatedAtAsLong
    {
        get => CreatedAt.Ticks;
        set => CreatedAt = new DateTime(value);
    }

    [ProtoMember(5)]
    [Key(4)]
    public DateTime? UpdatedAt { get; set; }

    [FlatBufferItem(4), Ignore, IgnoreMember]
    public long? UpdatedAtAsLong
    {
        get => UpdatedAt?.Ticks ?? 0;
        set => UpdatedAt = value == null || value == 0 ? null : new DateTime(value.Value);
    }


    [ProtoMember(6)]
    [Key(5)]
    [FlatBufferItem(5)]
    public Currency Currency { get; set; }

    [ProtoMember(7)]
    [Key(6)]
    [FlatBufferItem(6)]
    public string? Notes { get; set; }

    [ProtoMember(8)]
    [Key(7)]
    public Dictionary<string, Variant>? Meta { get; set; }

    //  FlatBuffers: don't support dictionary.
    [FlatBufferItem(7), Ignore, IgnoreMember]
    public string[] MetaKeys { get; set; }
    [FlatBufferItem(8), Ignore, IgnoreMember]
    public Variant[] MetaValues { get; set; }

    public override bool Equals(object? obj)
    {
        var other = obj as DocumentHeader;

        if (other == null) return false;
        if (!DocId.SequenceEqual(other.DocId)) return false;
        if (Type != other.Type) return false;
        if (Version != other.Version) return false;
        //if (CreatedAtAsLong != other.CreatedAtAsLong) return false;
        //if (UpdatedAtAsLong != other.UpdatedAtAsLong) return false;
        if (CreatedAt != other.CreatedAt) return false;
        if (UpdatedAt != other.UpdatedAt) return false;

        if (Currency != other.Currency) return false;
        if (Notes != other.Notes) return false;

        if (other.MetaKeys == null)
            other.MetaKeys = other.Meta.Keys.ToArray();

        if (other.MetaValues == null)
            other.MetaValues = other.Meta.Values.ToArray();

        if (!MetaKeys.SequenceEqual(other.MetaKeys)) return false;
        if (!MetaValues.SequenceEqual(other.MetaValues)) return false;


        return true;
    }
}

// ========================= BusinessDocument (root) =========================

[ProtoContract]
[MessagePackObject]
[FlatBufferTable]
[Export]
public class BusinessDocument : IRecord
{

    [ProtoMember(1)]
    [Key(0)]
    [FlatBufferItem(0)]
    public DocumentHeader? Header { get; set; }

    [ProtoMember(2)]
    [Key(1)]
    [FlatBufferItem(1)]
    public Party? Seller { get; set; }

    [ProtoMember(3)]
    [Key(2)]
    [FlatBufferItem(2)]
    public Party? Buyer { get; set; }

    [ProtoMember(4)]
    [Key(3)]
    [FlatBufferItem(3)]
    public LineItem[]? Items { get; set; }

    [ProtoMember(5)]
    [Key(4)]
    [FlatBufferItem(4)]
    public Payment[]? Payments { get; set; }

    [ProtoMember(6)]
    [Key(5)]
    [FlatBufferItem(5)]
    public Attachment[]? Attachments { get; set; }

    [ProtoMember(7)]
    [Key(6)]
    [FlatBufferItem(6)]
    public int[]? RiskScores { get; set; }

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


    [FlatBufferTable]
    public class ArrayRoot<T>
    {
        // Field index must be stable; start at 0
        [FlatBufferItem(0)]
        public virtual IList<T>? Values { get; set; }
    }


}
