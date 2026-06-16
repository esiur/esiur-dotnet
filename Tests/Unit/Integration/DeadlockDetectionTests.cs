using System;
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
/// Answers the methodological questions a deadlock-prevention experiment must address:
///  (a) the timeout / detection thresholds, justified against the measured completion-time
///      distribution;
///  (b) how a deadlock is detected as distinct from slow processing — via a progress (stall)
///      detector, validated by a NaiveWait resolver that genuinely deadlocks on cycles;
///  (c) that circular dependencies are actually present in the (randomly generated) request pool —
///      counted by static cycle detection (DFS) and by the resolver's cycle-break operations.
/// </summary>
[Collection("Integration")]
public class DeadlockDetectionTests
{
    readonly ITestOutputHelper _out;
    public DeadlockDetectionTests(ITestOutputHelper output) => _out = output;

    // ---- detection thresholds (reported in the paper) --------------------------------------
    // A run is a DEADLOCK if no resource attaches for StallMs while fetches are still pending; it is
    // SLOW (not deadlock) if it is still making progress at HardTimeoutMs. StallMs is ~3 orders of
    // magnitude above the observed completion time, so a stall is unambiguous.
    const int StallMs = 1500;
    const int HardTimeoutMs = 15000;
    const int PollMs = 25;

    enum Outcome { Completed, Deadlocked, SlowTimeout, Faulted }

    static long Counter(string name) => Global.Counters.Contains(name) ? Global.Counters[name] : 0;

    static async Task<IntegrationCluster> StartGraph(int nodes, IEnumerable<(int from, int to)> edges, DeadlockResolutionMode mode)
    {
        var edgeList = edges.ToArray();
        var cluster = await IntegrationCluster.StartAsync(async wh =>
        {
            var ns = new Node[nodes];
            for (var i = 0; i < nodes; i++) { ns[i] = new Node { Id = i }; await wh.Put($"sys/n{i}", ns[i]); }
            foreach (var grp in edgeList.GroupBy(e => e.from))
                ns[grp.Key].Links = grp.Select(e => ns[e.to]).ToArray();
        });

        if (mode == DeadlockResolutionMode.NaiveWait)
        {
            // Node.Links is self-referential at the typedef level. Warm it with the default
            // resolver so this test isolates NaiveWait behavior to the resource graph.
            var nodeTypeDef = cluster.ServerWarehouse.GetLocalTypeDefByName(typeof(Node).FullName ?? nameof(Node));
            if (nodeTypeDef == null)
                throw new InvalidOperationException("Node typedef was not registered.");
            await cluster.Connection.FetchTypeDef(nodeTypeDef.Id, null);
        }

        cluster.Connection.DeadlockResolution = mode;
        return cluster;
    }

    // Fires fetches for all roots and classifies the run using the progress (stall) detector.
    // Uses per-connection counters (each run has a fresh connection) so progress and cycle-break
    // measurements are free of cross-connection contamination from the shared Global.Counters.
    async Task<(Outcome outcome, double ms, long cycleBreaks)> Classify(IntegrationCluster cluster, int[] roots)
    {
        var connection = cluster.Connection;

        var tasks = roots.Select(r =>
        {
            var tcs = new TaskCompletionSource<bool>();
            connection.Get($"sys/n{r}")
                .Then(_ => tcs.TrySetResult(true))
                .Error(ex => tcs.TrySetException((Exception)ex));
            return tcs.Task;
        }).ToArray();
        var all = Task.WhenAll(tasks);

        var sw = Stopwatch.StartNew();
        var lastProgress = connection.AttachedResourceCount;
        var lastProgressMs = 0.0;

        while (true)
        {
            await Task.WhenAny(all, Task.Delay(PollMs));

            if (all.IsCompletedSuccessfully)
            {
                sw.Stop();
                return (Outcome.Completed, sw.Elapsed.TotalMilliseconds, connection.CycleBreakCount);
            }
            if (all.IsFaulted)
            {
                sw.Stop();
                return (Outcome.Faulted, sw.Elapsed.TotalMilliseconds, 0);
            }

            var progress = connection.AttachedResourceCount;
            if (progress != lastProgress) { lastProgress = progress; lastProgressMs = sw.Elapsed.TotalMilliseconds; }

            var sinceProgress = sw.Elapsed.TotalMilliseconds - lastProgressMs;
            if (sinceProgress >= StallMs)   // pending, but no resource attached for the stall window
            {
                sw.Stop();
                return (Outcome.Deadlocked, sw.Elapsed.TotalMilliseconds, 0);
            }
            if (sw.Elapsed.TotalMilliseconds >= HardTimeoutMs) // still progressing but not done
            {
                sw.Stop();
                return (Outcome.SlowTimeout, sw.Elapsed.TotalMilliseconds, 0);
            }
        }
    }

    // ---- (b) deadlock is real and detectable, distinct from slow ----------------------------

    public static IEnumerable<object[]> DemoTopologies() => new[]
    {
        new object[] { "acyclic chain",        5, new[]{ (0,1),(1,2),(2,3),(3,4) }, new[]{0}, false },
        new object[] { "acyclic diamond",      4, new[]{ (0,1),(0,2),(1,3),(2,3) }, new[]{0}, false },
        new object[] { "single-root 4-cycle",  4, new[]{ (0,1),(1,2),(2,3),(3,0) }, new[]{0}, true  },
        new object[] { "concurrent ring x3",   3, new[]{ (0,1),(1,2),(2,0) },       new[]{0,1,2}, true },
    };

    [Theory]
    [MemberData(nameof(DemoTopologies))]
    public async Task NaiveWait_Deadlocks_On_Cycles_While_Resolvers_Complete(
        string name, int nodes, (int, int)[] edges, int[] roots, bool hasCycle)
    {
        // NaiveWait (no cycle handling): must deadlock iff the graph has a cycle.
        await using (var c = await StartGraph(nodes, edges, DeadlockResolutionMode.NaiveWait))
        {
            var (outcome, ms, _) = await Classify(c, roots);
            _out.WriteLine($"[NaiveWait] {name}: {outcome} in {ms:F0} ms");
            Assert.Equal(hasCycle ? Outcome.Deadlocked : Outcome.Completed, outcome);
        }

        // Both production resolvers must complete regardless of cycles.
        foreach (var mode in new[] { DeadlockResolutionMode.LegacyCrossChainPlaceholder, DeadlockResolutionMode.WaitWithCycleDetection })
        {
            await using var c = await StartGraph(nodes, edges, mode);
            var (outcome, ms, breaks) = await Classify(c, roots);
            _out.WriteLine($"[{mode}] {name}: {outcome} in {ms:F1} ms, cycle-breaks={breaks}");
            Assert.Equal(Outcome.Completed, outcome);
        }
    }

    // ---- (c) circular dependencies in a random request pool ---------------------------------

    // Static cycle detection over a directed graph (DFS three-colouring). Returns whether any cycle
    // exists and the number of back edges (cycle-closing edges, including self loops).
    static bool HasCycle(int n, IReadOnlyList<(int from, int to)> edges, out int backEdges)
    {
        var adj = new List<int>[n];
        for (var i = 0; i < n; i++) adj[i] = new List<int>();
        var back = 0;
        foreach (var (a, b) in edges)
        {
            if (a == b) back++;        // self loop
            else adj[a].Add(b);
        }

        var color = new byte[n];       // 0 = unvisited, 1 = on stack, 2 = done
        var stack = new Stack<(int node, int idx)>();

        for (var s = 0; s < n; s++)
        {
            if (color[s] != 0) continue;
            stack.Push((s, 0));
            color[s] = 1;
            while (stack.Count > 0)
            {
                var (u, idx) = stack.Pop();
                if (idx < adj[u].Count)
                {
                    stack.Push((u, idx + 1));
                    var v = adj[u][idx];
                    if (color[v] == 1) back++;             // back edge -> cycle
                    else if (color[v] == 0) { color[v] = 1; stack.Push((v, 0)); }
                }
                else color[u] = 2;
            }
        }

        backEdges = back;
        return back > 0;
    }

    static (int, int)[] RandomGraph(int n, double edgeProbability, Random rng)
    {
        var edges = new List<(int, int)>();
        for (var i = 0; i < n; i++)
            for (var j = 0; j < n; j++)
                if (i != j && rng.NextDouble() < edgeProbability)
                    edges.Add((i, j));
        return edges.ToArray();
    }

    [Fact]
    public async Task RandomRequestPool_ContainsCycles_And_Resolves_Without_Deadlock()
    {
        const int graphs = 40;
        const int nodes = 8;
        const double edgeProbability = 0.22;
        var rng = new Random(20260603); // fixed seed -> reproducible pool

        int graphsWithCycles = 0, totalBackEdges = 0;
        int completed = 0, deadlocked = 0, slow = 0;
        long totalCycleBreaks = 0;
        var times = new List<double>();

        for (var g = 0; g < graphs; g++)
        {
            var edges = RandomGraph(nodes, edgeProbability, rng);
            if (HasCycle(nodes, edges, out var backEdges)) { graphsWithCycles++; totalBackEdges += backEdges; }

            await using var cluster = await StartGraph(nodes, edges, DeadlockResolutionMode.WaitWithCycleDetection);
            var (outcome, ms, breaks) = await Classify(cluster, Enumerable.Range(0, nodes).ToArray());
            totalCycleBreaks += breaks;
            switch (outcome)
            {
                case Outcome.Completed: completed++; times.Add(ms); break;
                case Outcome.Deadlocked: deadlocked++; break;
                case Outcome.SlowTimeout: slow++; break;
            }
        }

        EmitDetectionReport(graphs, nodes, edgeProbability, graphsWithCycles, totalBackEdges,
                            totalCycleBreaks, completed, deadlocked, slow, times);

        // (c) the random pool must actually contain circular dependencies, otherwise the experiment
        // would not exercise the mechanism at all.
        Assert.True(graphsWithCycles > 0, "random request pool contained no circular dependencies");
        // and the new resolver must resolve every one of them without deadlock.
        Assert.Equal(0, deadlocked);
        Assert.Equal(0, slow);
    }

    void EmitDetectionReport(int graphs, int nodes, double edgeProb, int graphsWithCycles, int backEdges,
                             long cycleBreaks, int completed, int deadlocked, int slow, List<double> times)
    {
        times.Sort();
        double Pct(double p) => times.Count == 0 ? 0 : times[(int)Math.Min(times.Count - 1, p * times.Count)];

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Esiur deadlock detection — methodology and random-pool census");
        sb.AppendLine();
        sb.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
        sb.AppendLine();
        sb.AppendLine("## (a) Detection thresholds");
        sb.AppendLine($"- Stall window (no-progress => deadlock): **{StallMs} ms**");
        sb.AppendLine($"- Hard timeout (progress but unfinished => slow): **{HardTimeoutMs} ms**");
        sb.AppendLine($"- Observed completion time over {times.Count} successful runs: " +
                      $"median **{Pct(0.5):F1} ms**, p99 **{Pct(0.99):F1} ms**, max **{(times.Count > 0 ? times[^1] : 0):F1} ms**.");
        sb.AppendLine($"  The stall window is ~{(times.Count > 0 && Pct(0.5) > 0 ? StallMs / Pct(0.5) : 0):F0}x the median completion time, so a stall is unambiguously a deadlock, not slow processing.");
        sb.AppendLine();
        sb.AppendLine("## (b) Deadlock detection");
        sb.AppendLine("A run is classified DEADLOCKED when fetches remain pending yet the progress counter");
        sb.AppendLine("(resources attached) does not advance for the stall window. Validated by the NaiveWait");
        sb.AppendLine("resolver, which genuinely deadlocks on cyclic graphs and is detected as such.");
        sb.AppendLine();
        sb.AppendLine("## (c) Random request pool — circular-dependency census");
        sb.AppendLine($"- Pool: {graphs} random directed graphs, {nodes} nodes each, edge probability {edgeProb:F2}, fixed seed.");
        sb.AppendLine($"- Graphs containing >=1 cycle (static DFS): **{graphsWithCycles}/{graphs}** ({100.0 * graphsWithCycles / graphs:F0}%), {backEdges} cycle-closing edges total.");
        sb.AppendLine($"- Cycle-break operations performed by the resolver: **{cycleBreaks}** (circular dependencies actually exercised).");
        sb.AppendLine($"- Outcomes (new resolver): completed **{completed}**, deadlocked **{deadlocked}**, slow **{slow}**.");

        var report = sb.ToString();
        _out.WriteLine(report);
        var path = Path.Combine(AppContext.BaseDirectory, "deadlock-detection.md");
        File.WriteAllText(path, report);
        _out.WriteLine($"Report written to: {path}");
    }
}
