#nullable enable
using System.Collections.Generic;

namespace Esiur.Data.Types;

/// <summary>
/// The member name to index mapping of a definition at one point in time. Member names are the
/// stable identity of a member; indexes are only valid for one definition, so state keyed by
/// index is re-keyed through the layout captured before a definition changed.
/// </summary>
internal sealed class TypeDefLayout
{
    internal Dictionary<string, byte> Properties { get; } = new();
    internal Dictionary<string, byte> Functions { get; } = new();
    internal Dictionary<string, byte> Events { get; } = new();

    internal static TypeDefLayout Of(TypeDef definition)
    {
        var layout = new TypeDefLayout();
        foreach (var member in definition.Properties)
            layout.Properties[member.Name] = member.Index;
        foreach (var member in definition.Functions)
            layout.Functions[member.Name] = member.Index;
        foreach (var member in definition.Events)
            layout.Events[member.Name] = member.Index;
        return layout;
    }
}
