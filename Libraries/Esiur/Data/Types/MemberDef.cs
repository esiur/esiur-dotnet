using Esiur.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Data.Types;
public class MemberDef
{
    IReadOnlyList<Attribute> memberPolicyAttributes = Array.Empty<Attribute>();

    // Core fields
    public byte Index { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool Inherited { get; set; }

    public bool Deprecated { get; set; }

    /// <summary>
    /// Name of the server-side Warehouse rate policy applied to this member.
    /// This is local execution metadata and is not sent to remote clients.
    /// </summary>
    public string? RatePolicyName { get; set; }

    /// <summary>
    /// Local reflection attributes supplied to resource managers while this member
    /// is evaluated. This execution metadata is not serialized to remote peers.
    /// </summary>
    public IReadOnlyList<Attribute> MemberPolicyAttributes
    {
        get => memberPolicyAttributes;
        internal set => memberPolicyAttributes = value is null
            ? Array.Empty<Attribute>()
            : Array.AsReadOnly(value.ToArray());
    }

    public TypeDef Definition { get; set; } = null!;

    // Human-readable metadata
    public string? Description { get; set; }

    public string? Usage { get; set; }

    public List<object>? Examples { get; set; }

    public List<string>? Tags { get; set; }

    // Value constraints and representation
    public string? Unit { get; set; }

    public object? Minimum { get; set; }

    public object? Maximum { get; set; }

    public List<object>? AllowedValues { get; set; }

    public string? Pattern { get; set; }

    public string? Format { get; set; }

    // Operational semantics
    public List<string>? Preconditions { get; set; }

    public List<string>? Postconditions { get; set; }

    public OperationEffects Effects { get; set; }

    public List<string>? Warnings { get; set; }

    public List<byte>? RelatedMembers { get; set; }

    // Compatibility guidance
    public string? DeprecationMessage { get; set; }

    public Map<string, string>? Annotations { get; set; }

    public string Fullname =>
        Definition is null || string.IsNullOrEmpty(Definition.Name)
            ? Name
            : $"{Definition.Name}.{Name}";

}

