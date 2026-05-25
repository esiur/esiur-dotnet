// ============================================================
// Scalability Extension: Concurrent Attach Sweep
// ------------------------------------------------------------
// Extends Tests/Distribution/ConcurrentAttach with:
//   - Sweep over a wider range of concurrent request counts A.
//   - More rounds per A for confidence-interval reporting.
//   - Auto-stop when timeouts or failures appear (the
//     saturation signal for concurrent attach is different
//     from fan-out: it's *correctness* failure, not slowdown).
//   - Unified CSV output suitable for direct plotting.
//
// Server: re-use the existing
//   Tests/Distribution/ConcurrentAttach with --mode server.
// Or run this binary with --mode both.
// ------------------------------------------------------------
// Usage:
//   dotnet run -- --mode both --resources 200 \
//                 --a-values 10,25,50,100,250,500,1000,2000 \
//                 --rounds 10 --timeout 10000
// ============================================================

using Esiur.Protocol;
using Esiur.Resource;
using Esiur.Stores;
using Esiur.Tests.ConcurrentAttachSweep;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;

var mode = GetArg(args, "--mode", "both");
var host = GetArg(args, "--host", "127.0.0.1");
var port = int.Parse(GetArg(args, "--port", "10902"));
var resources = int.Parse(GetArg(args, "--resources", "200"));
var timeoutMs = int.Parse(GetArg(args, "--timeout", "10000"));
var rounds = int.Parse(GetArg(args, "--rounds", "10"));
var aValStr = GetArg(args, "--a-values", "10,25,50,100,250,500,1000,2000");
var outCsv = GetArg(args, "--output", "concurrent_attach_sweep.csv");
var aValues = aValStr.Split(',').Select(int.Parse).ToArray();

var serverWh = new Warehouse();
var clientWh = new Warehouse();

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
    Console.WriteLine($"[Server-T3+] Ready: {resources} resources on port {port}");

    if (mode == "server")
    {
        Console.WriteLine("Press ENTER to stop.");
        Console.ReadLine();
        await serverWh.Close();
        return;
    }

    await Task.Delay(500);
}

// ----------------------------------------------------------------
// CLIENT SIDE: sweep over A values
// ----------------------------------------------------------------
Console.WriteLine($"[Client-T3+] resources={resources} timeout={timeoutMs}ms rounds={rounds}");
Console.WriteLine($"[Client-T3+] A values: {string.Join(",", aValues)}");

var summary = new List<ASummary>();
bool failureDetected = false;

foreach (int A in aValues)
{
    if (failureDetected)
    {
        Console.WriteLine($"\n[Client-T3+] A={A}: SKIPPED (failure at lower A)");
        continue;
    }

    Console.WriteLine($"\n[Client-T3+] === A={A} ===");
    var roundResults = new List<RoundResult>();

    for (int round = 0; round < rounds; round++)
    {
        var rng = new Random(round * 1000 + A);
        var targets = Enumerable.Range(0, A)
            .Select(_ => rng.Next(resources))
            .ToArray();

        long succeeded = 0, failed = 0, timedOut = 0;
        var latencies = new double[A];
        var roundSw = Stopwatch.StartNew();

        // One shared connection per round, matching the existing test methodology
        var connection = await clientWh.Get<EpConnection>($"ep://{host}:{port}");

        var tasks = targets.Select((resourceIdx, taskIdx) => Task.Run(async () =>
        {
            var sw = Stopwatch.StartNew();
            using var cts = new CancellationTokenSource(timeoutMs);
            try
            {
                var proxy = await connection.Get($"sys/sensor_{resourceIdx}");
                sw.Stop();
                latencies[taskIdx] = sw.Elapsed.TotalMilliseconds;
                if (proxy != null) Interlocked.Increment(ref succeeded);
                else Interlocked.Increment(ref failed);
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                latencies[taskIdx] = timeoutMs;
                Interlocked.Increment(ref timedOut);
            }
            catch
            {
                sw.Stop();
                latencies[taskIdx] = sw.Elapsed.TotalMilliseconds;
                Interlocked.Increment(ref failed);
            }
        })).ToArray();

        await Task.WhenAll(tasks);
        roundSw.Stop();

        var sorted = latencies.OrderBy(x => x).ToArray();
        int n = sorted.Length;

        var rr = new RoundResult
        {
            Round = round + 1,
            A = A,
            Succeeded = succeeded,
            Failed = failed,
            TimedOut = timedOut,
            WallMs = roundSw.Elapsed.TotalMilliseconds,
            P50 = sorted[Math.Min(n - 1, (int)(n * 0.50))],
            P95 = sorted[Math.Min(n - 1, (int)(n * 0.95))],
            P99 = sorted[Math.Min(n - 1, (int)(n * 0.99))],
            Max = sorted[n - 1],
        };
        roundResults.Add(rr);

        Console.WriteLine($"  round {round + 1}/{rounds}: ok={succeeded}/{A} fail={failed} "
                        + $"timeout={timedOut} wall={rr.WallMs:F0}ms p50={rr.P50:F0} p99={rr.P99:F0}");

        // Round 1 of each A is conventionally excluded from latency
        // aggregation due to connection-establishment overhead (matches
        // the existing test methodology).

        GC.Collect();
        await Task.Delay(500);
    }

    var steady = roundResults.Skip(1).ToList(); // exclude round 1
    if (steady.Count == 0) steady = roundResults;

    var anyFailure = roundResults.Any(r => r.Failed > 0 || r.TimedOut > 0);

    var s = new ASummary
    {
        A = A,
        Rounds = roundResults.Count,
        AnyFailures = anyFailure,
        TotalSucceeded = roundResults.Sum(r => r.Succeeded),
        TotalFailed = roundResults.Sum(r => r.Failed),
        TotalTimedOut = roundResults.Sum(r => r.TimedOut),
        MeanP50 = steady.Average(r => r.P50),
        Ci95P50 = ConfidenceIntervalHalfWidth95(steady.Select(r => r.P50).ToArray()),
        MeanP99 = steady.Average(r => r.P99),
        Ci95P99 = ConfidenceIntervalHalfWidth95(steady.Select(r => r.P99).ToArray()),
        MeanWall = steady.Average(r => r.WallMs),
        Ci95Wall = ConfidenceIntervalHalfWidth95(steady.Select(r => r.WallMs).ToArray()),
    };
    summary.Add(s);

    Console.WriteLine($"  [A={A}] SUMMARY: "
                    + $"p50={s.MeanP50:F1}±{s.Ci95P50:F1} "
                    + $"p99={s.MeanP99:F1}±{s.Ci95P99:F1} "
                    + $"wall={s.MeanWall:F0}±{s.Ci95Wall:F0}ms "
                    + $"failures={s.TotalFailed + s.TotalTimedOut}/{s.TotalSucceeded + s.TotalFailed + s.TotalTimedOut}");

    if (anyFailure)
    {
        Console.WriteLine($"  [A={A}] *** FAILURE DETECTED: stopping sweep ***");
        failureDetected = true;
    }
}

// ----------------------------------------------------------------
// CSV output
// ----------------------------------------------------------------
var sb = new System.Text.StringBuilder();
sb.AppendLine("a,rounds,any_failures,total_succeeded,total_failed,total_timed_out," +
              "mean_p50_ms,ci95_p50,mean_p99_ms,ci95_p99,mean_wall_ms,ci95_wall");
foreach (var r in summary)
{
    sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
        $"{r.A},{r.Rounds},{r.AnyFailures},{r.TotalSucceeded},{r.TotalFailed},{r.TotalTimedOut}," +
        $"{r.MeanP50:F2},{r.Ci95P50:F2},{r.MeanP99:F2},{r.Ci95P99:F2}," +
        $"{r.MeanWall:F2},{r.Ci95Wall:F2}"));
}
await File.WriteAllTextAsync(outCsv, sb.ToString());
Console.WriteLine($"\n[Client-T3+] Results written to {outCsv}");

if (mode == "server" || mode == "both") await serverWh.Close();
if (mode == "client" || mode == "both") await clientWh.Close();


// ----------------------------------------------------------------
// Helpers
// ----------------------------------------------------------------
static double ConfidenceIntervalHalfWidth95(double[] xs)
{
    int n = xs.Length;
    if (n < 2) return 0;
    double mean = xs.Average();
    double sumSq = xs.Sum(x => (x - mean) * (x - mean));
    double std = Math.Sqrt(sumSq / (n - 1));
    double sem = std / Math.Sqrt(n);
    double t = (n - 1) switch
    {
        1 => 12.706,
        2 => 4.303,
        3 => 3.182,
        4 => 2.776,
        5 => 2.571,
        6 => 2.447,
        7 => 2.365,
        8 => 2.306,
        9 => 2.262,
        _ => 1.960
    };
    return t * sem;
}

static string GetArg(string[] args, string key, string def)
{
    int i = Array.IndexOf(args, key);
    return (i >= 0 && i + 1 < args.Length) ? args[i + 1] : def;
}

record RoundResult
{
    public int Round;
    public int A;
    public long Succeeded;
    public long Failed;
    public long TimedOut;
    public double WallMs;
    public double P50;
    public double P95;
    public double P99;
    public double Max;
}

record ASummary
{
    public int A;
    public int Rounds;
    public bool AnyFailures;
    public long TotalSucceeded;
    public long TotalFailed;
    public long TotalTimedOut;
    public double MeanP50;
    public double Ci95P50;
    public double MeanP99;
    public double Ci95P99;
    public double MeanWall;
    public double Ci95Wall;
}