using System;

namespace Esiur.Net.Packets;

/// <summary>
/// Compatibility base for packet parsers and composers.
/// </summary>
public class Packet
{
    public byte[] Data;

    public virtual long Parse(byte[] data, uint offset, uint ends) => 0;

    public virtual bool Compose() => false;

    protected static void ValidateBounds(byte[] data, uint offset, uint ends)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        if (ends > data.Length)
            throw new ArgumentOutOfRangeException(nameof(ends));
        if (offset > ends)
            throw new ArgumentOutOfRangeException(nameof(offset));
    }

    protected static bool TryGetMissingBytes(
        uint offset,
        uint ends,
        uint needed,
        out long parseResult)
    {
        var available = ends - offset;
        if (available < needed)
        {
            parseResult = -(long)(needed - available);
            return true;
        }

        parseResult = 0;
        return false;
    }
}
