#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace Esiur.Data.Types;

/// <summary>
/// Indexed, transport-safe representation of a type definition.
/// </summary>
public class TypeDefInfo : IndexedStructure
{
    [Index((int)TypeDefField.Version)]
    public int Version { get; set; }

    [Index((int)TypeDefField.Id)]
    public ulong Id { get; set; }

    [Index((int)TypeDefField.Name)]
    public string Name { get; set; } = string.Empty;

    [Index((int)TypeDefField.Namespace)]
    public string? Namespace { get; set; }

    [Index((int)TypeDefField.Kind)]
    public TypeDefKind Kind { get; set; }

    [Index((int)TypeDefField.Parent)]
    public ulong? Parent { get; set; }

    [Index((int)TypeDefField.Properties)]
    public List<PropertyDefInfo>? Properties { get; set; }

    [Index((int)TypeDefField.Functions)]
    public List<FunctionDefInfo>? Functions { get; set; }

    [Index((int)TypeDefField.Events)]
    public List<EventDefInfo>? Events { get; set; }

    [Index((int)TypeDefField.Constants)]
    public List<ConstantDefInfo>? Constants { get; set; }

    [Index((int)TypeDefField.Usage)]
    public string? Usage { get; set; }

    [Index((int)TypeDefField.Description)]
    public string? Description { get; set; }

    [Index((int)TypeDefField.Example)]
    public object? Example { get; set; }

    [Index((int)TypeDefField.Category)]
    public string? Category { get; set; }

    [Index((int)TypeDefField.Since)]
    public string? Since { get; set; }

    [Index((int)TypeDefField.Annotations)]
    public Map<string, string>? Annotations { get; set; }

    public static TypeDefInfo FromTypeDef(TypeDef definition)
    {
        return new TypeDefInfo
        {
            Version = definition.Version,
            Id = definition.Id,
            Name = definition.Name,
            Kind = definition.Kind,
            Parent = definition.ParentTypeId,
            Annotations = definition.Annotations,
            Properties = definition.Properties.Select(FromProperty).ToList(),
            Functions = definition.Functions.Select(FromFunction).ToList(),
            Events = definition.Events.Select(FromEvent).ToList(),
            Constants = definition.Constants.Select(FromConstant).ToList(),
        };
    }

    private static T CopyMember<T>(MemberDef source, T target) where T : MemberDefInfo
    {
        target.Index = source.Index;
        target.Name = source.Name;
        target.Description = source.Description;
        target.Usage = source.Usage;
        target.Examples = source.Examples;
        target.Tags = source.Tags;
        target.Unit = source.Unit;
        target.Minimum = source.Minimum;
        target.Maximum = source.Maximum;
        target.AllowedValues = source.AllowedValues;
        target.Pattern = source.Pattern;
        target.Format = source.Format;
        target.Preconditions = source.Preconditions;
        target.Postconditions = source.Postconditions;
        target.Effects = source.Effects;
        target.Warnings = source.Warnings;
        target.RelatedMembers = source.RelatedMembers;
        target.DeprecationMessage = source.DeprecationMessage;
        return target;
    }

    private static PropertyDefInfo FromProperty(PropertyDef source)
    {
        var flags = source.Inherited ? PropertyDefFlags.Inherited : PropertyDefFlags.None;
        if (source.ReadOnly) flags |= PropertyDefFlags.ReadOnly;
        if (source.Constant) flags |= PropertyDefFlags.Constant;
        if (source.Historical) flags |= PropertyDefFlags.Historical;

        return CopyMember(source, new PropertyDefInfo
        {
            Flags = (byte)flags,
            ValueType = source.ValueType,
            HistoryControl = source.Historical ? (byte)1 : (byte)0,
        });
    }

    private static FunctionDefInfo FromFunction(FunctionDef source)
    {
        var flags = source.Inherited ? FunctionDefFlags.Inherited : FunctionDefFlags.None;
        if (source.Deprecated) flags |= FunctionDefFlags.Deprecated;
        if (source.IsStatic) flags |= FunctionDefFlags.Static;
        if (source.ReadOnly) flags |= FunctionDefFlags.ReadOnly;
        if (source.Idempotent) flags |= FunctionDefFlags.Idempotent;
        if (source.Cancellable) flags |= FunctionDefFlags.Cancellable;

        return CopyMember(source, new FunctionDefInfo
        {
            Flags = (byte)flags,
            ReturnType = source.ReturnType,
            StreamMode = source.StreamMode,
            Arguments = source.Arguments?.Select(FromArgument).ToList(),
        });
    }

    private static ArgumentDefInfo FromArgument(ArgumentDef source)
    {
        return new ArgumentDefInfo
        {
            Index = checked((byte)source.Index),
            Name = source.Name,
            Flags = source.Optional ? (byte)ArgumentDefFlags.Optional : (byte)ArgumentDefFlags.None,
            ValueType = source.Type,
            DefaultValue = source.DefaultValue,
        };
    }

    private static EventDefInfo FromEvent(EventDef source)
    {
        var flags = source.Inherited ? EventDefFlags.Inherited : EventDefFlags.None;
        if (source.Deprecated) flags |= EventDefFlags.Deprecated;
        if (source.AutoDelivered) flags |= EventDefFlags.AutoDelivered;

        return CopyMember(source, new EventDefInfo
        {
            Flags = (byte)flags,
            ArgumentType = source.ArgumentType,
        });
    }

    private static ConstantDefInfo FromConstant(ConstantDef source)
    {
        var flags = source.Inherited ? ConstantDefFlags.Inherited : ConstantDefFlags.None;
        return CopyMember(source, new ConstantDefInfo
        {
            Flags = (byte)flags,
            ValueType = source.ValueType,
            Value = source.Value,
        });
    }
}
