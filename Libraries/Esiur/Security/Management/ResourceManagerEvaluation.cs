using Esiur.Security.Permissions;
using System;

namespace Esiur.Security.Management;

/// <summary>
/// Aggregated decisions from each independent manager category.
/// An allow in one category never grants admission in another category.
/// </summary>
public sealed class ResourceManagerEvaluation
{
    public Ruling Permissions { get; }
    public Ruling RateControl { get; }
    public Ruling Auditing { get; }
    public TimeSpan Delay { get; }

    public string? PermissionsDenialReason { get; }
    public string? RateControlDenialReason { get; }
    public string? AuditingDenialReason { get; }

    public bool IsAllowed =>
        Permissions == Ruling.Allowed &&
        RateControl != Ruling.Denied &&
        Auditing != Ruling.Denied;

    internal ResourceManagerEvaluation(
        Ruling permissions,
        Ruling rateControl,
        Ruling auditing,
        TimeSpan delay,
        string? permissionsDenialReason,
        string? rateControlDenialReason,
        string? auditingDenialReason)
    {
        Permissions = permissions;
        RateControl = rateControl;
        Auditing = auditing;
        Delay = delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
        PermissionsDenialReason = permissionsDenialReason;
        RateControlDenialReason = rateControlDenialReason;
        AuditingDenialReason = auditingDenialReason;
    }
}
