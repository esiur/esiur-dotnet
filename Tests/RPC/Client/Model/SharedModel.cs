#nullable enable

using System;
using System.Collections.Generic;

namespace Esiur.Tests.RPC.Client.SharedModel
{
    // ====================== Enums ======================

    public enum Currency
    {
        IQD,
        CNH,
        USD,
        EUR,
        JPY,
        GBP
    }

    public enum DocType
    {
        Quote,
        Order,
        Invoice,
        CreditNote
    }

    public enum PaymentMethod
    {
        Cash,
        Card,
        Wire,
        Crypto,
        Other
    }

    public enum LineType
    {
        Product,
        Service,
        Discount,
        Shipping
    }

    // Variant.Kind
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

    public sealed class Variant
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

    public sealed class MetaEntry
    {
        public string Key { get; set; } = string.Empty;
        public Variant Value { get; set; } = new Variant();
    }

    public sealed class ExtEntry
    {
        public string Key { get; set; } = string.Empty;
        public Variant Value { get; set; } = new Variant();
    }

    // ====================== Party & Address ======================

    public sealed class Address
    {
        public string Line1 { get; set; } = string.Empty;
        public string? Line2 { get; set; }

        public string City { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string? PostalCode { get; set; }
    }

    public sealed class Party
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

    public sealed class DocumentHeader
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

    public sealed class LineItem
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
        public Dictionary<string, Variant> Ext { get; set; } = new();
    }

    public sealed class Payment
    {
        public PaymentMethod Method { get; set; }

        public double Amount { get; set; }
        public string? Reference { get; set; }

        public DateTime Timestamp { get; set; }

        public double? Fee { get; set; }

        public Currency Currency { get; set; }
    }

    public sealed class Attachment
    {
        public string Name { get; set; } = string.Empty;
        public string MimeType { get; set; } = string.Empty;
        public byte[] Data { get; set; } = Array.Empty<byte>();
    }

    // ====================== Top-level BusinessDocument ======================

    public sealed class BusinessDocument
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
