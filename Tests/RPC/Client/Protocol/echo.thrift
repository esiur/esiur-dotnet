namespace netstd Echo.ThriftModel

// ====================== Enums ======================

enum Currency {
  IQD = 0,
  CNH = 1,
  USD = 2,
  EUR = 3,
  JPY = 4,
  GBP = 5
}

enum DocType {
  Quote      = 0,
  Order      = 1,
  Invoice    = 2,
  CreditNote = 3
}

enum PaymentMethod {
  Cash   = 0,
  Card   = 1,
  Wire   = 2,
  Crypto = 3,
  Other  = 4
}

enum LineType {
  Product  = 0,
  Service  = 1,
  Discount = 2,
  Shipping = 3
}

// Variant.Kind
enum Kind {
  Null      = 0,
  Bool      = 1,
  Int64     = 2,
  UInt64    = 3,
  Double    = 4,
  Decimal   = 5,
  String    = 6,
  Bytes     = 7,
  DateTime  = 8,
  Guid      = 9
}

// ====================== Core Value Types ======================

// C# Variant:
// Tag: Kind
// Bool: bool?      -> optional bool
// I64: long?       -> optional i64
// U64: ulong?      -> optional i64 (store unsigned in signed range or with convention)
// F64: double?     -> optional double
// Str: string?     -> optional string
// Bytes: byte[]?   -> optional binary
// Dt: DateTime?    -> optional i64 (e.g., Unix epoch millis or ticks)
// Guid: byte[]?    -> optional binary
struct Variant {
  1:  Kind   tag,
  2:  optional bool   boolVal,
  3:  optional i64    i64Val,
  4:  optional i64    u64Val,
  5:  optional double f64Val,
  6:  optional string strVal,
  7:  optional binary bytesVal,
  8:  optional i64    dtVal,
  9:  optional binary guidVal
}

// Optional helper view of a meta/ext entry (not strictly required,
// since we use map<string, Variant> below, but kept to mirror the graph).
struct MetaEntry {
  1: string  key,
  2: Variant value
}

struct ExtEntry {
  1: string  key,
  2: Variant value
}

// ====================== Party & Address ======================

struct Address {
  1: string  line1,
  2: optional string line2,
  3: string  city,
  4: string  region,
  5: string  country,
  6: optional string postalCode
}

struct Party {
  // C# ulong -> i64 (assumes IDs fit in signed 64-bit)
  1: i64     id,
  2: string  name,
  3: optional string taxId,
  4: optional string email,
  5: optional string phone,
  6: optional Address address,
  7: optional string preferredLanguage
}

// ====================== Document Header ======================

// DocId: byte[]
// Type: DocType
// Version: int
// CreatedAt: DateTime      -> i64 (e.g. Unix ms)
// UpdatedAt: DateTime?     -> optional i64
// Currency: Currency
// Notes: string?
// Meta: Dictionary<string, Variant> -> map<string, Variant>
struct DocumentHeader {
  1:  binary              docId,
  2:  DocType             type,
  3:  i32                 version,
  4:  i64                 createdAt,
  5:  optional i64        updatedAt,
  6:  Currency            currency,
  7:  optional string     notes,
  8:  map<string,Variant> meta
}

// ====================== Line Items, Payments, Attachments ======================

struct LineItem {
  1:  i32                 lineNo,
  2:  LineType            type,
  3:  string              sku,
  4:  string              description,
  5:  double              qty,
  6:  string              qtyUnit,
  7:  double              unitPrice,
  8:  optional double     vatRate,
  9:  optional double     discount,
  10: map<string,Variant> ext
}

struct Payment {
  1: PaymentMethod  method,
  2: double         amount,
  3: optional string reference,
  // DateTime -> i64 (e.g. Unix epoch millis or ticks)
  4: i64           timestamp,
  5: optional double fee,
  6: Currency      currency
}

struct Attachment {
  1: string name,
  2: string mimeType,
  3: binary data
}

// ====================== Top-Level Document ======================

struct BusinessDocument {
  1: DocumentHeader      header,
  2: Party               seller,
  3: Party               buyer,
  4: list<LineItem>      items,
  5: list<Payment>       payments,
  6: list<Attachment>    attachments,
  7: list<i32>           riskScores
}

service EchoService {
  binary EchoBytes(1: binary data),
  list<BusinessDocument> EchoDocuments(1: list<BusinessDocument> docs),
  list<i32> EchoIntArray(1: list<i32> array),
  list<string> EchoStringArray(1: list<string> array),
  map<string,BusinessDocument> EchoMap(1: map<string,BusinessDocument> map),
  list<DocType> EchoEnumArray(1: list<DocType> docTypes)
}
