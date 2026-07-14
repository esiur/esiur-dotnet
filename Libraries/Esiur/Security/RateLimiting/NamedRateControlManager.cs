using Esiur.Security.Management;
using Esiur.Security.Permissions;
using System;

namespace Esiur.Security.RateLimiting;

/// <summary>
/// Bridges the existing named RateControl policy registry into the unified
/// resource-manager pipeline.
/// </summary>
internal sealed class NamedRateControlManager : IRateControlManager
{
    public Ruling Applicable(ResourceManagerContext context)
    {
        if (context.Action != ActionType.Execute &&
            context.Action != ActionType.SetProperty)
            return Ruling.DontCare;

        var policyName = context.Member?.RatePolicyName;
        if (string.IsNullOrWhiteSpace(policyName))
            return Ruling.DontCare;

        if (context.Connection is null || context.Session is null || context.Member is null)
        {
            context.DenialReason = $"Rate policy `{policyName}` requires an authenticated connection.";
            return Ruling.Denied;
        }

        var policy = context.Warehouse.TryGetRatePolicy(policyName);
        if (policy is null)
        {
            context.DenialReason = $"Rate policy `{policyName}` is not registered.";
            return Ruling.Denied;
        }

        var rateContext = new RateControlContext(
            context.Warehouse,
            context.Connection,
            context.Session,
            context.Resource,
            context.Member,
            context.Action);

        var ruling = policy.Applicable(rateContext);
        if (rateContext.Delay > context.Delay)
            context.Delay = rateContext.Delay;

        if (ruling == Ruling.Denied && string.IsNullOrWhiteSpace(context.DenialReason))
            context.DenialReason = $"Rate policy `{policyName}` denied `{context.Member.Fullname}`.";

        return ruling;
    }
}
