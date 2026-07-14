using Esiur.Security.Management;
using System;

namespace Esiur.Resource;

/// <summary>
/// Associates a registered auditing-manager implementation with a resource type.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class AuditingManagerAttribute<T> : ResourceManagerAttribute
    where T : IAuditingManager
{
    public AuditingManagerAttribute() : base(typeof(T))
    {
    }
}
