// ============================================================
// Distributed deadlock test — CLIENT NODE
// Connects to the server, fetches the resource graph concurrently, and classifies each run as
// COMPLETED, DEADLOCKED, or SLOW using a progress (stall) detector — deadlock is detected as the
// absence of attachment progress while requests are still pending, NOT as a blunt timeout, so it is
// distinguished from slow WAN processing. Reports completion-time distribution, cycle-break and
// unnecessary-placeholder counts, and the published-state of delivered resources.
//
// Usage:
//   dotnet run -- --host SERVER_IP --port 10950 --nodes 8 --mode WaitWithCycleDetection --iterations 20
//   dotnet run -- --host SERVER_IP --port 10950 --nodes 4 --roots 0 --mode WaitWithCycleDetection   (single-root cycle)
//   dotnet run -- --host SERVER_IP --port 10950 --nodes 8 --mode NaiveWait                            (control: deadlocks)
// Modes: WaitWithCycleDetection (default) | NaiveWait | LegacyCrossChainPlaceholder
// ============================================================

using System.Collections;
using System.Diagnostics;
using Esiur.Protocol;
using Esiur.Resource;

var host       = GetArg(args, "--host", "127.0.0.1");
var port       = int.Parse(GetArg(args, "--port", "10950"));
var nodeCount  = int.Parse(GetArg(args, "--nodes", "100"));
var modeArg    = GetArg(args, "--mode", "NaiveWait");
var iterations = int.Parse(GetArg(args, "--iterations", "20"));
var stallMs    = int.Parse(GetArg(args, "--stall-ms", "5000"));
var hardMs     = int.Parse(GetArg(args, "--hard-ms", "60000"));
var rootsArg   = GetArg(args, "--roots", "all");

if (!Enum.TryParse<DeadlockResolutionMode>(modeArg, ignoreCase: true, out var mode))
{
    Console.WriteLine($"Unknown --mode '{modeArg}'. Use WaitWithCycleDetection | NaiveWait | LegacyCrossChainPlaceholder.");
    return;
}

var roots = rootsArg.Equals("all", StringComparison.OrdinalIgnoreCase)
    ? Enumerable.Range(0, nodeCount).Select(i => $"sys/n{i}").ToArray()
    : rootsArg.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
              .Select(s => $"sys/n{int.Parse(s)}").ToArray();

Console.WriteLine($"[Client] {host}:{port} nodes={nodeCount} mode={mode} roots={roots.Length} " +
                  $"iterations={iterations} stallMs={stallMs} hardMs={hardMs}");
Console.WriteLine($"[Client] {"iter",-5}{"outcome",-14}{"ms",10}{"breaks",10}{"unnec",8}{"unpublished",13}");

var rows = new List<(int iter, string outcome, double ms, long breaks, long unnec, int unpublished)>();

for (var it = 0; it < iterations; it++)
{
    // Fresh warehouse + connection per iteration so the per-connection counters start at 0.
    var wh = new Warehouse();
    EpConnection con;
    try { con = await wh.Get<EpConnection>($"ep://{host}:{port}"); }
    catch (Exception ex) { Console.WriteLine($"[Client] connect failed: {ex.Message}"); return; }

    con.DeadlockResolution = mode;
    Console.WriteLine($"[Client] iter {it + 1}: connected, fetching {roots.Length} roots...");

    var (outcome, ms, results) = await Classify(con, roots, stallMs, hardMs);
    var unpublished = results == null ? -1 : CountUnpublished(results);

    rows.Add((it + 1, outcome, ms, con.CycleBreakCount, con.UnnecessaryPlaceholderCount, unpublished));
    Console.WriteLine($"[Client] {it + 1,-5}{outcome,-14}{ms,10:F1}{con.CycleBreakCount,10}{con.UnnecessaryPlaceholderCount,8}{unpublished,13}");

    try { con.Destroy(); } catch { }
}

// ---- summary ----------------------------------------------------------------------------------
var completed = rows.Where(r => r.outcome == "Completed").ToList();
var times = completed.Select(r => r.ms).OrderBy(x => x).ToList();
double Pct(double p) => times.Count == 0 ? 0 : times[(int)Math.Min(times.Count - 1, p * times.Count)];

Console.WriteLine();
Console.WriteLine($"[Client] === summary ({mode}) ===");
Console.WriteLine($"  completed={completed.Count}  deadlocked={rows.Count(r => r.outcome == "Deadlocked")}  " +
                  $"slow={rows.Count(r => r.outcome == "SlowTimeout")}  faulted={rows.Count(r => r.outcome == "Faulted")}");
Console.WriteLine($"  completion ms: median={Pct(0.5):F1}  p99={Pct(0.99):F1}  max={(times.Count > 0 ? times[^1] : 0):F1}");
Console.WriteLine($"  cycle-breaks total={rows.Sum(r => r.breaks)}  unnecessary-placeholders total={rows.Sum(r => r.unnec)}");
Console.WriteLine($"  partial deliveries (unpublished>0) in {rows.Count(r => r.unpublished > 0)}/{rows.Count} runs");

var csv = "iteration,outcome,ms,cycle_breaks,unnecessary_placeholders,unpublished\n" +
          string.Join("\n", rows.Select(r => $"{r.iter},{r.outcome},{r.ms:F1},{r.breaks},{r.unnec},{r.unpublished}"));
var outFile = $"deadlock_{mode}_{host}_{port}.csv";
await File.WriteAllTextAsync(outFile, csv);
Console.WriteLine($"[Client] results written to {outFile}");

Console.ReadLine();

// ---- stall-based classification ---------------------------------------------------------------

// Fires fetches for all roots and classifies the run. A run is DEADLOCKED when fetches are still
// pending but the connection's attached-resource count has not advanced for stallMs (no progress);
// SLOW if it is still progressing at hardMs; COMPLETED when every fetch resolves.
static async Task<(string outcome, double ms, EpResource[]? results)> Classify(
    EpConnection con, string[] roots, int stallMs, int hardMs)
{
    var tasks = roots.Select(p =>
    {
        var tcs = new TaskCompletionSource<IResource?>();
        con.Get(p)
           .Then(r => tcs.TrySetResult(r as IResource))
           .Error(ex => { Console.WriteLine($"[Client] Get({p}) error: {ex.Message}"); tcs.TrySetException((Exception)ex); });
        return tcs.Task;
    }).ToArray();
    var all = Task.WhenAll(tasks);

    var sw = Stopwatch.StartNew();
    var lastProgress = con.AttachedResourceCount;
    var lastProgressMs = 0.0;

    while (true)
    {
        await Task.WhenAny(all, Task.Delay(25));

        if (all.IsCompletedSuccessfully)
        {
            sw.Stop();
            return ("Completed", sw.Elapsed.TotalMilliseconds, all.Result.OfType<EpResource>().ToArray());
        }
        if (all.IsFaulted)
        {
            sw.Stop();
            return ("Faulted", sw.Elapsed.TotalMilliseconds, null);
        }

        var progress = con.AttachedResourceCount;
        if (progress != lastProgress) { lastProgress = progress; lastProgressMs = sw.Elapsed.TotalMilliseconds; }

        if (sw.Elapsed.TotalMilliseconds - lastProgressMs >= stallMs) { sw.Stop(); return ("Deadlocked", sw.Elapsed.TotalMilliseconds, null); }
        if (sw.Elapsed.TotalMilliseconds >= hardMs) { sw.Stop(); return ("SlowTimeout", sw.Elapsed.TotalMilliseconds, null); }
    }
}

// Counts resources reachable from the delivered roots that are not Published — i.e. handed to the
// application while their dependency graph was not fully attached. Links is property index 1.
static int CountUnpublished(EpResource[] roots)
{
    var seen = new HashSet<uint>();
    var queue = new Queue<EpResource>(roots);
    var unpublished = 0;

    while (queue.Count > 0)
    {
        var node = queue.Dequeue();
        if (node == null || !seen.Add(node.ResourceInstanceId)) continue;

        if (node.Status != ResourceStatus.Published) unpublished++;

        if (node.Status == ResourceStatus.Attached && node.TryGetPropertyValue((byte)1, out var linksObj) && linksObj is IEnumerable links)
            foreach (var child in links)
                if (child is EpResource childResource)
                    queue.Enqueue(childResource);
    }
    return unpublished;
}

static string GetArg(string[] args, string key, string def)
{
    var i = Array.IndexOf(args, key);
    return (i >= 0 && i + 1 < args.Length) ? args[i + 1] : def;
}
