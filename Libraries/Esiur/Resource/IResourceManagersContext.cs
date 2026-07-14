using Esiur.Security.Management;
using System.Collections.Generic;

namespace Esiur.Resource;

/// <summary>
/// Optional trusted creation context that binds registered managers to a resource.
/// Keeping this separate from <see cref="IResourceContext"/> preserves compatibility
/// with existing context implementations.
/// </summary>
public interface IResourceManagersContext
{
    IReadOnlyList<IResourceManager> ResourceManagers { get; }
}
