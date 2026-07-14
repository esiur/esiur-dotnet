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
/// Immutable metadata describing a resource operation being evaluated by managers.
/// <see cref="Delay"/> is the sole mutable value and allows rate-control managers to
/// request deferred execution.
/// </summary>
public sealed class ResourceManagerContext
{
    readonly IReadOnlyList<Attribute> _memberPolicyAttributes;

    public Warehouse Warehouse { get; }
    public EpConnection? Connection { get; }
    public Session? Session { get; }
    public IResource? Resource { get; }
    public MemberDef? Member { get; }
    public ActionType Action { get; }
    public object? Inquirer { get; }

    /// <summary>
    /// Gets the local attributes that configure manager policy for the target member.
    /// The collection is a defensive, read-only snapshot.
    /// </summary>
    public IReadOnlyList<Attribute> MemberPolicyAttributes => _memberPolicyAttributes;

    /// <summary>
    /// Gets or sets an optional delay requested by a rate-control manager.
    /// </summary>
    public TimeSpan Delay { get; set; }

    public ResourceManagerContext(
        Warehouse warehouse,
        EpConnection? connection,
        Session? session,
        IResource? resource,
        MemberDef? member,
        ActionType action,
        object? inquirer = null,
        IEnumerable<Attribute>? memberPolicyAttributes = null)
    {
        Warehouse = warehouse ?? throw new ArgumentNullException(nameof(warehouse));
        Connection = connection;
        Session = session;
        Resource = resource;
        Member = member;
        Action = action;
        Inquirer = inquirer;

        var policies = memberPolicyAttributes?.ToArray() ?? Array.Empty<Attribute>();
        if (policies.Any(attribute => attribute == null))
            throw new ArgumentException(
                "Member policy attributes cannot contain null values.",
                nameof(memberPolicyAttributes));

        _memberPolicyAttributes = Array.AsReadOnly(policies);
    }
}
