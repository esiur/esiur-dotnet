using System.Collections.Generic;
using Esiur.Protocol;

namespace Esiur.Tests.Unit;

/// <summary>
/// Unit tests for EpConnection.HasWaitForCycle — the pure decision function that decides whether
/// waiting for an in-flight resource fetch would deadlock (and a placeholder must break the cycle)
/// versus being safe to wait for full attachment. This is the heart of the cross-chain fix.
/// </summary>
public class FetchCycleDetectionTests
{
    static Dictionary<uint, HashSet<uint>> Graph(params (uint parent, uint[] children)[] edges)
    {
        var g = new Dictionary<uint, HashSet<uint>>();
        foreach (var (parent, children) in edges)
            g[parent] = new HashSet<uint>(children);
        return g;
    }

    [Fact]
    public void AppFacingFetch_NoChain_NeverCycles()
    {
        // requestSequence == null marks an application-facing fetch: it must always wait, never
        // receive a placeholder, regardless of the wait-for graph.
        var g = Graph((1u, new uint[] { 2 }), (2u, new uint[] { 1 }));
        Assert.False(EpConnection.HasWaitForCycle(2, null, g));
        Assert.False(EpConnection.HasWaitForCycle(2, new uint[0], g));
    }

    [Fact]
    public void NoBlocking_IsNotCyclic()
    {
        var g = Graph();
        Assert.False(EpConnection.HasWaitForCycle(2, new uint[] { 1 }, g));
    }

    [Fact]
    public void IndependentInFlight_IsNotCyclic()
    {
        // Chain [1] fetching 2; 2 is blocked on 3 (an unrelated resource). No path back to chain.
        var g = Graph((2u, new uint[] { 3 }));
        Assert.False(EpConnection.HasWaitForCycle(2, new uint[] { 1 }, g));
    }

    [Fact]
    public void MutualCrossChain_IsCyclic()
    {
        // Two concurrent fetches: 1 is blocked on 2, and 2 is blocked on 1. Chain [1] now wants 2.
        // Waiting would deadlock, so this must be reported as a cycle.
        var g = Graph((1u, new uint[] { 2 }), (2u, new uint[] { 1 }));
        Assert.True(EpConnection.HasWaitForCycle(2, new uint[] { 1 }, g));
    }

    [Fact]
    public void TransitiveCycle_IsDetected()
    {
        // Chain [1] wants 2; 2 -> 3 -> 1 leads back into the chain.
        var g = Graph((2u, new uint[] { 3 }), (3u, new uint[] { 1 }));
        Assert.True(EpConnection.HasWaitForCycle(2, new uint[] { 1 }, g));
    }

    [Fact]
    public void ParallelChildren_OnlyOneClosesCycle()
    {
        // 2 is blocked on several children; only one (5) leads back to the chain root.
        var g = Graph((2u, new uint[] { 3, 4, 5 }), (5u, new uint[] { 1 }));
        Assert.True(EpConnection.HasWaitForCycle(2, new uint[] { 1, 9 }, g));
    }

    [Fact]
    public void DeeperChain_BackEdgeToAncestor_IsCyclic()
    {
        // Current chain is [1,2,3]; fetching 4 which is blocked on 2 (an ancestor) -> cycle.
        var g = Graph((4u, new uint[] { 2 }));
        Assert.True(EpConnection.HasWaitForCycle(4, new uint[] { 1, 2, 3 }, g));
    }

    [Fact]
    public void SelfReferentialGraph_DoesNotInfiniteLoop()
    {
        // Defensive: a self-loop / disjoint cycle that never reaches the chain must terminate.
        var g = Graph((2u, new uint[] { 3 }), (3u, new uint[] { 2 }));
        Assert.False(EpConnection.HasWaitForCycle(2, new uint[] { 1 }, g));
    }
}
