using Esiur.Security.Permissions;

namespace Esiur.Security.Management;

/// <summary>
/// Evaluates whether a resource operation is admitted by rate-control policy.
/// A manager may assign <see cref="ResourceManagerContext.Delay"/> when allowing
/// an operation that should be queued.
/// </summary>
public interface IRateControlManager : IResourceManager
{
    Ruling Applicable(ResourceManagerContext context);
}
