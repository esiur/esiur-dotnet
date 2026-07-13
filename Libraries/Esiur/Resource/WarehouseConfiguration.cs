using System;

namespace Esiur.Resource;

/// <summary>
/// Runtime configuration owned by a Warehouse instance.
/// </summary>
public sealed class WarehouseConfiguration
{
    public RateControlConfiguration RateControl { get; set; } = new RateControlConfiguration();
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
