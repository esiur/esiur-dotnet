#nullable enable

namespace Esiur.Data.Types;

/// <summary>
/// Indexed function-argument definition carried inside <see cref="FunctionDefInfo"/>.
/// </summary>
public class ArgumentDefInfo : MemberDefInfo
{
    [Index((int)ArgumentDefField.ValueType)]
    public Tru ValueType { get; set; } = null!;

    [Index((int)ArgumentDefField.DefaultValue)]
    public object? DefaultValue { get; set; }
}
