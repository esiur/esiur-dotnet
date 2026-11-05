#nullable enable
using System.Collections.Generic;
using Esiur.Data;
using Esiur.Data.Types;

namespace Esiur.Protocol;

// Wire structures for EpPacketRequest.SyncTypeDefs. See docs/typedef-sync.md.

/// <summary>One definition in a client's cache, as sent in a <c>SyncTypeDefs</c> request.</summary>
public sealed class TypeDefCacheEntry : IndexedStructure
{
    /// <summary>The id the client last used for this definition.</summary>
    [Index(0)]
    public ulong Id { get; set; }

    /// <summary>Full class name; the identity of the definition.</summary>
    [Index(1)]
    public string Name { get; set; } = string.Empty;

    [Index(2)]
    public TypeDefKind Kind { get; set; }

    [Index(3)]
    public int Version { get; set; }

    /// <summary>The signature the node sent with the cached definition.</summary>
    [Index(4)]
    public ulong Signature { get; set; }
}

/// <summary>A cached definition whose signature is unchanged but whose id differs.</summary>
public sealed class TypeDefIdMove : IndexedStructure
{
    [Index(0)]
    public ulong OldId { get; set; }

    [Index(1)]
    public ulong NewId { get; set; }
}

/// <summary>The node's answer to <c>SyncTypeDefs</c>. Unchanged entries are not mentioned.</summary>
public sealed class TypeDefSyncInfo : IndexedStructure
{
    [Index(0)]
    public List<TypeDefIdMove>? Moved { get; set; }

    /// <summary>Cached ids that no longer exist, or that the requester may not view.</summary>
    [Index(1)]
    public List<ulong>? Removed { get; set; }

    /// <summary>Definitions whose signature differs, complete and with their new ids.</summary>
    [Index(2)]
    public List<TypeDefUpdate>? Updated { get; set; }
}

/// <summary>A cached definition that changed: the client replaces it in place.</summary>
public sealed class TypeDefUpdate : IndexedStructure
{
    [Index(0)]
    public ulong OldId { get; set; }

    [Index(1)]
    public ulong NewId { get; set; }

    /// <summary>The complete definition, exactly as <c>TypeDefById</c> returns it.</summary>
    [Index(2)]
    public byte[] Definition { get; set; } = System.Array.Empty<byte>();
}
