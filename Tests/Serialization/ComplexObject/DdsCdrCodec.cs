using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace Esiur.Tests.ComplexModel;

// ============================================================================
// DdsCdrCodec.cs
// ----------------------------------------------------------------------------
// Implements ICodec by encoding BusinessDocument as OMG XCDR2 (Extended CDR
// version 2, PLAIN_CDR2, all types FINAL). This is the on-the-wire payload
// format used by every conformant DDS implementation per DDS-XTypes 1.3 for
// final-extensibility types.
//
// The corresponding IDL definition lives in BusinessDocument.idl alongside
// this file. The struct layout, member order, and union discriminator
// values below MUST match the IDL exactly; a divergence between this code
// and the IDL would produce a wire format incompatible with real DDS
// implementations, defeating the purpose of the benchmark.
//
// Encoding choices documented inline. Where the spec offered multiple
// equivalent options (e.g., DateTime as int64 ticks vs. struct), the choice
// that minimizes wire size was selected, so that this measurement reports
// the most favorable DDS payload size achievable for this schema.
// ============================================================================

public sealed class DdsCdrCodec : ICodec
{
    public string Name => "DDS-XCDR2";

    public byte[]? Serialize(BusinessDocument obj)
    {
        var w = new Xcdr2Writer(capacity: 8192);
        WriteBusinessDocument(w, obj);
        var bytes = w.ToArray();

        // Optional self-check (cheap): decode and compare. Mirrors the
        // pattern used by FlatBuffersCodec and ProtobufCodec in
        // ModelRunner.cs for outside-the-timing-loop validation.
        // Disabled by default to keep parity with most other codecs; the
        // outer harness performs equality testing.
        return bytes;
    }

    public BusinessDocument Deserialize(byte[] data)
    {
        var r = new Xcdr2Reader(data);
        return ReadBusinessDocument(r);
    }

    // ---- BusinessDocument (FINAL struct, top-level) -----------------------
    //
    // In XCDR2 the top-level FINAL struct does NOT carry a DHEADER. It is
    // emitted as a plain concatenation of members in declaration order.
    // (DDS-XTypes 1.3 §7.4.3.5.3 rule 7: FINAL aggregated types use
    // PLAIN_CDR2 without delimiter.)

    private static void WriteBusinessDocument(Xcdr2Writer w, BusinessDocument d)
    {
        // member 0: optional<DocumentHeader> Header
        WriteOptionalStruct(w, d.Header, WriteDocumentHeader);
        // member 1: optional<Party> Seller
        WriteOptionalStruct(w, d.Seller, WriteParty);
        // member 2: optional<Party> Buyer
        WriteOptionalStruct(w, d.Buyer, WriteParty);
        // member 3: sequence<LineItem> Items
        WriteSequenceOfStruct(w, d.Items, WriteLineItem);
        // member 4: sequence<Payment> Payments
        WriteSequenceOfStruct(w, d.Payments, WritePayment);
        // member 5: sequence<Attachment> Attachments
        WriteSequenceOfStruct(w, d.Attachments, WriteAttachment);
        // member 6: sequence<int32> RiskScores  (primitive, no DHEADER)
        WriteSequenceOfInt32(w, d.RiskScores);
    }

    private static BusinessDocument ReadBusinessDocument(Xcdr2Reader r)
    {
        var d = new BusinessDocument
        {
            Header = ReadOptionalStruct(r, ReadDocumentHeader),
            Seller = ReadOptionalStruct(r, ReadParty),
            Buyer = ReadOptionalStruct(r, ReadParty),
            Items = ReadSequenceOfStruct(r, ReadLineItem),
            Payments = ReadSequenceOfStruct(r, ReadPayment),
            Attachments = ReadSequenceOfStruct(r, ReadAttachment),
            RiskScores = ReadSequenceOfInt32(r),
        };
        return d;
    }

    // ---- DocumentHeader ---------------------------------------------------

    private static void WriteDocumentHeader(Xcdr2Writer w, DocumentHeader h)
    {
        // member 0: sequence<octet> DocId  (primitive seq, no DHEADER)
        w.WriteOctetSequence(h.DocId ?? Array.Empty<byte>());
        // member 1: DocType Type  (enum -> int32)
        w.WriteInt32((int)h.Type);
        // member 2: int32 Version
        w.WriteInt32(h.Version);
        // member 3: int64 CreatedAtTicks
        w.WriteInt64(h.CreatedAt.Ticks);
        // member 4: optional<int64> UpdatedAtTicks
        if (h.UpdatedAt.HasValue)
        {
            w.WriteBool(true);
            w.WriteInt64(h.UpdatedAt.Value.Ticks);
        }
        else
        {
            w.WriteBool(false);
        }
        // member 5: Currency  (enum -> int32)
        w.WriteInt32((int)h.Currency);
        // member 6: optional<string> Notes
        WriteOptionalString(w, h.Notes);
        // member 7: sequence<MetaEntry> Meta  (non-primitive seq -> DHEADER)
        WriteVariantDictionary(w, h.Meta);
    }

    private static DocumentHeader ReadDocumentHeader(Xcdr2Reader r)
    {
        var h = new DocumentHeader();
        h.DocId = r.ReadOctetSequence();
        h.Type = (DocType)r.ReadInt32();
        h.Version = r.ReadInt32();
        h.CreatedAt = new DateTime(r.ReadInt64());
        h.UpdatedAt = r.ReadBool() ? new DateTime(r.ReadInt64()) : (DateTime?)null;
        h.Currency = (Currency)r.ReadInt32();
        h.Notes = ReadOptionalString(r);
        h.Meta = ReadVariantDictionary(r);
        if (h.Meta != null)
        {
            h.MetaKeys = h.Meta.Keys.ToArray();
            h.MetaValues = h.Meta.Values.ToArray();
        }
        return h;
    }

    // ---- Party ------------------------------------------------------------

    private static void WriteParty(Xcdr2Writer w, Party p)
    {
        w.WriteUInt64(p.Id);
        w.WriteString(p.Name);
        WriteOptionalString(w, p.TaxId);
        WriteOptionalString(w, p.Email);
        WriteOptionalString(w, p.Phone);
        // optional<Address>
        if (p.Address != null)
        {
            w.WriteBool(true);
            WriteAddress(w, p.Address);
        }
        else
        {
            w.WriteBool(false);
        }
        WriteOptionalString(w, p.PreferredLanguage);
    }

    private static Party ReadParty(Xcdr2Reader r)
    {
        return new Party
        {
            Id = r.ReadUInt64(),
            Name = r.ReadString(),
            TaxId = ReadOptionalString(r),
            Email = ReadOptionalString(r),
            Phone = ReadOptionalString(r),
            Address = r.ReadBool() ? ReadAddress(r) : null,
            PreferredLanguage = ReadOptionalString(r),
        };
    }

    // ---- Address ----------------------------------------------------------

    private static void WriteAddress(Xcdr2Writer w, Address a)
    {
        w.WriteString(a.Line1);
        WriteOptionalString(w, a.Line2);
        w.WriteString(a.City);
        w.WriteString(a.Region);
        w.WriteString(a.Country);
        WriteOptionalString(w, a.PostalCode);
    }

    private static Address ReadAddress(Xcdr2Reader r)
    {
        return new Address
        {
            Line1 = r.ReadString(),
            Line2 = ReadOptionalString(r),
            City = r.ReadString(),
            Region = r.ReadString(),
            Country = r.ReadString(),
            PostalCode = ReadOptionalString(r),
        };
    }

    // ---- LineItem ---------------------------------------------------------

    private static void WriteLineItem(Xcdr2Writer w, LineItem li)
    {
        w.WriteInt32(li.LineNo);
        w.WriteInt32((int)li.Type);
        w.WriteString(li.SKU);
        w.WriteString(li.Description);
        w.WriteDouble(li.Qty);
        w.WriteString(li.QtyUnit);
        w.WriteDouble(li.UnitPrice);
        WriteOptionalDouble(w, li.VatRate);
        WriteOptionalDouble(w, li.Discount);
        WriteVariantDictionary(w, li.Ext);
    }

    private static LineItem ReadLineItem(Xcdr2Reader r)
    {
        var li = new LineItem
        {
            LineNo = r.ReadInt32(),
            Type = (LineType)r.ReadInt32(),
            SKU = r.ReadString(),
            Description = r.ReadString(),
            Qty = r.ReadDouble(),
            QtyUnit = r.ReadString(),
            UnitPrice = r.ReadDouble(),
            VatRate = ReadOptionalDouble(r),
            Discount = ReadOptionalDouble(r),
            Ext = ReadVariantDictionary(r),
        };
        if (li.Ext != null)
        {
            li.ExtKeys = li.Ext.Keys.ToArray();
            li.ExtValues = li.Ext.Values.ToArray();
        }
        return li;
    }

    // ---- Payment ----------------------------------------------------------

    private static void WritePayment(Xcdr2Writer w, Payment p)
    {
        w.WriteInt32((int)p.Method);
        w.WriteDouble(p.Amount);
        WriteOptionalString(w, p.Reference);
        w.WriteInt64(p.Timestamp.Ticks);
        WriteOptionalDouble(w, p.Fee);
    }

    private static Payment ReadPayment(Xcdr2Reader r)
    {
        return new Payment
        {
            Method = (PaymentMethod)r.ReadInt32(),
            Amount = r.ReadDouble(),
            Reference = ReadOptionalString(r),
            Timestamp = new DateTime(r.ReadInt64()),
            Fee = ReadOptionalDouble(r),
        };
    }

    // ---- Attachment -------------------------------------------------------

    private static void WriteAttachment(Xcdr2Writer w, Attachment a)
    {
        w.WriteString(a.Name);
        w.WriteString(a.MimeType);
        w.WriteOctetSequence(a.Data);
    }

    private static Attachment ReadAttachment(Xcdr2Reader r)
    {
        return new Attachment
        {
            Name = r.ReadString(),
            MimeType = r.ReadString(),
            Data = r.ReadOctetSequence(),
        };
    }

    // ---- Variant (union discriminated by Kind) ----------------------------
    //
    // IDL mapping (see BusinessDocument.idl):
    //   union Variant switch(int32 /* Kind */) {
    //     case 0 (Null):     /* no member */;
    //     case 1 (Bool):     boolean   b;
    //     case 2 (Int64):    int64     i64;
    //     case 3 (UInt64):   uint64    u64;
    //     case 4 (Double):   double    d;
    //     case 6 (String):   string    s;
    //     case 7 (Bytes):    sequence<octet> by;
    //     case 8 (DateTime): int64     dt;     // ticks
    //     case 9 (Guid):     octet[16] g;
    //   };
    //
    // XCDR2 union encoding: int32 discriminator + the selected branch.

    private static void WriteVariant(Xcdr2Writer w, Variant v)
    {
        int tag = (int)v.Tag;
        w.WriteInt32(tag);
        switch (v.Tag)
        {
            case Variant.Kind.Null:
                break;
            case Variant.Kind.Bool:
                w.WriteBool(v.Bool ?? false);
                break;
            case Variant.Kind.Int64:
                w.WriteInt64(v.I64 ?? 0);
                break;
            case Variant.Kind.UInt64:
                w.WriteUInt64(v.U64 ?? 0);
                break;
            case Variant.Kind.Double:
            case Variant.Kind.Decimal:  // Decimal mapped to double in IDL
                w.WriteDouble(v.F64 ?? 0.0);
                break;
            case Variant.Kind.String:
                w.WriteString(v.Str ?? "");
                break;
            case Variant.Kind.Bytes:
                w.WriteOctetSequence(v.Bytes ?? Array.Empty<byte>());
                break;
            case Variant.Kind.DateTime:
                w.WriteInt64(v.Dt?.Ticks ?? v.DtAsLong);
                break;
            case Variant.Kind.Guid:
                w.WriteOctetArrayFixed(v.Guid ?? new byte[16], 16);
                break;
            default:
                throw new InvalidOperationException($"Unknown Variant kind {v.Tag}");
        }
    }

    private static Variant ReadVariant(Xcdr2Reader r)
    {
        var tag = (Variant.Kind)r.ReadInt32();
        var v = new Variant { Tag = tag };
        switch (tag)
        {
            case Variant.Kind.Null:
                break;
            case Variant.Kind.Bool:
                v.Bool = r.ReadBool();
                break;
            case Variant.Kind.Int64:
                v.I64 = r.ReadInt64();
                break;
            case Variant.Kind.UInt64:
                v.U64 = r.ReadUInt64();
                break;
            case Variant.Kind.Double:
            case Variant.Kind.Decimal:
                v.F64 = r.ReadDouble();
                break;
            case Variant.Kind.String:
                v.Str = r.ReadString();
                break;
            case Variant.Kind.Bytes:
                v.Bytes = r.ReadOctetSequence();
                break;
            case Variant.Kind.DateTime:
                {
                    long ticks = r.ReadInt64();
                    v.Dt = new DateTime(ticks);
                    v.DtAsLong = ticks;
                    break;
                }
            case Variant.Kind.Guid:
                v.Guid = r.ReadOctetArrayFixed(16);
                break;
            default:
                throw new InvalidOperationException($"Unknown Variant kind {tag}");
        }
        return v;
    }

    // ---- Dictionary<string, Variant> -> sequence<MetaEntry> ---------------
    //
    // IDL: struct MetaEntry { string key; Variant value; };
    //      sequence<MetaEntry> ...
    //
    // Non-primitive sequence: per XCDR2 rule 15, MUST emit DHEADER before
    // the sequence length and elements.

    private static void WriteVariantDictionary(Xcdr2Writer w, Dictionary<string, Variant>? dict)
    {
        int token = w.BeginDHeader();
        if (dict == null)
        {
            w.WriteUInt32(0);
        }
        else
        {
            w.WriteUInt32((uint)dict.Count);
            foreach (var kv in dict)
            {
                w.WriteString(kv.Key);
                WriteVariant(w, kv.Value);
            }
        }
        w.EndDHeader(token);
    }

    private static Dictionary<string, Variant>? ReadVariantDictionary(Xcdr2Reader r)
    {
        _ = r.ReadDHeader(); // size, ignored (schema-driven decode)
        uint n = r.ReadUInt32();
        if (n == 0) return new Dictionary<string, Variant>();
        var d = new Dictionary<string, Variant>((int)n);
        for (uint i = 0; i < n; i++)
        {
            var k = r.ReadString();
            var v = ReadVariant(r);
            d[k] = v;
        }
        return d;
    }

    // ---- helpers: optional<T> and sequence<T> -----------------------------

    private static void WriteOptionalString(Xcdr2Writer w, string? s)
    {
        if (s is null) { w.WriteBool(false); }
        else { w.WriteBool(true); w.WriteString(s); }
    }

    private static string? ReadOptionalString(Xcdr2Reader r)
        => r.ReadBool() ? r.ReadString() : null;

    private static void WriteOptionalDouble(Xcdr2Writer w, double? v)
    {
        if (v is null) { w.WriteBool(false); }
        else { w.WriteBool(true); w.WriteDouble(v.Value); }
    }

    private static double? ReadOptionalDouble(Xcdr2Reader r)
        => r.ReadBool() ? r.ReadDouble() : null;

    private static void WriteOptionalStruct<T>(
        Xcdr2Writer w, T? value, Action<Xcdr2Writer, T> writeInner)
        where T : class
    {
        if (value is null) { w.WriteBool(false); }
        else { w.WriteBool(true); writeInner(w, value); }
    }

    private static T? ReadOptionalStruct<T>(
        Xcdr2Reader r, Func<Xcdr2Reader, T> readInner)
        where T : class
    {
        return r.ReadBool() ? readInner(r) : null;
    }

    private static void WriteSequenceOfStruct<T>(
        Xcdr2Writer w, T[]? arr, Action<Xcdr2Writer, T> writeInner)
    {
        // Non-primitive sequence: DHEADER required.
        int token = w.BeginDHeader();
        if (arr is null)
        {
            w.WriteUInt32(0);
        }
        else
        {
            w.WriteUInt32((uint)arr.Length);
            for (int i = 0; i < arr.Length; i++)
                writeInner(w, arr[i]);
        }
        w.EndDHeader(token);
    }

    private static T[] ReadSequenceOfStruct<T>(
        Xcdr2Reader r, Func<Xcdr2Reader, T> readInner)
    {
        _ = r.ReadDHeader();
        uint n = r.ReadUInt32();
        var arr = new T[n];
        for (uint i = 0; i < n; i++)
            arr[i] = readInner(r);
        return arr;
    }

    private static void WriteSequenceOfInt32(Xcdr2Writer w, int[]? arr)
    {
        // Primitive sequence: NO DHEADER per XCDR2 rule 14.
        if (arr is null)
        {
            w.WriteUInt32(0);
        }
        else
        {
            w.WriteUInt32((uint)arr.Length);
            for (int i = 0; i < arr.Length; i++)
                w.WriteInt32(arr[i]);
        }
    }

    private static int[] ReadSequenceOfInt32(Xcdr2Reader r)
    {
        uint n = r.ReadUInt32();
        var arr = new int[n];
        for (uint i = 0; i < n; i++)
            arr[i] = r.ReadInt32();
        return arr;
    }
}