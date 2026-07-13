using Esiur.Resource;
using System;
using System.Linq;
using System.Reflection;

namespace Esiur.Data.Types;

internal static class DefinitionAttributeReader
{
    internal static void Apply(Type source, TypeDef target)
    {
        target.Usage = source.GetCustomAttribute<UsageAttribute>(true)?.Value;
        target.Description = source.GetCustomAttribute<DescriptionAttribute>(true)?.Value;
        target.Example = source.GetCustomAttributes<ExampleAttribute>(true)
            .Select(x => x.Value)
            .FirstOrDefault();
        target.Category = source.GetCustomAttribute<CategoryAttribute>(true)?.Value;
        target.Since = source.GetCustomAttribute<SinceAttribute>(true)?.Value;
    }

    internal static T Apply<T>(MemberInfo source, T target) where T : MemberDef
    {
        target.Description = source.GetCustomAttribute<DescriptionAttribute>(true)?.Value;
        target.Usage = source.GetCustomAttribute<UsageAttribute>(true)?.Value;

        var examples = source.GetCustomAttributes<ExampleAttribute>(true)
            .Select(x => x.Value)
            .ToList();
        target.Examples = examples.Count == 0 ? null : examples;

        var tags = source.GetCustomAttribute<TagsAttribute>(true)?.Values;
        target.Tags = tags == null || tags.Length == 0 ? null : tags.ToList();
        target.Unit = source.GetCustomAttribute<UnitAttribute>(true)?.Value;
        target.Minimum = source.GetCustomAttribute<MinimumAttribute>(true)?.Value;
        target.Maximum = source.GetCustomAttribute<MaximumAttribute>(true)?.Value;

        var allowedValues = source.GetCustomAttributes<AllowedValueAttribute>(true)
            .Select(x => x.Value)
            .ToList();
        target.AllowedValues = allowedValues.Count == 0 ? null : allowedValues;

        target.Pattern = source.GetCustomAttribute<PatternAttribute>(true)?.Value;
        target.Format = source.GetCustomAttribute<FormatAttribute>(true)?.Value;

        var preconditions = source.GetCustomAttributes<PreconditionAttribute>(true)
            .Select(x => x.Value)
            .ToList();
        target.Preconditions = preconditions.Count == 0 ? null : preconditions;

        var postconditions = source.GetCustomAttributes<PostconditionAttribute>(true)
            .Select(x => x.Value)
            .ToList();
        target.Postconditions = postconditions.Count == 0 ? null : postconditions;

        target.Effects = source.GetCustomAttribute<EffectsAttribute>(true)?.Value
            ?? OperationEffects.None;

        var warnings = source.GetCustomAttributes<WarningAttribute>(true)
            .Select(x => x.Value)
            .ToList();
        target.Warnings = warnings.Count == 0 ? null : warnings;

        var relatedMembers = source.GetCustomAttribute<RelatedMembersAttribute>(true)?.Indexes;
        target.RelatedMembers = relatedMembers == null || relatedMembers.Length == 0
            ? null
            : relatedMembers.ToList();

        var obsolete = source.GetCustomAttribute<ObsoleteAttribute>(true);
        target.Deprecated = obsolete != null;
        target.DeprecationMessage = obsolete?.Message;

        return target;
    }
}
