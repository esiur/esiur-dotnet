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

var edges = BuildTopology(topology, ref nodeCount, seed, edgeProb);
var (hasCycle, backEdges) = CycleCensus(nodeCount, edges);

Console.WriteLine($"[Server] topology={topology} nodes={nodeCount} edges={edges.Count} " +
                  $"cyclic={hasCycle} backEdges={backEdges} port={port}");

var wh = new Warehouse();
await wh.Put("sys", new MemoryStore());
// AllowUnauthorizedAccess enables anonymous (None-mode) connections so the test needs no
// credentials — the deadlock behaviour under study is independent of authentication.
var server = await wh.Put("sys/server", new EpServer { Port = (ushort)port, AllowUnauthorizedAccess = true });

var nodes = new Node[nodeCount];
var resources1 = new Resource1[res1Count];
var resources2 = new Resource2[res2Count];

for (var i = 0; i < nodeCount; i++) {
    nodes[i] = new Node { Id = i }; 
    await wh.Put($"sys/n{i}", nodes[i]); 
}

for (var i = 0; i < res1Count; i++)
{
    resources1[i] = new Resource1();
    await wh.Put($"sys/r1_{i}", resources1[i]);
}

for (var i = 0; i < res2Count; i++)
{
    resources2[i] = new Resource2();
    await wh.Put($"sys/r2_{i}", resources2[i]);
}

// randomly assign some resources to each node so the fetches do some work beyond just traversing the links; this also
for(var i = 0; i < nodeCount; i++)
{
    var rng = new Random(seed);
    

    nodes[i].Resources1 = rng.GetItems(resources1, res1Count / 2);
    nodes[i].Resources2 = rng.GetItems(resources2, res2Count / 2);
}

for(var i  =0; i < res1Count; i++)
{
    var rng = new Random(seed);
    var res1Index = rng.Next(res1Count);
    var res2Index = rng.Next(res2Count);
    resources1[i].res1 = resources1[res1Index];
    resources1[i].res2 = resources2[res2Index];
}

for (var i = 0; i < res2Count; i++)
{
    var rng = new Random(seed);
    var res1Index = rng.Next(res1Count);
    var res2Index = rng.Next(res2Count);
    resources2[i].res1 = resources1[res1Index];
    resources2[i].res2 = resources2[res2Index];
}

foreach (var grp in edges.GroupBy(e => e.from))
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

// DFS three-colouring; counts back edges (cycle-closing edges, including self loops).
static (bool hasCycle, int backEdges) CycleCensus(int n, IReadOnlyList<(int from, int to)> edges)
{
    var adj = new List<int>[n];
    for (var i = 0; i < n; i++) adj[i] = new List<int>();
    var back = 0;
    foreach (var (a, b) in edges) { if (a == b) back++; else adj[a].Add(b); }

    var color = new byte[n]; // 0 unvisited, 1 on-stack, 2 done
    for (var s = 0; s < n; s++)
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
                var v = adj[u][idx];
                if (color[v] == 1) back++;
                else if (color[v] == 0) { color[v] = 1; stack.Push((v, 0)); }
            }
            else color[u] = 2;
        }
    }
    return (back > 0, back);
}

static string GetArg(string[] args, string key, string def)
{
    var i = Array.IndexOf(args, key);
    return (i >= 0 && i + 1 < args.Length) ? args[i + 1] : def;
}
