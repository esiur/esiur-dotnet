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

    internal static ulong MultiplySaturated(ulong value, ulong multiplier)
        => value > ulong.MaxValue / multiplier ? ulong.MaxValue : value * multiplier;
}
