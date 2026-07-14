using Esiur.Data.Types;
using Esiur.Protocol;
using Esiur.Resource;
using Esiur.Security.Authority;
using Esiur.Security.Permissions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Esiur.Security.Management;

/// <summary>
/// Metadata describing a resource operation being evaluated by managers. Operation
/// identity is immutable; <see cref="Delay"/> and <see cref="DenialReason"/> let a
/// manager return admission details.
/// </summary>
public sealed class ResourceManagerContext
{
    readonly IReadOnlyList<Attribute> _memberPolicyAttributes;

    public Warehouse Warehouse { get; }
    public EpConnection? Connection { get; }
    public Session? Session { get; }
    public IResource? Resource { get; }
    public MemberDef? Member { get; }
    public TypeDef? TypeDefinition { get; }
    public ActionType Action { get; }
    public object? Inquirer { get; }

    /// <summary>
    /// Indicates whether the protocol operation can honor an allowed delayed ruling.
    /// Rate managers should inspect this before reserving queued capacity.
    /// </summary>
    public bool SupportsDelay { get; }

    /// <summary>
    /// Gets the local attributes that configure manager policy for the target member.
    /// The collection is a defensive, read-only snapshot.
    /// </summary>
    public IReadOnlyList<Attribute> MemberPolicyAttributes => _memberPolicyAttributes;

    /// <summary>
    /// Gets or sets an optional delay requested by a rate-control manager. Execute,
    /// property-set, and pull-stream operations honor delayed rulings; operations
    /// that cannot be delayed fail closed.
    /// </summary>
    public TimeSpan Delay { get; set; }

    /// <summary>
    /// Optional public-safe reason supplied by a manager when it denies an operation.
    /// </summary>
    public string? DenialReason { get; set; }

    public ResourceManagerContext(
        Warehouse warehouse,
        EpConnection? connection,
        Session? session,
        IResource? resource,
        MemberDef? member,
        ActionType action,
        object? inquirer = null,
        IEnumerable<Attribute>? memberPolicyAttributes = null,
        TypeDef? typeDefinition = null,
        bool supportsDelay = false)
    {
        Warehouse = warehouse ?? throw new ArgumentNullException(nameof(warehouse));
        Connection = connection;
        Session = session;
        Resource = resource;
        Member = member;
        TypeDefinition = typeDefinition ?? resource?.Instance?.Definition ?? member?.Definition;
        Action = action;
        Inquirer = inquirer;
        SupportsDelay = supportsDelay;

        var policies = memberPolicyAttributes?.ToArray() ?? Array.Empty<Attribute>();
        if (policies.Any(attribute => attribute == null))
            throw new ArgumentException(
                "Member policy attributes cannot contain null values.",
                nameof(memberPolicyAttributes));

        _memberPolicyAttributes = Array.AsReadOnly(policies);
    }
}
