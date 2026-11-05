#nullable enable
using System;
using Esiur.Data.Types;

namespace Esiur.Protocol;

/// <summary>
/// A member that compiled client code (a generated proxy) was built against does not exist on
/// the definition the node serves now. The member name is stable, its index is not: generated
/// proxies resolve the index by name at run time, so a node that renumbered or extended its
/// type keeps working, and a member the node removed fails with this error naming the member
/// and the type.
/// </summary>
public sealed class EpMemberUnavailableException : InvalidOperationException
{
    public string TypeName { get; }
    public string MemberName { get; }

    /// <summary>"function", "property" or "event".</summary>
    public string MemberKind { get; }

    public EpMemberUnavailableException(string typeName, string memberName, string memberKind, string? reason = null)
        : base(
            $"The {memberKind} '{memberName}' is no longer available on '{typeName}'" +
            (string.IsNullOrEmpty(reason) ? "" : $" ({reason})") +
            ". The server definition changed; update the client code that uses it.")
    {
        TypeName = typeName;
        MemberName = memberName;
        MemberKind = memberKind;
    }
}

/// <summary>
/// Resolves the current index of a member by name. Generated code carries the index it was
/// generated against only as a fast path: when the definition still has that name at that
/// index nothing is looked up.
/// </summary>
internal static class EpMemberBinding
{
    static void EnsureAvailable(TypeDef definition, string memberName, string kind)
    {
        if (definition is RemoteTypeDef { Removed: true })
            throw new EpMemberUnavailableException(definition.Name, memberName, kind, "the type no longer exists");
    }

    internal static byte Function(TypeDef definition, string name, byte generatedIndex)
    {
        EnsureAvailable(definition, name, "function");

        var atIndex = definition.GetFunctionDefByIndex(generatedIndex);
        if (atIndex != null && atIndex.Name == name)
            return generatedIndex;

        var byName = definition.GetFunctionDefByName(name)
                     ?? throw new EpMemberUnavailableException(definition.Name, name, "function");
        return byName.Index;
    }

    internal static byte Property(TypeDef definition, string name, byte generatedIndex)
    {
        EnsureAvailable(definition, name, "property");

        var atIndex = definition.GetPropertyDefByIndex(generatedIndex);
        if (atIndex != null && atIndex.Name == name)
            return generatedIndex;

        var byName = definition.GetPropertyDefByName(name)
                     ?? throw new EpMemberUnavailableException(definition.Name, name, "property");
        return byName.Index;
    }

    internal static byte Event(TypeDef definition, string name, byte generatedIndex)
    {
        EnsureAvailable(definition, name, "event");

        var atIndex = definition.GetEventDefByIndex(generatedIndex);
        if (atIndex != null && atIndex.Name == name)
            return generatedIndex;

        var byName = definition.GetEventDefByName(name)
                     ?? throw new EpMemberUnavailableException(definition.Name, name, "event");
        return byName.Index;
    }
}
