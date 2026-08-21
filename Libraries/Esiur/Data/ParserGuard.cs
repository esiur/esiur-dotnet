using Esiur.Protocol;
using Esiur.Resource;
using System;
using System.IO;

namespace Esiur.Data;

/// <summary>
/// Raised when untrusted input exceeds a configured parser budget.
/// </summary>
public sealed class ParserLimitException : Exception
{
    public ParserLimitException(string message) : base(message)
    {
    }
}

/// <summary>
/// Raised before transmission when a composed value exceeds a budget advertised by the peer.
/// </summary>
public sealed class RemoteParserLimitException : Exception
{
    public RemoteParserLimitException(string message) : base(message)
    {
    }
}

internal static class ParserGuard
{
    internal static Warehouse? GetWarehouse(EpConnection? connection)
        => connection?.ParsingWarehouse;

    internal static void EnsurePacketSize(Warehouse? warehouse, ulong size)
    {
        var limit = warehouse?.Configuration.Parser.MaximumPacketSize ?? 0;
        if (limit > 0 && size > limit)
            throw new ParserLimitException(
                $"Declared packet payload of {size} bytes exceeds the {limit}-byte limit.");
    }

    internal static void EnsureAllocation(Warehouse? warehouse, ulong size, string kind)
    {
        var limit = warehouse?.Configuration.Parser.MaximumAllocationSize ?? 0;
        if (limit > 0 && size > limit)
            throw new ParserLimitException(
                $"Decoded {kind} allocation of {size} bytes exceeds the {limit}-byte limit.");
    }

    internal static void EnsureCollectionCount(
        Warehouse? warehouse,
        int count,
        int estimatedBytesPerItem = 0)
    {
        var configuration = warehouse?.Configuration.Parser;
        if (configuration == null)
            return;

        if (configuration.MaximumCollectionItems > 0 && count > configuration.MaximumCollectionItems)
            throw new ParserLimitException(
                $"Decoded collection count of {count} exceeds the {configuration.MaximumCollectionItems}-item limit.");

        if (estimatedBytesPerItem > 0)
            EnsureAllocation(
                warehouse,
                (ulong)count * (ulong)estimatedBytesPerItem,
                "collection");
    }

    internal static void EnsureTypeMetadataDepth(Warehouse? warehouse, int depth)
    {
        // Some compatibility entry points accept a null Warehouse. Keep those safe by
        // applying the built-in default rather than silently disabling this stack guard.
        var limit = warehouse?.Configuration.Parser.MaximumTypeMetadataDepth
                    ?? ParserConfiguration.DefaultMaximumTypeMetadataDepth;

        if (limit > 0 && depth > limit)
            throw new ParserLimitException(
                $"TRU type metadata depth of {depth} exceeds the configured limit of {limit}.");
    }

    internal static ulong MultiplySaturated(ulong value, ulong multiplier)
        => value > ulong.MaxValue / multiplier ? ulong.MaxValue : value * multiplier;

    internal static void EnsureRemotePacketSize(EpConnection? connection, ulong size)
    {
        var limit = connection?.Session?.RemoteMaximumPacketSize ?? 0;
        if (limit > 0 && size > limit)
            throw new RemoteParserLimitException(
                $"Composed packet payload of {size} bytes exceeds the peer's advertised {limit}-byte limit.");
    }

    internal static void EnsureRemoteAllocation(
        EpConnection? connection,
        ulong size,
        string kind)
    {
        var limit = connection?.Session?.RemoteMaximumAllocationSize ?? 0;
        if (limit > 0 && size > limit)
            throw new RemoteParserLimitException(
                $"Composed {kind} would allocate {size} bytes at the peer, exceeding its advertised {limit}-byte limit.");
    }

    internal static void EnsureRemoteCollectionCount(
        EpConnection? connection,
        int count,
        int estimatedBytesPerItem = 0)
    {
        var limit = connection?.Session?.RemoteMaximumCollectionItems ?? 0;
        if (limit > 0 && count > limit)
            throw new RemoteParserLimitException(
                $"Composed collection has {count} items, exceeding the peer's advertised {limit}-item limit.");

        if (estimatedBytesPerItem > 0)
            EnsureRemoteAllocation(
                connection,
                MultiplySaturated((ulong)count, (ulong)estimatedBytesPerItem),
                "collection");
    }

    internal static void EnsureRemoteTypeMetadataDepth(EpConnection? connection, Tru? tru)
    {
        var limit = connection?.Session?.RemoteMaximumTypeMetadataDepth ?? 0;
        if (limit <= 0 || tru == null)
            return;

        var depth = GetTypeMetadataDepth(tru);
        if (depth > limit)
            throw new RemoteParserLimitException(
                $"Composed TRU type metadata depth of {depth} exceeds the peer's advertised limit of {limit}.");
    }

    static int GetTypeMetadataDepth(Tru tru)
    {
        if (tru is not TruComposite composite || composite.SubTypes == null || composite.SubTypes.Length == 0)
            return 1;

        var maximumChildDepth = 0;
        foreach (var child in composite.SubTypes)
            maximumChildDepth = Math.Max(maximumChildDepth, GetTypeMetadataDepth(child));

        return 1 + maximumChildDepth;
    }
}
