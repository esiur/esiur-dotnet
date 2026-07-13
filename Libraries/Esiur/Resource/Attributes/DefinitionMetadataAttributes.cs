using Esiur.Data.Types;
using System;

namespace Esiur.Resource;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface |
    AttributeTargets.Enum | AttributeTargets.Method | AttributeTargets.Property |
    AttributeTargets.Field | AttributeTargets.Event | AttributeTargets.Parameter,
    AllowMultiple = false, Inherited = true)]
public sealed class DescriptionAttribute : Attribute
{
    public string Value { get; }

    public DescriptionAttribute(string value) => Value = value;
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface |
    AttributeTargets.Enum | AttributeTargets.Method | AttributeTargets.Property |
    AttributeTargets.Field | AttributeTargets.Event | AttributeTargets.Parameter,
    AllowMultiple = true, Inherited = true)]
public sealed class ExampleAttribute : Attribute
{
    public object Value { get; }

    public ExampleAttribute(object value) => Value = value;
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct |
    AttributeTargets.Interface | AttributeTargets.Enum,
    AllowMultiple = false, Inherited = true)]
public sealed class CategoryAttribute : Attribute
{
    public string Value { get; }

    public CategoryAttribute(string value) => Value = value;
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct |
    AttributeTargets.Interface | AttributeTargets.Enum,
    AllowMultiple = false, Inherited = true)]
public sealed class SinceAttribute : Attribute
{
    public string Value { get; }

    public SinceAttribute(string value) => Value = value;
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property |
    AttributeTargets.Field | AttributeTargets.Event,
    AllowMultiple = false, Inherited = true)]
public sealed class TagsAttribute : Attribute
{
    public string[] Values { get; }

    public TagsAttribute(params string[] values) => Values = values;
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field |
    AttributeTargets.Method | AttributeTargets.Event,
    AllowMultiple = false, Inherited = true)]
public sealed class UnitAttribute : Attribute
{
    public string Value { get; }

    public UnitAttribute(string value) => Value = value;
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field |
    AttributeTargets.Parameter,
    AllowMultiple = false, Inherited = true)]
public sealed class MinimumAttribute : Attribute
{
    public object Value { get; }

    public MinimumAttribute(object value) => Value = value;
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field |
    AttributeTargets.Parameter,
    AllowMultiple = false, Inherited = true)]
public sealed class MaximumAttribute : Attribute
{
    public object Value { get; }

    public MaximumAttribute(object value) => Value = value;
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field |
    AttributeTargets.Parameter,
    AllowMultiple = true, Inherited = true)]
public sealed class AllowedValueAttribute : Attribute
{
    public object Value { get; }

    public AllowedValueAttribute(object value) => Value = value;
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field |
    AttributeTargets.Parameter,
    AllowMultiple = false, Inherited = true)]
public sealed class PatternAttribute : Attribute
{
    public string Value { get; }

    public PatternAttribute(string value) => Value = value;
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field |
    AttributeTargets.Method | AttributeTargets.Event | AttributeTargets.Parameter,
    AllowMultiple = false, Inherited = true)]
public sealed class FormatAttribute : Attribute
{
    public string Value { get; }

    public FormatAttribute(string value) => Value = value;
}

[AttributeUsage(AttributeTargets.Method,
    AllowMultiple = true, Inherited = true)]
public sealed class PreconditionAttribute : Attribute
{
    public string Value { get; }

    public PreconditionAttribute(string value) => Value = value;
}

[AttributeUsage(AttributeTargets.Method,
    AllowMultiple = true, Inherited = true)]
public sealed class PostconditionAttribute : Attribute
{
    public string Value { get; }

    public PostconditionAttribute(string value) => Value = value;
}

[AttributeUsage(AttributeTargets.Method,
    AllowMultiple = false, Inherited = true)]
public sealed class EffectsAttribute : Attribute
{
    public OperationEffects Value { get; }

    public EffectsAttribute(OperationEffects value) => Value = value;
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property |
    AttributeTargets.Field | AttributeTargets.Event,
    AllowMultiple = true, Inherited = true)]
public sealed class WarningAttribute : Attribute
{
    public string Value { get; }

    public WarningAttribute(string value) => Value = value;
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property |
    AttributeTargets.Field | AttributeTargets.Event,
    AllowMultiple = false, Inherited = true)]
public sealed class RelatedMembersAttribute : Attribute
{
    public byte[] Indexes { get; }

    public RelatedMembersAttribute(params byte[] indexes) => Indexes = indexes;
}
