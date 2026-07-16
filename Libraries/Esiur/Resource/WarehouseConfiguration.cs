using System;

namespace Esiur.Resource;

/// <summary>
/// Runtime configuration owned by a Warehouse instance.
/// </summary>
public sealed class WarehouseConfiguration
{
    public RateControlConfiguration RateControl { get; set; } = new RateControlConfiguration();
    public ParserConfiguration Parser { get; set; } = new ParserConfiguration();
    public ResourceAttachmentConfiguration ResourceAttachments { get; set; } = new ResourceAttachmentConfiguration();
    public ConnectionConfiguration Connections { get; set; } = new ConnectionConfiguration();
    public EncryptionConfiguration Encryption { get; set; } = new EncryptionConfiguration();
}

/// <summary>
/// Bounds encrypted transport records before any peer-controlled allocation occurs.
/// A value of zero disables the limit.
/// </summary>
public sealed class EncryptionConfiguration
{
    public uint MaximumRecordSize { get; set; } = 8 * 1024 * 1024 + 1024;
}

/// <summary>
/// Limits memory and object amplification while parsing untrusted packets.
/// A value of zero disables the corresponding limit.
/// </summary>
public sealed class ParserConfiguration
{
    /// <summary>Default maximum nesting depth for TRU type metadata.</summary>
    public const int DefaultMaximumTypeMetadataDepth = 64;

    /// <summary>Maximum declared TDU payload retained for one packet.</summary>
    public uint MaximumPacketSize { get; set; } = 8 * 1024 * 1024;

    /// <summary>Maximum allocation produced by one decoded value.</summary>
    public uint MaximumAllocationSize { get; set; } = 4 * 1024 * 1024;

    /// <summary>Maximum number of values decoded into one collection.</summary>
    public int MaximumCollectionItems { get; set; } = 65_536;

    /// <summary>
    /// Maximum number of nested TRU type-metadata nodes, including the root node.
    /// A value of zero disables the limit.
    /// </summary>
    public int MaximumTypeMetadataDepth { get; set; } = DefaultMaximumTypeMetadataDepth;
}

/// <summary>
/// Limits resources imported from, or attached by, one remote connection.
/// A value of zero disables the corresponding limit.
/// </summary>
public sealed class ResourceAttachmentConfiguration
{
    public int MaximumAttachedResourcesPerConnection { get; set; } = 4_096;
    public int MaximumPendingAttachmentsPerConnection { get; set; } = 128;
    public bool RejectDuplicateAttachments { get; set; } = true;
}

/// <summary>
/// Configures network connection admission limits.
/// A value of zero disables the corresponding limit.
/// </summary>
public sealed class ConnectionConfiguration
{
    public int MaximumConnectionsPerIpAddress { get; set; } = 64;

    /// <summary>
    /// Maximum connection attempts admitted from one IP during
    /// <see cref="ConnectionAttemptWindow"/>. This limits repeated pre-authentication
    /// cryptographic and identity-lookup work. A value of zero disables the limit.
    /// </summary>
    public int MaximumConnectionAttemptsPerIpAddress { get; set; } = 120;

    public TimeSpan ConnectionAttemptWindow { get; set; } = TimeSpan.FromMinutes(1);
}

/// <summary>
/// Configures how repeated rate-control denials affect an EP connection.
/// </summary>
public sealed class RateControlConfiguration
{
    /// <summary>
    /// Number of denials within <see cref="DenialWindow"/> that blocks the connection.
    /// Set to zero to disable connection blocking.
    /// </summary>
    public int DenialsBeforeConnectionBlock { get; set; } = 5;

    /// <summary>
    /// Window in which denials are accumulated for connection blocking.
    /// </summary>
    public TimeSpan DenialWindow { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Delay before a blocked connection is closed, allowing its final error reply to flush.
    /// </summary>
    public TimeSpan ConnectionBlockDelay { get; set; } = TimeSpan.FromMilliseconds(100);
}
