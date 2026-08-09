using System;

namespace Esiur.Resource;

/// <summary>
/// Identifies an exact position in a resource's ordered change stream.
/// Generation changes when the stream is recreated; revision increases for
/// every observable property modification or event occurrence.
/// </summary>
public readonly struct ResourceCursor : IEquatable<ResourceCursor>
{
    public Guid Generation { get; }
    public ulong Revision { get; }

    public ResourceCursor(Guid generation, ulong revision)
    {
        Generation = generation;
        Revision = revision;
    }

    public bool IsEmpty => Generation == Guid.Empty && Revision == 0;

    public bool Equals(ResourceCursor other) =>
        Generation == other.Generation && Revision == other.Revision;

    public override bool Equals(object obj) =>
        obj is ResourceCursor other && Equals(other);

    public override int GetHashCode() =>
        (Generation.GetHashCode() * 397) ^ Revision.GetHashCode();

    public override string ToString() => $"{Generation:N}:{Revision}";

    public static bool operator ==(ResourceCursor left, ResourceCursor right) => left.Equals(right);
    public static bool operator !=(ResourceCursor left, ResourceCursor right) => !left.Equals(right);
}
