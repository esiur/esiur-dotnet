using System;

namespace Esiur.Resource;

/// <summary>
/// Applies a named Warehouse rate policy to an exported function or property setter.
/// </summary>
[AttributeUsage(
    AttributeTargets.Method | AttributeTargets.Property,
    AllowMultiple = false,
    Inherited = true)]
public sealed class RateControlAttribute : Attribute
{
    /// <summary>
    /// Gets the name used to resolve the policy from the owning Warehouse.
    /// </summary>
    public string PolicyName { get; }

    public RateControlAttribute(string policyName)
    {
        if (string.IsNullOrWhiteSpace(policyName))
            throw new ArgumentException("A rate policy name is required.", nameof(policyName));

        PolicyName = policyName;
    }
}
