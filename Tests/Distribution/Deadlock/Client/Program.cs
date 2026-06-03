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
Console.WriteLine($"[Client] {"iter",-5}{"outcome",-13}{"ms",10}{"attached",10}{"breaks",9}{"unnec",8}{"unpub",8}");

var rows = new List<(int iter, string outcome, double ms, long attached, long breaks, long unnec, int unpublished)>();

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

    rows.Add((it + 1, outcome, ms, con.AttachedResourceCount, con.CycleBreakCount, con.UnnecessaryPlaceholderCount, unpublished));
    Console.WriteLine($"[Client] {it + 1,-5}{outcome,-13}{ms,10:F1}{con.AttachedResourceCount,10}{con.CycleBreakCount,9}{con.UnnecessaryPlaceholderCount,8}{unpublished,8}");

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
Console.WriteLine($"  resources attached per run (max)={rows.Max(r => r.attached)}");
Console.WriteLine($"  completion ms: median={Pct(0.5):F1}  p99={Pct(0.99):F1}  max={(times.Count > 0 ? times[^1] : 0):F1}");
Console.WriteLine($"  cycle-breaks total={rows.Sum(r => r.breaks)}  unnecessary-placeholders total={rows.Sum(r => r.unnec)}");
Console.WriteLine($"  partial deliveries (unpublished>0) in {rows.Count(r => r.unpublished > 0)}/{rows.Count} runs");

var csv = "iteration,outcome,ms,attached,cycle_breaks,unnecessary_placeholders,unpublished\n" +
          string.Join("\n", rows.Select(r => $"{r.iter},{r.outcome},{r.ms:F1},{r.attached},{r.breaks},{r.unnec},{r.unpublished}"));
var outFile = $"deadlock_{mode}_{host}_{port}.csv";
await File.WriteAllTextAsync(outFile, csv);
Console.WriteLine($"[Client] results written to {outFile}");

// Keep the window open only when run interactively; scripted/redirected runs exit immediately.
if (!Console.IsInputRedirected)
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
// application while their dependency graph was not fully attached. Traverses every reference-typed
// property (Node.Links/Resources1/Resources2 and the Resource1/Resource2 cross-references) so the
// whole delivered graph is checked, not just the node links.
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

        // Only attached/published resources can be safely read for further references.
        if (node.Status != ResourceStatus.Attached && node.Status != ResourceStatus.Published) continue;

        var properties = node.Instance.Definition.Properties.Length;
        for (byte p = 0; p < properties; p++)
            if (node.TryGetPropertyValue(p, out var value))
                Flatten(value, queue);
    }
    return unpublished;
}

static void Flatten(object? value, Queue<EpResource> queue)
{
    if (value is EpResource resource)
        queue.Enqueue(resource);
    else if (value is IEnumerable sequence && value is not string)
        foreach (var item in sequence)
            Flatten(item, queue);
}

static string GetArg(string[] args, string key, string def)
{
    var i = Array.IndexOf(args, key);
    return (i >= 0 && i + 1 < args.Length) ? args[i + 1] : def;
}
