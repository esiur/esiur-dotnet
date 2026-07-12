using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Data.Types
{
    public enum MemberDefField : byte
    {
        // Core member fields
        Index = 0x00, // byte
        Name = 0x01, // string
        Flags = 0x02, // byte: member-specific flags enum

        // Human-readable metadata
        Description = 0x20, // string
        Usage = 0x21, // string
        Examples = 0x22, // List<object>
        Tags = 0x23, // List<string>

        // Value constraints and representation
        Unit = 0x24, // string
        Minimum = 0x25, // object: same type as the value
        Maximum = 0x26, // object: same type as the value
        AllowedValues = 0x27, // List<object>
        Pattern = 0x28, // string
        Format = 0x29, // string

        // Operational semantics
        Preconditions = 0x2A, // List<string>
        Postconditions = 0x2B, // List<string>
        Effects = 0x2C, // OperationEffects
        Warnings = 0x2D, // List<string>
        RelatedMembers = 0x2E, // List<byte>: member indexes

        // Compatibility guidance
        DeprecationMessage = 0x2F, // string
    }
}
