using System;

namespace Esiur.Data;

/// <summary>
/// Assigns a stable wire index to a field or property of a <see cref="IndexedStructure"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field,
                AllowMultiple = false, Inherited = true)]
public sealed class IndexAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the byte-sized index used on the wire.
    /// </summary>
    public byte Index { get; set; }

    public IndexAttribute()
    {
    }

    public IndexAttribute(int index)
    {
        if (index < byte.MinValue || index > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(index),
                "A structure index must be between 0 and 255.");

        Index = (byte)index;
    }
}
