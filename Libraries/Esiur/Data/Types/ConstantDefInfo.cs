#nullable enable

namespace Esiur.Data.Types;

public class ConstantDefInfo : MemberDefInfo
{
    [Index((int)ConstantDefField.ValueType)]
    public Tru ValueType { get; set; } = null!;

    [Index((int)ConstantDefField.Value)]
    public object? Value { get; set; }
}
