using Esiur.Data;

namespace Esiur.CLI.Generation;

/// <summary>
/// Maps a wire-level <see cref="Tru"/> type representation onto the closest
/// TypeScript type that esiur-ts's runtime actually decodes it into (e.g.
/// 64-bit integers become `bigint`, not `number` — see
/// esiur-ts/src/data/DataDeserializer.ts) so generated stubs match runtime
/// values, not just wire shape.
/// </summary>
public static class TypeScriptTypeMapper
{
    /// <param name="names">Maps a referenced TypeDef's id to the identifier name
    /// it was declared under (see <see cref="TypeScriptStubGenerator"/>) — a
    /// TypeDef's own <c>Name</c> is often a fully-qualified CLR name like
    /// <c>MyApp.Server.Counter</c>, not a valid TypeScript identifier.</param>
    public static string Map(Tru? type, IReadOnlyDictionary<ulong, string> names)
    {
        if (type is null) return "unknown";
        var mapped = MapCore(type, names);
        return type.Nullable && mapped != "void" ? $"{mapped} | null" : mapped;
    }

    static string MapCore(Tru type, IReadOnlyDictionary<ulong, string> names) => type switch
    {
        TruTypeDef { TypeDef: not null } reference =>
            names.TryGetValue(reference.TypeDef.Id, out var name) ? name : "unknown",
        TruTypeDef => "unknown",
        TruComposite composite => MapComposite(composite, names),
        TruPrimitive primitive => MapPrimitive(primitive.Identifier),
        _ => "unknown",
    };

    static string MapComposite(TruComposite composite, IReadOnlyDictionary<ulong, string> names) =>
        composite.Identifier switch
        {
            TruIdentifier.TypedList => $"{Map(composite.SubTypes[0], names)}[]",
            TruIdentifier.TypedMap =>
                $"Map<{Map(composite.SubTypes[0], names)}, {Map(composite.SubTypes[1], names)}>",
            TruIdentifier.Tuple2 or TruIdentifier.Tuple3 or TruIdentifier.Tuple4
                or TruIdentifier.Tuple5 or TruIdentifier.Tuple6 or TruIdentifier.Tuple7 =>
                $"[{string.Join(", ", composite.SubTypes.Select(sub => Map(sub, names)))}]",
            _ => "unknown",
        };

    static string MapPrimitive(TruIdentifier identifier) => identifier switch
    {
        TruIdentifier.Void => "void",
        TruIdentifier.Dynamic => "unknown",
        TruIdentifier.Bool => "boolean",
        TruIdentifier.Char => "string",
        TruIdentifier.UInt8 or TruIdentifier.Int8
            or TruIdentifier.UInt16 or TruIdentifier.Int16
            or TruIdentifier.UInt32 or TruIdentifier.Int32
            or TruIdentifier.Float32 or TruIdentifier.Float64 => "number",
        // esiur-ts decodes 64-bit integers as `bigint` to avoid precision loss.
        TruIdentifier.Int64 or TruIdentifier.UInt64 => "bigint",
        // esiur-ts decodes Decimal as its own Decimal128 class (exported from "esiur").
        TruIdentifier.Decimal => "Decimal128",
        TruIdentifier.String => "string",
        TruIdentifier.DateTime => "Date",
        TruIdentifier.RawData => "Uint8Array",
        TruIdentifier.Resource => "unknown",
        TruIdentifier.Record => "unknown",
        TruIdentifier.List => "unknown[]",
        TruIdentifier.Map => "Map<unknown, unknown>",
        _ => "unknown",
    };

    /// <summary>True when any type reachable from <paramref name="type"/> maps to Decimal128,
    /// so the emitter knows whether to import it.</summary>
    public static bool UsesDecimal128(Tru? type) => type switch
    {
        null => false,
        TruPrimitive primitive => primitive.Identifier == TruIdentifier.Decimal,
        TruComposite composite => composite.SubTypes.Any(UsesDecimal128),
        _ => false,
    };
}
