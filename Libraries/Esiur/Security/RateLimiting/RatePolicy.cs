using Esiur.Data.Types;
using Esiur.Protocol;
using Esiur.Resource;
using Esiur.Security.Authority;
using Esiur.Security.Permissions;
using System;

namespace Esiur.Security.RateLimiting;

/// <summary>
/// Describes the request currently being evaluated by a rate policy.
/// </summary>
public sealed class RateControlContext
{
    public Warehouse Warehouse { get; }
    public EpConnection Connection { get; }
    public Session Session { get; }
    public IResource? Resource { get; }
    public MemberDef Member { get; }
    public ActionType Action { get; }

    /// <summary>
    /// Optional delay assigned by a policy to an allowed queued request.
    /// </summary>
    public TimeSpan Delay { get; set; }

    public RateControlContext(
        Warehouse warehouse,
        EpConnection connection,
        Session session,
        IResource? resource,
        MemberDef member,
        ActionType action)
    {
        Warehouse = warehouse ?? throw new ArgumentNullException(nameof(warehouse));
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        Session = session ?? throw new ArgumentNullException(nameof(session));
        Resource = resource;
        Member = member ?? throw new ArgumentNullException(nameof(member));
        Action = action;
    }
}

/// <summary>
/// Base class for named Warehouse rate-control policies.
/// </summary>
public abstract class RatePolicy
{
    /// <summary>
    /// Name referenced by <see cref="RateControlAttribute"/>.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    protected RatePolicy()
    {
    }

    protected RatePolicy(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Evaluates a context-free policy. Override this for simple policies.
    /// </summary>
    public virtual Ruling Applicable() => Ruling.DontCare;

    /// <summary>
    /// Evaluates a request. Context-aware policies should override this overload.
    /// </summary>
    public virtual Ruling Applicable(RateControlContext context) => Applicable();
}
