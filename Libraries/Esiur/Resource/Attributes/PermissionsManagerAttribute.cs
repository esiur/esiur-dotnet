using Esiur.Security.Permissions;
using System;

namespace Esiur.Resource;

/// <summary>
/// Associates a registered permissions-manager implementation with a resource type.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class PermissionsManagerAttribute<T> : Attribute
    where T : IPermissionsManager
{
}
