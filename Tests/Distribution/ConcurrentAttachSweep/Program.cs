// ============================================================
// Scalability Extension: Concurrent Attach Sweep
// ------------------------------------------------------------
// Extends Tests/Distribution/ConcurrentAttach with:
//   - Sweep over a wider range of concurrent request counts A.
//   - More rounds per A for sample-standard-deviation reporting.
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
var roundsCsv = GetArg(args, "--round-output", Path.ChangeExtension(outCsv, ".rounds.csv"));
var aValues = aValStr.Split(',').Select(int.Parse).ToArray();

var serverWh = new Warehouse();

// ----------------------------------------------------------------
// SERVER SIDE
// ----------------------------------------------------------------
if (mode == "server" || mode == "both")
{
    await serverWh.Put("sys", new MemoryStore());
    await serverWh.Put("sys/server", new EpServer() { Port = (ushort)port, AllowUnauthorizedAccess = true });

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
Console.WriteLine($"[Client-T3+] output={outCsv}");
Console.WriteLine($"[Client-T3+] round-output={roundsCsv}");

var summary = new List<ASummary>();
var allRoundResults = new List<RoundResult>();
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

        // A fresh client warehouse per round keeps the ten samples independent:
        // previous attaches cannot turn later rounds into local cache hits.
        var clientWh = new Warehouse();
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
            P50 = Quantile(sorted, 0.50),
            P90 = Quantile(sorted, 0.90),
            P95 = Quantile(sorted, 0.95),
            P99 = Quantile(sorted, 0.99),
            Max = sorted[n - 1],
            Mean = sorted.Average(),
        };
        roundResults.Add(rr);
        allRoundResults.Add(rr);

        Console.WriteLine($"  round {round + 1}/{rounds}: ok={succeeded}/{A} fail={failed} "
                        + $"timeout={timedOut} wall={rr.WallMs:F0}ms p50={rr.P50:F0} p90={rr.P90:F0} p99={rr.P99:F0}");

        await clientWh.Close();
        GC.Collect();
        await Task.Delay(500);
    }

    var anyFailure = roundResults.Any(r => r.Failed > 0 || r.TimedOut > 0);

    var p50 = MeanStdDev(roundResults.Select(r => r.P50));
    var p90 = MeanStdDev(roundResults.Select(r => r.P90));
    var p99 = MeanStdDev(roundResults.Select(r => r.P99));
    var mean = MeanStdDev(roundResults.Select(r => r.Mean));
    var wall = MeanStdDev(roundResults.Select(r => r.WallMs));

    var s = new ASummary
    {
        A = A,
        Rounds = roundResults.Count,
        AnyFailures = anyFailure,
        TotalSucceeded = roundResults.Sum(r => r.Succeeded),
        TotalFailed = roundResults.Sum(r => r.Failed),
        TotalTimedOut = roundResults.Sum(r => r.TimedOut),
        MeanP50 = p50.Mean,
        SdP50 = p50.SampleStdDev,
        MeanP90 = p90.Mean,
        SdP90 = p90.SampleStdDev,
        MeanP99 = p99.Mean,
        SdP99 = p99.SampleStdDev,
        MeanLatency = mean.Mean,
        SdLatency = mean.SampleStdDev,
        MeanWall = wall.Mean,
        SdWall = wall.SampleStdDev,
    };
    summary.Add(s);

    Console.WriteLine($"  [A={A}] SUMMARY: "
                    + $"p50={s.MeanP50:F1}±{s.SdP50:F1} "
                    + $"p90={s.MeanP90:F1}±{s.SdP90:F1} "
                    + $"p99={s.MeanP99:F1}±{s.SdP99:F1} "
                    + $"mean={s.MeanLatency:F1}±{s.SdLatency:F1} "
                    + $"wall={s.MeanWall:F0}±{s.SdWall:F0}ms "
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
              "mean_p50_ms,sd_p50_ms,mean_p90_ms,sd_p90_ms,mean_p99_ms,sd_p99_ms," +
              "mean_latency_ms,sd_latency_ms,mean_wall_ms,sd_wall_ms");
foreach (var r in summary)
{
    sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
        $"{r.A},{r.Rounds},{r.AnyFailures},{r.TotalSucceeded},{r.TotalFailed},{r.TotalTimedOut}," +
        $"{r.MeanP50:F2},{r.SdP50:F2},{r.MeanP90:F2},{r.SdP90:F2}," +
        $"{r.MeanP99:F2},{r.SdP99:F2},{r.MeanLatency:F2},{r.SdLatency:F2}," +
        $"{r.MeanWall:F2},{r.SdWall:F2}"));
}
await File.WriteAllTextAsync(outCsv, sb.ToString());
Console.WriteLine($"\n[Client-T3+] Results written to {outCsv}");

var rsb = new System.Text.StringBuilder();
rsb.AppendLine("a,round,succeeded,failed,timed_out,wall_ms,p50_ms,p90_ms,p95_ms,p99_ms,max_ms,mean_ms");
foreach (var r in allRoundResults)
{
    rsb.AppendLine(string.Create(CultureInfo.InvariantCulture,
        $"{r.A},{r.Round},{r.Succeeded},{r.Failed},{r.TimedOut}," +
        $"{r.WallMs:F2},{r.P50:F2},{r.P90:F2},{r.P95:F2},{r.P99:F2},{r.Max:F2},{r.Mean:F2}"));
}
await File.WriteAllTextAsync(roundsCsv, rsb.ToString());
Console.WriteLine($"[Client-T3+] Round results written to {roundsCsv}");

if (mode == "server" || mode == "both") await serverWh.Close();


// ----------------------------------------------------------------
// Helpers
// ----------------------------------------------------------------
static double Quantile(double[] sorted, double p)
{
    return sorted[Math.Min(sorted.Length - 1, (int)(sorted.Length * p))];
}

static (double Mean, double SampleStdDev) MeanStdDev(IEnumerable<double> values)
{
    var xs = values.ToArray();
    var mean = xs.Average();

    if (xs.Length < 2)
        return (mean, 0);

    var sumSq = xs.Sum(x =>
    {
        var d = x - mean;
        return d * d;
    });

    return (mean, Math.Sqrt(sumSq / (xs.Length - 1)));
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
    public double P90;
    public double P95;
    public double P99;
    public double Max;
    public double Mean;
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
    public double SdP50;
    public double MeanP90;
    public double SdP90;
    public double MeanP99;
    public double SdP99;
    public double MeanLatency;
    public double SdLatency;
    public double MeanWall;
    public double SdWall;
}
