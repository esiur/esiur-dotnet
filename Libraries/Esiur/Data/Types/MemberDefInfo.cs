#nullable enable
using System.Collections.Generic;

namespace Esiur.Data.Types;

/// <summary>
/// Wire representation shared by all TypeDef members.
/// </summary>
public class MemberDefInfo : IndexedStructure
{
    [Index((int)MemberDefField.Index)]
    public byte Index { get; set; }

    [Index((int)MemberDefField.Name)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Raw member-specific flags. Interpret this as PropertyDefFlags, FunctionDefFlags,
    /// EventDefFlags, ConstantDefFlags, or ArgumentDefFlags according to the concrete type.
    /// </summary>
    [Index((int)MemberDefField.Flags)]
    public byte Flags { get; set; }

    [Index((int)MemberDefField.Description)]
    public string? Description { get; set; }

    [Index((int)MemberDefField.Usage)]
    public string? Usage { get; set; }

    [Index((int)MemberDefField.Examples)]
    public List<object>? Examples { get; set; }

    [Index((int)MemberDefField.Tags)]
    public List<string>? Tags { get; set; }

    [Index((int)MemberDefField.Unit)]
    public string? Unit { get; set; }

    [Index((int)MemberDefField.Minimum)]
    public object? Minimum { get; set; }

    [Index((int)MemberDefField.Maximum)]
    public object? Maximum { get; set; }

    [Index((int)MemberDefField.AllowedValues)]
    public List<object>? AllowedValues { get; set; }

    [Index((int)MemberDefField.Pattern)]
    public string? Pattern { get; set; }

    [Index((int)MemberDefField.Format)]
    public string? Format { get; set; }

    [Index((int)MemberDefField.Preconditions)]
    public List<string>? Preconditions { get; set; }

    [Index((int)MemberDefField.Postconditions)]
    public List<string>? Postconditions { get; set; }

    [Index((int)MemberDefField.Effects)]
    public OperationEffects Effects { get; set; }

    [Index((int)MemberDefField.Warnings)]
    public List<string>? Warnings { get; set; }

    [Index((int)MemberDefField.RelatedMembers)]
    public List<byte>? RelatedMembers { get; set; }

    [Index((int)MemberDefField.DeprecationMessage)]
    public string? DeprecationMessage { get; set; }

    [Index((int)MemberDefField.Annotations)]
    public Map<string, string>? Annotations { get; set; }
}
