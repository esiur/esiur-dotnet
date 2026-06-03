namespace Esiur.Resource;

/// <summary>
/// Lifecycle state of a distributed (remote) resource on the consuming side. Replaces the former
/// separate attached/suspended/destroyed booleans with a single explicit state machine.
/// </summary>
public enum ResourceStatus : byte
{
    /// <summary>Created as a placeholder; its properties have not been received yet.</summary>
    Pending = 0,

    /// <summary>
    /// Its own properties have been received and merged, but its dependency graph may still be
    /// incomplete (e.g. it was used to break a reference cycle). Not yet safe to hand to the
    /// application as fully ready.
    /// </summary>
    Attached = 1,

    /// <summary>
    /// Attached and delivered to the application as part of a fully-attached object graph. This is
    /// the only state in which a resource — including every resource it depends on — is guaranteed
    /// ready for application use.
    /// </summary>
    Published = 2,

    /// <summary>The connection was lost; the resource is awaiting reattachment.</summary>
    Suspended = 3,

    /// <summary>The resource has been detached/destroyed and must not be accessed.</summary>
    Destroyed = 4,
}
