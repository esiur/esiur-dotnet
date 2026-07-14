using Esiur.Security.Management;
using System;

namespace Esiur.Resource;

/// <summary>
/// Associates a registered rate-control-manager implementation with a resource type.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class RateControlManagerAttribute<T> : ResourceManagerAttribute
    where T : IRateControlManager
{
    public RateControlManagerAttribute() : base(typeof(T))
    {
    }
}
