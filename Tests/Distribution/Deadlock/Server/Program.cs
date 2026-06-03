// ============================================================
// Distributed deadlock test — SERVER NODE
// Hosts a configurable graph of Node resources (sys/n0 .. sys/n{N-1}) whose references can form
// cycles. A client on another node fetches the graph and measures whether the recursive-attachment
// resolver completes or deadlocks. The server prints the cycle census of the deployed graph so the
// experiment can state, for the record, that circular dependencies were actually generated.
//
// Usage:
//   dotnet run -- --port 10950 --topology ring --nodes 8
//   dotnet run -- --port 10950 --topology random --nodes 12 --seed 20260603 --edge-prob 0.22
//   dotnet run -- --port 10950 --topology staggered
// Topologies: ring | cycle | chain | diamond | complete | staggered | random
// ============================================================

using Esiur.Protocol;
using Esiur.Resource;
using Esiur.Stores;
using Esiur.Tests.Deadlock.Server;

var port     = int.Parse(GetArg(args, "--port", "10950"));
var topology = GetArg(args, "--topology", "ring").ToLowerInvariant();
var nodeCount = int.Parse(GetArg(args, "--nodes", "100"));
var res1Count = int.Parse(GetArg(args, "--res1", "100"));
var res2Count = int.Parse(GetArg(args, "--res2", "100"));
var seed     = int.Parse(GetArg(args, "--seed", "20260603"));
var edgeProb = double.Parse(GetArg(args, "--edge-prob", "0.22"));

var nodeEdges = BuildTopology(topology, ref nodeCount, seed, edgeProb);

// One RNG, seeded once, for all random assignment. (Previously a new Random(seed) was created
// inside each loop, so every node/resource pointed at the same target and the cycle structure
// collapsed; one RNG yields a genuinely random, densely cyclic resource graph.)
var rng = new Random(seed);

// Plan the resource cross-references as indices first, so the FULL graph (nodes + Resource1 +
// Resource2 + every reference) can be censused for circular dependencies before it is wired.
var nodeRes1 = new int[nodeCount][];
var nodeRes2 = new int[nodeCount][];
for (var i = 0; i < nodeCount; i++)
{
    nodeRes1[i] = Sample(rng, res1Count, res1Count / 2);
    nodeRes2[i] = Sample(rng, res2Count, res2Count / 2);
}

var res1Ref1 = new int[res1Count];
var res1Ref2 = new int[res1Count];
for (var i = 0; i < res1Count; i++)
{
    res1Ref1[i] = res1Count > 0 ? rng.Next(res1Count) : -1;
    res1Ref2[i] = res2Count > 0 ? rng.Next(res2Count) : -1;
}

var res2Ref1 = new int[res2Count];
var res2Ref2 = new int[res2Count];
for (var i = 0; i < res2Count; i++)
{
    res2Ref1[i] = res1Count > 0 ? rng.Next(res1Count) : -1;
    res2Ref2[i] = res2Count > 0 ? rng.Next(res2Count) : -1;
}

var totalResources = nodeCount + res1Count + res2Count;
var (hasCycle, backEdges, totalEdges) = FullCensus(
    nodeCount, res1Count, res2Count, nodeEdges, nodeRes1, nodeRes2, res1Ref1, res1Ref2, res2Ref1, res2Ref2);

Console.WriteLine($"[Server] topology={topology} nodes={nodeCount} res1={res1Count} res2={res2Count} " +
                  $"totalResources={totalResources} edges={totalEdges} cyclic={hasCycle} backEdges={backEdges} port={port}");

var wh = new Warehouse();
await wh.Put("sys", new MemoryStore());
// AllowUnauthorizedAccess enables anonymous (None-mode) connections so the test needs no
// credentials — the deadlock behaviour under study is independent of authentication.
var server = await wh.Put("sys/server", new EpServer { Port = (ushort)port, AllowUnauthorizedAccess = true });

var nodes = new Node[nodeCount];
var resources1 = new Resource1[res1Count];
var resources2 = new Resource2[res2Count];

for (var i = 0; i < nodeCount; i++) { nodes[i] = new Node { Id = i }; await wh.Put($"sys/n{i}", nodes[i]); }
for (var i = 0; i < res1Count; i++) { resources1[i] = new Resource1(); await wh.Put($"sys/r1_{i}", resources1[i]); }
for (var i = 0; i < res2Count; i++) { resources2[i] = new Resource2(); await wh.Put($"sys/r2_{i}", resources2[i]); }

// Wire the planned references: each Node also pulls in a random subset of Resource1/Resource2, and
// the resources cross-reference one another, creating dense cycles for the fetch to resolve.
for (var i = 0; i < nodeCount; i++)
{
    nodes[i].Resources1 = nodeRes1[i].Select(k => resources1[k]).ToArray();
    nodes[i].Resources2 = nodeRes2[i].Select(k => resources2[k]).ToArray();
}
for (var i = 0; i < res1Count; i++)
{
    if (res1Ref1[i] >= 0) resources1[i].res1 = resources1[res1Ref1[i]];
    if (res1Ref2[i] >= 0) resources1[i].res2 = resources2[res1Ref2[i]];
}
for (var i = 0; i < res2Count; i++)
{
    if (res2Ref1[i] >= 0) resources2[i].res1 = resources1[res2Ref1[i]];
    if (res2Ref2[i] >= 0) resources2[i].res2 = resources2[res2Ref2[i]];
}
foreach (var grp in nodeEdges.GroupBy(e => e.from))
    nodes[grp.Key].Links = grp.Select(e => nodes[e.to]).ToArray();

await wh.Open();

Console.WriteLine($"[Server] Listening on port {port}. Hosting {nodeCount} nodes: sys/n0 .. sys/n{nodeCount - 1}.");
Console.WriteLine($"[Server] The deployed request graph {(hasCycle ? "CONTAINS circular dependencies" : "is acyclic")} " +
                  $"({backEdges} cycle-closing edge(s)).");
Console.WriteLine($"[Server] Point the client at this host:port with --nodes {nodeCount}. Press Ctrl+C to stop.");

// Stay up until Ctrl+C (works whether or not stdin is interactive / redirected).
var stop = new TaskCompletionSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.TrySetResult(); };
await stop.Task;
await wh.Close();


// ---- topology + cycle census -------------------------------------------------------------

static List<(int from, int to)> BuildTopology(string topo, ref int n, int seed, double edgeProb)
{
    var edges = new List<(int, int)>();
    switch (topo)
    {
        case "ring":                              // i -> (i+1) mod n; every node a root
            for (var i = 0; i < n; i++) edges.Add((i, (i + 1) % n));
            break;
        case "cycle":                             // single-root cycle 0->1->..->n-1->0
            for (var i = 0; i < n - 1; i++) edges.Add((i, i + 1));
            edges.Add((n - 1, 0));
            break;
        case "chain":                             // acyclic control
            for (var i = 0; i < n - 1; i++) edges.Add((i, i + 1));
            break;
        case "diamond":                           // acyclic control: 0->1,0->2,1->3,2->3
            n = Math.Max(n, 4);
            edges.AddRange(new[] { (0, 1), (0, 2), (1, 3), (2, 3) });
            break;
        case "complete":                          // every ordered pair
            for (var i = 0; i < n; i++) for (var j = 0; j < n; j++) if (i != j) edges.Add((i, j));
            break;
        case "staggered":                         // X (0) and Y (1) share S; Y reaches S late; no cycle
        {
            var e = new List<(int, int)>();
            var next = 2;
            int Chain(int from, int depth) { for (var d = 0; d < depth; d++) { e.Add((from, next)); from = next; next++; } return from; }
            var xTail = Chain(0, 0);              // X reaches S immediately
            var yTail = Chain(1, 3);              // Y reaches S through a 3-hop chain
            var shared = next++;
            e.Add((xTail, shared)); e.Add((yTail, shared));
            Chain(shared, 3);                     // S has its own deep chain
            n = next;
            return e;
        }
        case "random":                            // Erdos-Renyi directed graph, fixed seed
        {
            var rng = new Random(seed);
            for (var i = 0; i < n; i++) for (var j = 0; j < n; j++) if (i != j && rng.NextDouble() < edgeProb) edges.Add((i, j));
            break;
        }
        default:
            throw new ArgumentException($"Unknown topology '{topo}'. Use ring|cycle|chain|diamond|complete|staggered|random.");
    }
    return edges;
}

// k indices drawn (with replacement) from [0, count); empty if count or k is 0.
static int[] Sample(Random rng, int count, int k)
{
    if (count <= 0 || k <= 0) return Array.Empty<int>();
    var result = new int[k];
    for (var i = 0; i < k; i++) result[i] = rng.Next(count);
    return result;
}

// Censuses the FULL request graph — Node Links + Node->Resource1/2 + Resource1/2 cross-references —
// for circular dependencies via DFS three-colouring. Vertices: [0..nodes) nodes, then res1, then
// res2. Returns whether the graph is cyclic, the number of cycle-closing (back) edges, and the
// total edge count.
static (bool hasCycle, int backEdges, int totalEdges) FullCensus(
    int nodes, int r1, int r2,
    IReadOnlyList<(int from, int to)> nodeEdges,
    int[][] nodeRes1, int[][] nodeRes2,
    int[] res1Ref1, int[] res1Ref2, int[] res2Ref1, int[] res2Ref2)
{
    var v = nodes + r1 + r2;
    int R1(int i) => nodes + i;
    int R2(int i) => nodes + r1 + i;

    var adj = new List<int>[v];
    for (var i = 0; i < v; i++) adj[i] = new List<int>();
    var total = 0;
    void Add(int a, int b) { adj[a].Add(b); total++; }

    foreach (var (a, b) in nodeEdges) Add(a, b);
    for (var i = 0; i < nodes; i++)
    {
        foreach (var k in nodeRes1[i]) Add(i, R1(k));
        foreach (var k in nodeRes2[i]) Add(i, R2(k));
    }
    for (var i = 0; i < r1; i++)
    {
        if (res1Ref1[i] >= 0) Add(R1(i), R1(res1Ref1[i]));
        if (res1Ref2[i] >= 0) Add(R1(i), R2(res1Ref2[i]));
    }
    for (var i = 0; i < r2; i++)
    {
        if (res2Ref1[i] >= 0) Add(R2(i), R1(res2Ref1[i]));
        if (res2Ref2[i] >= 0) Add(R2(i), R2(res2Ref2[i]));
    }

    var back = 0;
    var color = new byte[v]; // 0 unvisited, 1 on-stack, 2 done
    for (var s = 0; s < v; s++)
    {
        if (color[s] != 0) continue;
        var stack = new Stack<(int node, int idx)>();
        stack.Push((s, 0)); color[s] = 1;
        while (stack.Count > 0)
        {
            var (u, idx) = stack.Pop();
            if (idx < adj[u].Count)
            {
                stack.Push((u, idx + 1));
                var w = adj[u][idx];
                if (w == u) back++;                  // self-loop
                else if (color[w] == 1) back++;      // back edge -> cycle
                else if (color[w] == 0) { color[w] = 1; stack.Push((w, 0)); }
            }
            else color[u] = 2;
        }
    }
    return (back > 0, back, total);
}

static string GetArg(string[] args, string key, string def)
{
    var i = Array.IndexOf(args, key);
    return (i >= 0 && i + 1 < args.Length) ? args[i + 1] : def;
}
