using Esiur.Security.Permissions;

namespace Esiur.Security.Management;

/// <summary>
/// Audits a resource operation before it is executed. A
/// <see cref="Ruling.Denied"/> result may veto the operation, while
/// <see cref="Ruling.Allowed"/> and <see cref="Ruling.DontCare"/> never grant
/// authorization or override another manager's denial.
/// </summary>
public interface IAuditingManager : IResourceManager
{
    Ruling Applicable(ResourceManagerContext context);
}
