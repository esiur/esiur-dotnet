#nullable enable

namespace Esiur.Data.Types;

public class PropertyDefInfo : MemberDefInfo
{
    [Index((int)PropertyDefField.ValueType)]
    public Tru ValueType { get; set; } = null!;

    [Index((int)PropertyDefField.OrderingControl)]
    public OrderingControl OrderingControl { get; set; }

    // Kept byte-sized until the protocol defines a HistoryControl enum.
    [Index((int)PropertyDefField.HistoryControl)]
    public byte HistoryControl { get; set; }

    [Index((int)PropertyDefField.DefaultValue)]
    public object? DefaultValue { get; set; }
}
