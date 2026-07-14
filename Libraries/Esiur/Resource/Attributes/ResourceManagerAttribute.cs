using Esiur.Security.Management;
using System;

namespace Esiur.Resource;

/// <summary>
/// Base metadata used to resolve a resource manager from its owning Warehouse.
/// Manager attributes never create instances directly; the declared type must be
/// registered with the Warehouse before the resource is created.
/// </summary>
public abstract class ResourceManagerAttribute : Attribute
{
    public Type ManagerType { get; }

    protected ResourceManagerAttribute(Type managerType)
    {
        ManagerType = managerType ?? throw new ArgumentNullException(nameof(managerType));

        if (!typeof(IResourceManager).IsAssignableFrom(managerType))
            throw new ArgumentException(
                $"Manager type `{managerType}` must implement {nameof(IResourceManager)}.",
                nameof(managerType));
    }
}
