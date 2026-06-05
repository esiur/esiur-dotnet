using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Esiur.Core;
using Esiur.Misc;
using Esiur.Protocol;
using Esiur.Resource;
using Xunit.Abstractions;

namespace Esiur.Tests.Unit.Integration;

/// <summary>
/// End-to-end deadlock tests for EpConnection.FetchResource over a real loopback connection.
/// Builds a range of reference topologies (self-loop, cycles of increasing length, concurrent
/// cross-chain cycles, diamonds, dense graphs) and asserts, for every one, that the fetch
/// completes without deadlock (a timeout would indicate one) and that every resource delivered to
/// the application is fully attached (the cross-chain bug delivered partially-attached resources).
/// Per-topology statistics are collected from the protocol counters and written to a report.
/// </summary>
[Collection("Integration")]
public class DeadlockIntegrationTests
{
    readonly ITestOutputHelper _out;
    public DeadlockIntegrationTests(ITestOutputHelper output) => _out = output;

    const int Timeout = 15000;

    // ---- async + counter helpers -----------------------------------------------------------

    static Task<T> ToTask<T>(AsyncReply<T> reply)
    {
        var tcs = new TaskCompletionSource<T>();
        reply.Then(v => tcs.TrySetResult(v)).Error(ex => tcs.TrySetException((Exception)ex));
        return tcs.Task;
    }

    static async Task<T> WithTimeout<T>(Task<T> task, int ms = Timeout)
    {
        if (await Task.WhenAny(task, Task.Delay(ms)) != task)
            throw new TimeoutException("Operation timed out — possible deadlock.");
        return await task;
    }

    static long Counter(string name)
        => Global.Counters.Contains(name) ? Global.Counters[name] : 0;

    // ---- topology model --------------------------------------------------------------------

    record Topology(string Name, int Nodes, (int From, int To)[] Edges, int[] FetchRoots, bool Concurrent);

    static IEnumerable<Topology> Topologies() => new[]
    {
        new Topology("self-loop",        1, new[]{ (0,0) }, new[]{0}, false),
        new Topology("2-cycle",          2, new[]{ (0,1),(1,0) }, new[]{0}, false),
        new Topology("3-cycle",          3, new[]{ (0,1),(1,2),(2,0) }, new[]{0}, false),
        new Topology("4-cycle",          4, new[]{ (0,1),(1,2),(2,3),(3,0) }, new[]{0}, false),
        new Topology("cross-chain x2",   2, new[]{ (0,1),(1,0) }, new[]{0,1}, true),
        new Topology("cross-chain x3",   3, new[]{ (0,1),(1,2),(2,0) }, new[]{0,1,2}, true),
        new Topology("diamond",          4, new[]{ (0,1),(0,2),(1,3),(2,3) }, new[]{0}, false),
        new Topology("figure-8",         4, new[]{ (0,1),(1,0),(1,2),(2,3),(3,1) }, new[]{0}, false),
        new Topology("complete-4",       4, AllPairs(4), new[]{0}, false),
        new Topology("complete-4 concur",4, AllPairs(4), new[]{0,1,2,3}, true),
    };

    // Topologies for the legacy-vs-new comparison. The fan-in cases have many roots referencing a
    // single shared resource whose own dependency chain is deep: while that shared resource is
    // attaching its chain, the other concurrent fetchers reach it, and the legacy resolver hands
    // each of them the not-yet-attached placeholder (the bug), whereas the new resolver waits.
    static IEnumerable<Topology> ComparisonTopologies() => new[]
    {
        new Topology("single-root 4-cycle (control)", 4, new[]{ (0,1),(1,2),(2,3),(3,0) }, new[]{0}, false),
        Cycle("cross-chain ring x3", 3),
        // Staggered shared dependency (no cycle): X reaches the shared node S immediately while Y
        // reaches it through a chain, arriving during S's own deep-chain attach window. The legacy
        // resolver hands Y the not-yet-attached placeholder S (unnecessary — there is no cycle); the
        // new resolver waits for S to finish attaching.
        Staggered("staggered shared-dep", leadDepth: 0, lagDepth: 3, sharedDepth: 3),
        Staggered("staggered shared-dep (deep)", leadDepth: 0, lagDepth: 4, sharedDepth: 4),
    };

    // An N-node ring (i -> i+1, last -> 0), every node fetched concurrently.
    static Topology Cycle(string name, int n)
    {
        var edges = new (int, int)[n];
        for (var i = 0; i < n; i++) edges[i] = (i, (i + 1) % n);
        return new Topology(name, n, edges, Enumerable.Range(0, n).ToArray(), true);
    }

    // X (root 0) and Y (root 1) both depend on a shared node S. X reaches S through a chain of
    // length `leadDepth`, Y through a chain of length `lagDepth` (make lag > lead so Y arrives at S
    // later). S itself starts a chain of length `sharedDepth`, widening the window during which S is
    // attaching and another fetcher can be handed a placeholder. No cycle exists.
    static Topology Staggered(string name, int leadDepth, int lagDepth, int sharedDepth)
    {
        var edges = new List<(int, int)>();
        var next = 2;
        int Chain(int from, int depth)
        {
            for (var d = 0; d < depth; d++) { edges.Add((from, next)); from = next; next++; }
            return from; // tail
        }

        var xTail = Chain(0, leadDepth);    // X = 0
        var yTail = Chain(1, lagDepth);     // Y = 1
        var shared = next++;                // S
        edges.Add((xTail, shared));
        edges.Add((yTail, shared));
        Chain(shared, sharedDepth);         // S -> deep chain

        return new Topology(name, next, edges.ToArray(), new[] { 0, 1 }, true);
    }

    static (int, int)[] AllPairs(int n)
    {
        var edges = new List<(int, int)>();
        for (var i = 0; i < n; i++)
            for (var j = 0; j < n; j++)
                if (i != j) edges.Add((i, j));
        return edges.ToArray();
    }

    // ---- graph attach verification ---------------------------------------------------------

    // Walks the client-side object graph reachable from the fetched roots and returns whether
    // every node is fully attached, plus the number of distinct nodes reached.
    static (bool allAttached, int reached) VerifyGraph(IEnumerable<EpResource> roots)
    {
        var seen = new HashSet<uint>();
        var queue = new Queue<EpResource>(roots);
        var allAttached = true;

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            if (node == null || !seen.Add(node.ResourceInstanceId))
                continue;

            if (node.Status != Resource.ResourceStatus.Attached && node.Status != Resource.ResourceStatus.Published)
            {
                allAttached = false;
                continue; // do not traverse into a partially attached node
            }

            // property index 1 == Links (Id is index 0)
            if (node.TryGetPropertyValue((byte)1, out var linksObj) && linksObj is IEnumerable links)
                foreach (var child in links)
                    if (child is EpResource childResource)
                        queue.Enqueue(childResource);
        }

        return (allAttached, seen.Count);
    }

    // ---- per-topology run ------------------------------------------------------------------

    record StatRow(string Topology, int Nodes, int Reached, long SameChain, long CrossChain,
                   long Waits, long CacheHits, double Ms, bool AllAttached, bool Deadlock);

    async Task<StatRow> RunTopology(Topology topo)
    {
        await using var cluster = await IntegrationCluster.StartAsync(async wh =>
        {
            var nodes = new Node[topo.Nodes];
            for (var i = 0; i < topo.Nodes; i++)
            {
                nodes[i] = new Node { Id = i };
                await wh.Put($"sys/n{i}", nodes[i]);
            }

            foreach (var group in topo.Edges.GroupBy(e => e.From))
                nodes[group.Key].Links = group.Select(e => nodes[e.To]).ToArray();
        });

        var c0 = (same: Counter("EpResourceDeadLockSameChain"),
                  cross: Counter("EpResourceDeadLockCrossChain"),
                  wait: Counter("EpResourcePendingCacheHit"),
                  hit: Counter("EpResourceAttachedCacheHit"));

        var sw = Stopwatch.StartNew();
        var deadlock = false;
        var reached = 0;
        var allAttached = false;

        try
        {
            var fetchTasks = topo.FetchRoots
                .Select(r => ToTask(cluster.Connection.Get($"sys/n{r}")))
                .ToArray();

            if (!topo.Concurrent)
            {
                // sequential roots (usually a single root)
                foreach (var t in fetchTasks)
                    await WithTimeout(t);
            }

            var results = await WithTimeout(Task.WhenAll(fetchTasks));
            sw.Stop();

            (allAttached, reached) = VerifyGraph(results.Cast<EpResource>());
        }
        catch (TimeoutException)
        {
            sw.Stop();
            deadlock = true;
        }

        return new StatRow(topo.Name, topo.Nodes, reached,
            Counter("EpResourceDeadLockSameChain") - c0.same,
            Counter("EpResourceDeadLockCrossChain") - c0.cross,
            Counter("EpResourcePendingCacheHit") - c0.wait,
            Counter("EpResourceAttachedCacheHit") - c0.hit,
            sw.Elapsed.TotalMilliseconds, allAttached, deadlock);
    }

    // ---- tests -----------------------------------------------------------------------------

    [Fact]
    public async Task DeadlockMatrix_AllTopologies()
    {
        var rows = new List<StatRow>();

        foreach (var topo in Topologies())
        {
            var row = await RunTopology(topo);
            rows.Add(row);

            Assert.False(row.Deadlock, $"{topo.Name}: fetch deadlocked (timed out)");
            Assert.True(row.AllAttached, $"{topo.Name}: a partially-attached resource reached the application");
            Assert.True(row.Reached >= topo.Nodes, $"{topo.Name}: expected to reach {topo.Nodes} nodes, reached {row.Reached}");
        }

        EmitReport(rows);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    public async Task Concurrency_Sweep_CyclicGraph(int concurrency)
    {
        // A 4-node cycle fetched by N concurrent application requests for all four roots. Stresses
        // the wait-for/cycle-break paths under contention; all requests must complete and attach.
        await using var cluster = await IntegrationCluster.StartAsync(async wh =>
        {
            var nodes = new Node[4];
            for (var i = 0; i < 4; i++)
            {
                nodes[i] = new Node { Id = i };
                await wh.Put($"sys/n{i}", nodes[i]);
            }
            for (var i = 0; i < 4; i++)
                nodes[i].Links = new[] { nodes[(i + 1) % 4] };
        });

        var sw = Stopwatch.StartNew();
        var tasks = Enumerable.Range(0, concurrency)
            .SelectMany(_ => Enumerable.Range(0, 4).Select(r => ToTask(cluster.Connection.Get($"sys/n{r}"))))
            .ToArray();

        var results = await WithTimeout(Task.WhenAll(tasks), 30000);
        sw.Stop();

        var (allAttached, _) = VerifyGraph(results.Cast<EpResource>());
        Assert.True(allAttached, $"concurrency {concurrency}: a partially-attached resource was delivered");

        _out.WriteLine($"concurrency={concurrency,2}  requests={tasks.Length,3}  time={sw.Elapsed.TotalMilliseconds,8:F1} ms  " +
                       $"throughput={tasks.Length / sw.Elapsed.TotalSeconds,7:F0} req/s");
    }

    // ---- legacy vs new comparison ----------------------------------------------------------

    // Counts resources reachable from the delivered roots that are NOT published — i.e. handed to
    // the application while their own dependency graph is not fully attached.
    static int CountUnpublished(IEnumerable<EpResource> roots)
    {
        var seen = new HashSet<uint>();
        var queue = new Queue<EpResource>(roots);
        var unpublished = 0;

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            if (node == null || !seen.Add(node.ResourceInstanceId))
                continue;

            if (node.Status != ResourceStatus.Published)
                unpublished++;

            if ((node.Status == ResourceStatus.Attached || node.Status == ResourceStatus.Published)
                && node.TryGetPropertyValue((byte)1, out var linksObj) && linksObj is IEnumerable links)
                foreach (var child in links)
                    if (child is EpResource childResource)
                        queue.Enqueue(childResource);
        }

        return unpublished;
    }

    async Task<(bool deadlock, int unnecessaryPlaceholders)> RunForCompare(Topology topo, bool legacy)
    {
        await using var cluster = await IntegrationCluster.StartAsync(async wh =>
        {
            var nodes = new Node[topo.Nodes];
            for (var i = 0; i < topo.Nodes; i++)
            {
                nodes[i] = new Node { Id = i };
                await wh.Put($"sys/n{i}", nodes[i]);
            }
            foreach (var group in topo.Edges.GroupBy(e => e.From))
                nodes[group.Key].Links = group.Select(e => nodes[e.To]).ToArray();
        });

        cluster.Connection.DeadlockResolution = legacy
            ? DeadlockResolutionMode.LegacyCrossChainPlaceholder
            : DeadlockResolutionMode.WaitWithCycleDetection;

        var completions = new List<Task<bool>>();

        try
        {
            foreach (var r in topo.FetchRoots)
            {
                var tcs = new TaskCompletionSource<bool>();
                cluster.Connection.Get($"sys/n{r}")
                    .Then(_ => tcs.TrySetResult(true))
                    .Error(ex => tcs.TrySetException((Exception)ex));
                completions.Add(tcs.Task);
            }

            await WithTimeout(Task.WhenAll(completions));
            // Per-connection counter (fresh connection starts at 0), free of cross-connection noise.
            return (false, (int)cluster.Connection.UnnecessaryPlaceholderCount);
        }
        catch (TimeoutException)
        {
            return (true, -1);
        }
    }

    record CompareRow(string Topology, int Iterations,
                      int LegacyDeadlocks, int LegacyBugRuns, double LegacyAvgUnnecessary,
                      int NewDeadlocks, int NewBugRuns, double NewAvgUnnecessary);

    [Fact]
    public async Task LegacyVsNew_UnnecessaryPlaceholderComparison()
    {
        const int iterations = 20;
        var rows = new List<CompareRow>();

        foreach (var topo in ComparisonTopologies())
        {
            int legDead = 0, legBug = 0, legUnnec = 0;
            int newDead = 0, newBug = 0, newUnnec = 0;

            for (var i = 0; i < iterations; i++)
            {
                var (ld, lu) = await RunForCompare(topo, legacy: true);
                if (ld) legDead++; else { if (lu > 0) legBug++; legUnnec += Math.Max(0, lu); }

                var (nd, nu) = await RunForCompare(topo, legacy: false);
                if (nd) newDead++; else { if (nu > 0) newBug++; newUnnec += Math.Max(0, nu); }
            }

            rows.Add(new CompareRow(topo.Name, iterations,
                legDead, legBug, (double)legUnnec / iterations,
                newDead, newBug, (double)newUnnec / iterations));
        }

        EmitComparison(rows, iterations);

        // The new resolver must never deadlock and must never hand out an unnecessary placeholder
        // (it only breaks genuine wait-for cycles) — both deterministic invariants.
        Assert.All(rows, r => Assert.Equal(0, r.NewDeadlocks));
        Assert.All(rows, r => Assert.Equal(0, r.NewBugRuns));
    }

    void EmitComparison(List<CompareRow> rows, int iterations)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Esiur FetchResource — legacy vs new cross-chain resolution");
        sb.AppendLine();
        sb.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC   |   iterations per cell: {iterations}");
        sb.AppendLine();
        sb.AppendLine("Metric: 'unnecessary placeholder' = a not-yet-attached resource handed to a requester");
        sb.AppendLine("where NO genuine wait-for cycle exists — a partial delivery that the new resolver avoids");
        sb.AppendLine("by waiting for full attachment. Genuine cycles are excluded (both resolvers must break those).");
        sb.AppendLine();
        sb.AppendLine("| Topology | Legacy deadlocks | Legacy buggy runs | Legacy avg unnecessary | New deadlocks | New buggy runs | New avg unnecessary |");
        sb.AppendLine("|----------|-----------------:|------------------:|-----------------------:|--------------:|---------------:|--------------------:|");

        foreach (var r in rows)
            sb.AppendLine($"| {r.Topology} | {r.LegacyDeadlocks} | {r.LegacyBugRuns}/{r.Iterations} | {r.LegacyAvgUnnecessary:F2} | " +
                          $"{r.NewDeadlocks} | {r.NewBugRuns}/{r.Iterations} | {r.NewAvgUnnecessary:F2} |");

        sb.AppendLine();
        sb.AppendLine($"Legacy: {rows.Sum(r => r.LegacyBugRuns)} runs with an unnecessary placeholder, " +
                      $"{rows.Sum(r => r.LegacyDeadlocks)} deadlocks across {rows.Count * iterations} runs.");
        sb.AppendLine($"New:    {rows.Sum(r => r.NewBugRuns)} runs with an unnecessary placeholder, " +
                      $"{rows.Sum(r => r.NewDeadlocks)} deadlocks across {rows.Count * iterations} runs.");

        var report = sb.ToString();
        _out.WriteLine(report);
        var path = Path.Combine(AppContext.BaseDirectory, "deadlock-comparison.md");
        File.WriteAllText(path, report);
        _out.WriteLine($"Comparison written to: {path}");
    }

    // ---- report ----------------------------------------------------------------------------

    void EmitReport(List<StatRow> rows)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Esiur FetchResource deadlock test results");
        sb.AppendLine();
        sb.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
        sb.AppendLine();
        sb.AppendLine("| Topology | Nodes | Reached | Same-chain breaks | Cross-chain breaks | Waits | Cache hits | Time (ms) | All attached | Deadlock |");
        sb.AppendLine("|----------|------:|--------:|------------------:|-------------------:|------:|-----------:|----------:|:------------:|:--------:|");

        foreach (var r in rows)
            sb.AppendLine($"| {r.Topology} | {r.Nodes} | {r.Reached} | {r.SameChain} | {r.CrossChain} | " +
                          $"{r.Waits} | {r.CacheHits} | {r.Ms:F1} | {(r.AllAttached ? "yes" : "**NO**")} | {(r.Deadlock ? "**YES**" : "no")} |");

        sb.AppendLine();
        sb.AppendLine($"Topologies: {rows.Count}  |  Deadlocks: {rows.Count(r => r.Deadlock)}  |  " +
                      $"Fully attached: {rows.Count(r => r.AllAttached)}/{rows.Count}  |  " +
                      $"Total cycle breaks: same-chain {rows.Sum(r => r.SameChain)}, cross-chain {rows.Sum(r => r.CrossChain)}  |  " +
                      $"Total waits: {rows.Sum(r => r.Waits)}");

        var report = sb.ToString();
        _out.WriteLine(report);

        var path = Path.Combine(AppContext.BaseDirectory, "deadlock-stats.md");
        File.WriteAllText(path, report);
        _out.WriteLine($"Report written to: {path}");
    }
}

[CollectionDefinition("Integration", DisableParallelization = true)]
public class IntegrationCollection { }
