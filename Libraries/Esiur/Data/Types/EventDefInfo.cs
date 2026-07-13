#nullable enable

namespace Esiur.Data.Types;

public class EventDefInfo : MemberDefInfo
{
    [Index((int)EventDefField.ArgumentType)]
    public Tru ArgumentType { get; set; } = null!;

    [Index((int)EventDefField.ArgumentName)]
    public string? ArgumentName { get; set; }

    [Index((int)EventDefField.OrderingControl)]
    public OrderingControl OrderingControl { get; set; }

    // Kept byte-sized until the protocol defines a HistoryControl enum.
    [Index((int)EventDefField.HistoryControl)]
    public byte HistoryControl { get; set; }
}
