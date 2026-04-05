// ============================================================
// Test 3: Concurrent Attachments — COMBINED (server + clients
// in the same process for local stress testing, or run the
// server section separately for multi-machine testing).
//
// Fires N concurrent Warehouse.Get calls simultaneously and
// measures:
//   - Time for all proxies to reach Ready state
//   - Whether any attachments fail or deadlock (timeout)
//   - Distribution of per-attachment latency
//
// This directly stress-tests Algorithm 1 (FETCH-RESOURCE) and
// the parallel deadlock detection mechanism from Section V.D.
//
// Usage (single process):   dotnet run -- --mode both --concurrent 50 --resources 200
// Usage (server only):      dotnet run -- --mode server --resources 200 --port 10902
// Usage (client only):      dotnet run -- --mode client --host 127.0.0.1 --concurrent 50 --resources 200
// ============================================================

using Esiur.Protocol;
using Esiur.Resource;
using Esiur.Stores;
using System.Diagnostics;

var mode        = GetArg(args, "--mode",       "both");
var host        = GetArg(args, "--host",       "127.0.0.1");
var port        = int.Parse(GetArg(args, "--port",       "10902"));
var concurrent  = int.Parse(GetArg(args, "--concurrent", "50"));
var resources   = int.Parse(GetArg(args, "--resources",  "200"));
var timeoutMs   = int.Parse(GetArg(args, "--timeout",    "10000"));
var rounds      = int.Parse(GetArg(args, "--rounds",     "5"));

var clientWh = new Warehouse();
var serverWh = new Warehouse();
// ----------------------------------------------------------------
// SERVER SIDE
// ----------------------------------------------------------------
if (mode == "server" || mode == "both")
{
    await serverWh.Put("sys", new MemoryStore());
    await serverWh.Put("sys/server", new EpServer() { Port = (ushort)port });

    for (int i = 0; i < resources; i++)
    {
        await serverWh.Put($"sys/sensor_{i}", new SensorResource { SensorId = i, Value = i });
    }

    await serverWh.Open();
    Console.WriteLine($"[Server-T3] Ready: {resources} resources on port {port}");

    if (mode == "server")
    {
        Console.WriteLine("Press ENTER to stop.");
        Console.ReadLine();
        await serverWh.Close();
        return;
    }

    // Give server a moment to fully initialise before client fires
    await Task.Delay(500);
}

// ----------------------------------------------------------------
// CLIENT SIDE
// ----------------------------------------------------------------
Console.WriteLine($"[Client-T3] concurrent={concurrent}  resources={resources}  rounds={rounds}");

var roundResults = new List<RoundResult>();

for (int round = 0; round < rounds; round++)
{
    Console.WriteLine($"\n[Client-T3] Round {round + 1}/{rounds}");

    // Pick `concurrent` random resource indices (may overlap — intentional,
    // because overlapping triggers the "already in progress" path of Algorithm 1)
    var rng = new Random(round);
    var targets = Enumerable.Range(0, concurrent)
        .Select(_ => rng.Next(resources))
        .ToArray();

    long succeeded = 0, failed = 0, timedOut = 0;
    var latencies = new double[concurrent];

    var roundSw = Stopwatch.StartNew();

    // Fire all attachments simultaneously
    var tasks = targets.Select((resourceIdx, taskIdx) => Task.Run(async () =>
    {
        var sw = Stopwatch.StartNew();
        using var cts = new CancellationTokenSource(timeoutMs);
        try
        {
            var proxy = await clientWh.Get<IResource>(
                $"ep://{host}:{port}/sys/sensor_{resourceIdx}");

            sw.Stop();
            latencies[taskIdx] = sw.Elapsed.TotalMilliseconds;

            if (proxy != null)
                Interlocked.Increment(ref succeeded);
            else
                Interlocked.Increment(ref failed);
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            latencies[taskIdx] = timeoutMs;
            Interlocked.Increment(ref timedOut);
            Console.WriteLine($"  [!] Timeout on sensor_{resourceIdx} after {timeoutMs}ms");
        }
        catch (Exception ex)
        {
            sw.Stop();
            latencies[taskIdx] = sw.Elapsed.TotalMilliseconds;
            Interlocked.Increment(ref failed);
            Console.WriteLine($"  [!] Error on sensor_{resourceIdx}: {ex.Message}");
        }
    })).ToArray();

    await Task.WhenAll(tasks);
    roundSw.Stop();

    var sorted = latencies.OrderBy(x => x).ToArray();
    int n = sorted.Length;

    var result = new RoundResult
    {
        Round        = round + 1,
        Concurrent   = concurrent,
        Succeeded    = succeeded,
        Failed       = failed,
        TimedOut     = timedOut,
        TotalMs      = roundSw.Elapsed.TotalMilliseconds,
        MinMs        = sorted[0],
        P50Ms        = sorted[(int)(n * 0.50)],
        P95Ms        = sorted[(int)(n * 0.95)],
        P99Ms        = sorted[(int)(n * 0.99)],
        MaxMs        = sorted[n - 1],
        MeanMs       = sorted.Average()
    };
    roundResults.Add(result);

    Console.WriteLine($"  succeeded={succeeded}/{concurrent}  failed={failed}  timedOut={timedOut}");
    Console.WriteLine($"  total_wall={result.TotalMs:F0}ms");
    Console.WriteLine($"  latency: min={result.MinMs:F1}  p50={result.P50Ms:F1}  p95={result.P95Ms:F1}  " +
                      $"p99={result.P99Ms:F1}  max={result.MaxMs:F1}  mean={result.MeanMs:F1}  (ms)");

    // Release all proxies before next round to reset attachment state
    GC.Collect();
    await Task.Delay(1000);
}

// ----------------------------------------------------------------
// CSV output
// ----------------------------------------------------------------
var csv = "round,concurrent,succeeded,failed,timed_out,total_wall_ms,min_ms,p50_ms,p95_ms,p99_ms,max_ms,mean_ms\n" +
          string.Join("\n", roundResults.Select(r =>
              $"{r.Round},{r.Concurrent},{r.Succeeded},{r.Failed},{r.TimedOut}," +
              $"{r.TotalMs:F1},{r.MinMs:F2},{r.P50Ms:F2},{r.P95Ms:F2},{r.P99Ms:F2},{r.MaxMs:F2},{r.MeanMs:F2}"));

await File.WriteAllTextAsync("test3_concurrent_attach.csv", csv);
Console.WriteLine("\n[Client-T3] Results written to test3_concurrent_attach.csv");

if (mode == "server" || mode == "both")
    await serverWh.Close();

if (mode == "client" || mode == "both")
    await clientWh.Close();


// ----------------------------------------------------------------
// Helpers
// ----------------------------------------------------------------
static string GetArg(string[] args, string key, string def)
{
    int i = Array.IndexOf(args, key);
    return (i >= 0 && i + 1 < args.Length) ? args[i + 1] : def;
}

record RoundResult
{
    public int    Round;
    public int    Concurrent;
    public long   Succeeded, Failed, TimedOut;
    public double TotalMs, MinMs, P50Ms, P95Ms, P99Ms, MaxMs, MeanMs;
}
