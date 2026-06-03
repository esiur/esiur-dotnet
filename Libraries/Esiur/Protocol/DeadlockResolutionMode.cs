namespace Esiur.Protocol;

/// <summary>
/// Strategy used by <c>EpConnection.FetchResource</c> when it is asked for a resource whose
/// attachment is already in flight. Selectable mainly for experimental A/B/C evaluation of the
/// deadlock-prevention algorithm.
/// </summary>
public enum DeadlockResolutionMode : byte
{
    /// <summary>
    /// Default. Wait for the in-flight attachment to complete, except when a genuine wait-for cycle
    /// is detected (same dependency chain, or a cross-chain cycle in the wait-for graph), in which
    /// case a placeholder is returned to break it. Never deadlocks and never returns an unnecessary
    /// placeholder.
    /// </summary>
    WaitWithCycleDetection = 0,

    /// <summary>
    /// Legacy behaviour: return the not-yet-attached placeholder to any cross-chain requester of an
    /// in-flight resource. Never deadlocks, but delivers partially-attached resources for non-cyclic
    /// contention (the bug under study).
    /// </summary>
    LegacyCrossChainPlaceholder = 1,

    /// <summary>
    /// No cycle handling at all: always wait for the in-flight attachment, even within the same
    /// dependency chain. Genuinely deadlocks whenever the request graph contains a cycle. Used only
    /// to demonstrate that cycle handling is necessary and that the deadlock detector works.
    /// </summary>
    NaiveWait = 2,
}
