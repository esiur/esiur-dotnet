#nullable enable

using Esiur.Data;
using Esiur.Resource;
using System;
using System.Collections.Generic;

namespace Esiur.Tests.RPC.EsiurServer
{
    // ====================== Enums ======================

    [Export]
    public enum Currency
    {
        IQD,
        CNH,
        USD,
        EUR,
        JPY,
        GBP
    }

    [Export]
    public enum DocType
    {
        Quote,
        Order,
        Invoice,
        CreditNote
    }

    [Export]
    public enum PaymentMethod
    {
        Cash,
        Card,
        Wire,
        Crypto,
        Other
    }

    [Export]
    public enum LineType
    {
        Product,
        Service,
        Discount,
        Shipping
    }

    // Variant.Kind
    [Export]
    public enum Kind
    {
        Null,
        Bool,
        Int64,
        UInt64,
        Double,
        Decimal,
        String,
        Bytes,
        DateTime,
        Guid
    }

    // ====================== Variant & Entry helpers ======================
    [Export]
    public sealed class Variant:IRecord
    {
        public Kind Tag { get; set; }

        public bool? Bool { get; set; }
        public long? I64 { get; set; }
        public ulong? U64 { get; set; }
        public double? F64 { get; set; }
        public string? Str { get; set; }
        public byte[]? Bytes { get; set; }
        public DateTime? Dt { get; set; }
        public byte[]? Guid { get; set; }
    }

    [Export]
    public sealed class MetaEntry:IRecord
    {
        public string Key { get; set; } = string.Empty;
        public Variant Value { get; set; } = new Variant();
    }

    [Export]
    public sealed class ExtEntry:IRecord
    {
        public string Key { get; set; } = string.Empty;
        public Variant Value { get; set; } = new Variant();
    }

    // ====================== Party & Address ======================
    [Export]
    public sealed class Address:IRecord
    {
        public string Line1 { get; set; } = string.Empty;
        public string? Line2 { get; set; }

        public string City { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string? PostalCode { get; set; }
    }

    [Export]
    public sealed class Party:IRecord
    {
        public ulong Id { get; set; }

        public string Name { get; set; } = string.Empty;
        public string? TaxId { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }

        public Address? Address { get; set; }

        public string? PreferredLanguage { get; set; }
    }

    // ====================== DocumentHeader ======================

    [Export]
    public sealed class DocumentHeader:IRecord
    {
        // Guid serialized as bytes
        public byte[] DocId { get; set; } = Array.Empty<byte>();

        public DocType Type { get; set; }
        public int Version { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public Currency Currency { get; set; }

        public string? Notes { get; set; }

        // corresponds to Dictionary<string, Variant>
        public Dictionary<string, Variant> Meta { get; set; } = new();
    }

    // ====================== LineItem, Payment, Attachment ======================

    [Export]
    public sealed class LineItem:IRecord
    {
        public int LineNo { get; set; }
        public LineType Type { get; set; }

        public string SKU { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public double Qty { get; set; }
        public string QtyUnit { get; set; } = string.Empty;

        public double UnitPrice { get; set; }

        public double? VatRate { get; set; }
        public double? Discount { get; set; }

        // Dictionary<string, Variant>
        public Map<string, Variant> Ext { get; set; } = new();
    }

    [Export]
    public sealed class Payment:IRecord
    {
        public PaymentMethod Method { get; set; }

        public double Amount { get; set; }
        public string? Reference { get; set; }

        public DateTime Timestamp { get; set; }

        public double? Fee { get; set; }

        public Currency Currency { get; set; }
    }

    [Export]
    public sealed class Attachment:IRecord
    {
        public string Name { get; set; } = string.Empty;
        public string MimeType { get; set; } = string.Empty;
        public byte[] Data { get; set; } = Array.Empty<byte>();
    }

    // ====================== Top-level BusinessDocument ======================
    [Export]
    public sealed class BusinessDocument:IRecord
    {
        public DocumentHeader Header { get; set; } = new DocumentHeader();

        public Party Seller { get; set; } = new Party();
        public Party Buyer { get; set; } = new Party();

        public LineItem[] Items { get; set; } = Array.Empty<LineItem>();
        public Payment[] Payments { get; set; } = Array.Empty<Payment>();
        public Attachment[] Attachments { get; set; } = Array.Empty<Attachment>();

        public int[] RiskScores { get; set; } = Array.Empty<int>();
    }
}
