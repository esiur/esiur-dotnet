using Esiur.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Data.Types;
public class MemberDef
{

    // Core fields
    public byte Index { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool Inherited { get; set; }

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

    public string Fullname =>
        Definition is null || string.IsNullOrEmpty(Definition.Name)
            ? Name
            : $"{Definition.Name}.{Name}";

}

