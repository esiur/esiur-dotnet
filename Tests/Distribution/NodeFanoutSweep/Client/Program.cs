// ============================================================
// Scalability Extension: Fan-Out — ORCHESTRATOR CLIENT
// ------------------------------------------------------------
// Drives a full sweep of subscriber counts N against a single
// server instance. For each N value:
//   1. Spawns N in-process subscriber tasks, each opening its
//      own EpConnection to the server.
//   2. Each subscriber attaches to all M resources and counts
//      property-change notifications it receives over a fixed
//      measurement window.
//   3. The orchestrator polls the server's sys/control resource
//      to capture server-side CPU during the window.
//   4. Tears down all N subscribers and waits a settle interval
//      before the next sweep point.
//   5. Repeats for `replications` rounds so the per-N mean and
//      95% confidence interval can be computed.
//   6. Auto-stops the sweep if either:
//        - mean per-subscriber rate drops below 10% of theoretical,
//        - or server CPU stays at >180% (>90% of 2 cores) for the
//          entire measurement window.
//
// Note on in-process vs separate processes: subscribers are
// tasks within a single client process to keep the test self-
// contained and avoid spawning N OS processes. Each task uses
// its own EpConnection (TCP connection) to the server, so from
// the server's perspective the load looks identical to N
// distinct subscriber nodes for the property-propagation path.
// The single-client-process design does mean that the client
// host's CPU is shared across all subscribers; the orchestrator
// records this too so degradation can be attributed correctly.
// ------------------------------------------------------------
// Usage:
//   dotnet run -- --host 127.0.0.1 --port 10900 --resources 100 \
//                 --emit-interval-ms 50 --window-sec 60 \
//                 --warmup-sec 5 --replications 3 \
//                 --n-values 2,5,10,20,50,100,200,500
// ============================================================

using Esiur.Protocol;
using Esiur.Resource;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;

var host = GetArg(args, "--host", "127.0.0.1");
var port = int.Parse(GetArg(args, "--port", "10900"));
var resources = int.Parse(GetArg(args, "--resources", "100"));
var emitIntervalMs = int.Parse(GetArg(args, "--emit-interval-ms", "50"));
var windowSec = int.Parse(GetArg(args, "--window-sec", "60"));
var warmupSec = int.Parse(GetArg(args, "--warmup-sec", "5"));
var settleSec = int.Parse(GetArg(args, "--settle-sec", "5"));
var replications = int.Parse(GetArg(args, "--replications", "3"));
var nValuesStr = GetArg(args, "--n-values", "2,5,10,20,50,100,200,500");
var outputCsv = GetArg(args, "--output", "fanout_sweep_results.csv");

var nValues = nValuesStr.Split(',').Select(int.Parse).ToArray();
double theoreticalMaxRate = 1000.0 / emitIntervalMs * resources;
double minAcceptableRate = theoreticalMaxRate * 0.10;

Console.WriteLine($"[Orchestrator] resources={resources} interval={emitIntervalMs}ms "
                + $"window={windowSec}s replications={replications}");
Console.WriteLine($"[Orchestrator] theoretical_max_per_subscriber_rate={theoreticalMaxRate:F0} notif/s");
Console.WriteLine($"[Orchestrator] saturation_threshold={minAcceptableRate:F0} notif/s");
Console.WriteLine($"[Orchestrator] N values: {string.Join(",", nValues)}");

// ----------------------------------------------------------------
// Attach to the server's control resource once.
// ----------------------------------------------------------------
var controlWh = new Warehouse();
EpResource? control = null;
byte cpuIdx = 255, clientsIdx = 255;
try
{
    var controlConn = await controlWh.Get<EpConnection>($"ep://{host}:{port}");
    control = (EpResource)await controlConn.Get("sys/control");
    // Resolve property indices by name (EpResource exposes values by index, not dynamic member).
    var props = control.Instance.Definition.Properties;
    cpuIdx = (byte)Array.FindIndex(props, p => p.Name == "CpuPercent");
    clientsIdx = (byte)Array.FindIndex(props, p => p.Name == "ConnectedClients");
    Console.WriteLine($"[Orchestrator] sys/control attached (CpuPercent=idx {cpuIdx}, ConnectedClients=idx {clientsIdx}).");
}
catch (Exception ex)
{
    Console.WriteLine($"[Orchestrator] WARNING: could not attach to sys/control: {ex.Message}");
    Console.WriteLine("[Orchestrator] Server CPU will be reported as N/A.");
}

// ----------------------------------------------------------------
// All sweep points x replications, with per-N early-stop logic.
// ----------------------------------------------------------------
var allResults = new List<SweepResult>();
bool saturatedDetected = false;

foreach (int n in nValues)
{
    if (saturatedDetected)
    {
        Console.WriteLine($"\n[Orchestrator] N={n}: SKIPPED (saturation reached at lower N)");
        continue;
    }

    var perRepResults = new List<RepResult>();

    for (int rep = 0; rep < replications; rep++)
    {
        Console.WriteLine($"\n[Orchestrator] === N={n} rep={rep + 1}/{replications} ===");

        var subscribers = new SubscriberTask[n];
        var subscriberWhs = new Warehouse[n];

        // ---------- spawn N subscribers ----------
        Console.WriteLine($"[Orchestrator] Spawning {n} subscribers...");
        var spawnSw = Stopwatch.StartNew();
        var spawnTasks = new Task<SubscriberTask?>[n];
        for (int i = 0; i < n; i++)
        {
            int captured = i;
            subscriberWhs[i] = new Warehouse();
            spawnTasks[i] = SpawnSubscriber(subscriberWhs[i], host, port, resources, captured);
        }

        await Task.WhenAll(spawnTasks);

        bool spawnFailed = false;
        for (int i = 0; i < n; i++)
        {
            if (spawnTasks[i].Result == null)
                spawnFailed = true;
            else
                subscribers[i] = spawnTasks[i].Result!;
        }
        spawnSw.Stop();

        if (spawnFailed)
        {
            Console.WriteLine($"[Orchestrator] N={n}: spawn failed; treating as saturation.");
            saturatedDetected = true;
            await TeardownAll(subscribers, subscriberWhs);
            break;
        }
        Console.WriteLine($"[Orchestrator] All {n} subscribers attached in {spawnSw.Elapsed.TotalSeconds:F2}s");

        // ---------- warmup ----------
        Console.WriteLine($"[Orchestrator] Warmup {warmupSec}s...");
        await Task.Delay(warmupSec * 1000);
        foreach (var s in subscribers) s.ResetCounters();

        // ---------- measurement window with CPU sampling ----------
        Console.WriteLine($"[Orchestrator] Measurement window {windowSec}s...");
        var cpuSamples = new List<double>();
        var connSamples = new List<int>();
        var clientCpuSamples = new List<double>();
        var clientProc = Process.GetCurrentProcess();
        var prevClientCpu = clientProc.TotalProcessorTime;
        var prevClientWall = DateTime.UtcNow;
        var winSw = Stopwatch.StartNew();
        while (winSw.Elapsed.TotalSeconds < windowSec)
        {
            await Task.Delay(1000);

            // Server CPU + subscriber count via the control resource (read by property index;
            // values arrive as variable-width numerics, hence Convert.*).
            if (control != null && cpuIdx != 255)
            {
                try
                {
                    if (control.TryGetPropertyValue(cpuIdx, out var cpuVal) && cpuVal != null)
                        cpuSamples.Add(Convert.ToDouble(cpuVal));
                    if (control.TryGetPropertyValue(clientsIdx, out var cliVal) && cliVal != null)
                        connSamples.Add(Convert.ToInt32(cliVal));
                }
                catch { /* control resource may not have a current value yet */ }
            }

            // This harness's own CPU (% across all cores). Recorded so saturation can be attributed
            // to the server rather than to the single subscriber process driving N connections.
            clientProc.Refresh();
            var nowClientCpu = clientProc.TotalProcessorTime;
            var nowClientWall = DateTime.UtcNow;
            var wallMs = (nowClientWall - prevClientWall).TotalMilliseconds;
            if (wallMs > 0) clientCpuSamples.Add((nowClientCpu - prevClientCpu).TotalMilliseconds / wallMs * 100.0);
            prevClientCpu = nowClientCpu;
            prevClientWall = nowClientWall;
        }

        double elapsedSec = winSw.Elapsed.TotalSeconds;

        // ---------- collect per-subscriber counts ----------
        var perSubRates = new double[n];
        long totalReceived = 0;
        long totalLate = 0;
        for (int i = 0; i < n; i++)
        {
            perSubRates[i] = subscribers[i].Received / elapsedSec;
            totalReceived += subscribers[i].Received;
            totalLate += subscribers[i].LateDeliveries;
        }

        double meanPerSub = perSubRates.Average();
        double stdPerSub = StdDev(perSubRates);
        double minPerSub = perSubRates.Min();
        double maxPerSub = perSubRates.Max();
        double aggregate = perSubRates.Sum();
        double avgServerCpu = cpuSamples.Count > 0 ? cpuSamples.Average() : double.NaN;
        double peakServerCpu = cpuSamples.Count > 0 ? cpuSamples.Max() : double.NaN;
        double avgClientCpu = clientCpuSamples.Count > 0 ? clientCpuSamples.Average() : double.NaN;
        double peakClientCpu = clientCpuSamples.Count > 0 ? clientCpuSamples.Max() : double.NaN;

        Console.WriteLine($"[Orchestrator] N={n} rep={rep + 1}: "
                        + $"mean_per_sub={meanPerSub:F1}/s "
                        + $"aggregate={aggregate:F0}/s "
                        + $"late={totalLate} "
                        + $"server_cpu_avg={avgServerCpu:F1}%/peak={peakServerCpu:F1}% "
                        + $"client_cpu_avg={avgClientCpu:F1}%/peak={peakClientCpu:F1}%");

        perRepResults.Add(new RepResult
        {
            N = n,
            Rep = rep + 1,
            MeanPerSub = meanPerSub,
            StdPerSub = stdPerSub,
            MinPerSub = minPerSub,
            MaxPerSub = maxPerSub,
            Aggregate = aggregate,
            LateDeliveries = totalLate,
            ServerCpuAvg = avgServerCpu,
            ServerCpuPeak = peakServerCpu,
            ClientCpuAvg = avgClientCpu,
            ClientCpuPeak = peakClientCpu,
        });

        // ---------- teardown ----------
        Console.WriteLine($"[Orchestrator] Tearing down {n} subscribers...");
        await TeardownAll(subscribers, subscriberWhs);
        await Task.Delay(settleSec * 1000);
    }

    // ---------- per-N aggregation ----------
    if (perRepResults.Count > 0)
    {
        double meanOfMeans = perRepResults.Average(r => r.MeanPerSub);
        double ciHalfWidth = ConfidenceIntervalHalfWidth95(
            perRepResults.Select(r => r.MeanPerSub).ToArray());

        Console.WriteLine($"\n[Orchestrator] N={n} SUMMARY: "
                        + $"mean_per_sub={meanOfMeans:F1} ± {ciHalfWidth:F1} notif/s (95% CI)");

        // Saturation detection: stop sweep if per-sub rate falls below
        // 10% of theoretical OR server CPU peaked above 180% (>90% of 2 cores)
        if (meanOfMeans < minAcceptableRate)
        {
            Console.WriteLine($"[Orchestrator] *** SATURATION DETECTED: rate {meanOfMeans:F0} < {minAcceptableRate:F0} ***");
            saturatedDetected = true;
        }
        else if (perRepResults.Average(r => r.ServerCpuPeak) > 180.0)
        {
            Console.WriteLine($"[Orchestrator] *** SATURATION DETECTED: server CPU peaked > 180% ***");
            saturatedDetected = true;
        }

        // Aggregate row for CSV
        allResults.Add(new SweepResult
        {
            N = n,
            Replications = perRepResults.Count,
            MeanPerSubRate = meanOfMeans,
            Ci95HalfWidth = ciHalfWidth,
            MeanAggregate = perRepResults.Average(r => r.Aggregate),
            TotalLate = perRepResults.Sum(r => r.LateDeliveries),
            MeanServerCpuAvg = perRepResults.Average(r => r.ServerCpuAvg),
            MeanServerCpuPeak = perRepResults.Average(r => r.ServerCpuPeak),
            MeanClientCpuAvg = perRepResults.Average(r => r.ClientCpuAvg),
            MeanClientCpuPeak = perRepResults.Average(r => r.ClientCpuPeak),
        });
    }
}

// ----------------------------------------------------------------
// Output
// ----------------------------------------------------------------
var sb = new System.Text.StringBuilder();
sb.AppendLine("n,replications,mean_per_sub_rate,ci95_halfwidth,mean_aggregate," +
              "total_late,mean_server_cpu_avg,mean_server_cpu_peak,mean_client_cpu_avg,mean_client_cpu_peak");
foreach (var r in allResults)
{
    sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
        $"{r.N},{r.Replications},{r.MeanPerSubRate:F2},{r.Ci95HalfWidth:F2}," +
        $"{r.MeanAggregate:F1},{r.TotalLate},{r.MeanServerCpuAvg:F2},{r.MeanServerCpuPeak:F2}," +
        $"{r.MeanClientCpuAvg:F2},{r.MeanClientCpuPeak:F2}"));
}
await File.WriteAllTextAsync(outputCsv, sb.ToString());
Console.WriteLine($"\n[Orchestrator] Results written to {outputCsv}");

// ----------------------------------------------------------------
// Subscriber spawn / teardown
// ----------------------------------------------------------------
static async Task<SubscriberTask?> SpawnSubscriber(
    Warehouse wh, string host, int port, int resources, int subId)
{
    try
    {
        var conn = await wh.Get<EpConnection>($"ep://{host}:{port}");
        var sub = new SubscriberTask { SubscriberId = subId, Connection = conn };

        for (int i = 0; i < resources; i++)
        {
            var proxy = await conn.Get($"sys/sensor_{i}");
            sub.Resources.Add(proxy);
            long lastTick = Stopwatch.GetTimestamp();

            proxy.Instance.PropertyModified += (PropertyModificationInfo data) =>
            {
                if (data.Name != "Value") return;
                long now = Stopwatch.GetTimestamp();
                double elapsedMs = (now - lastTick) * 1000.0 / Stopwatch.Frequency;
                lastTick = now;
                Interlocked.Increment(ref sub._received);
                if (elapsedMs > 400) Interlocked.Increment(ref sub._lateDeliveries);
            };
        }

        return sub;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  [Spawn-{subId}] FAILED: {ex.Message}");
        return null;
    }
}

static async Task TeardownAll(SubscriberTask[] subscribers, Warehouse[] whs)
{
    foreach (var subscriber in subscribers)
    {
        if (subscriber == null)
            continue;

        try { subscriber.Connection?.Close(); }
        catch { /* ignore */ }

        subscriber.Connection = null;
        subscriber.Resources.Clear();
    }

    foreach (var wh in whs)
    {
        try { await wh.Close(); }
        catch { /* ignore */ }
    }
}

// ----------------------------------------------------------------
// Stats helpers
// ----------------------------------------------------------------
static double StdDev(double[] xs)
{
    if (xs.Length < 2) return 0;
    double mean = xs.Average();
    double sumSq = xs.Sum(x => (x - mean) * (x - mean));
    return Math.Sqrt(sumSq / (xs.Length - 1));
}

/// <summary>
/// 95% confidence interval half-width using Student's t-distribution.
/// For very small samples (n &lt; 3) returns 0 (not enough data).
/// t values for 95% two-sided are hard-coded; see standard tables.
/// </summary>
static double ConfidenceIntervalHalfWidth95(double[] xs)
{
    int n = xs.Length;
    if (n < 2) return 0;
    double std = StdDev(xs);
    double sem = std / Math.Sqrt(n);
    // t for df=n-1, two-sided 95%
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
        _ => 1.960  // normal approximation
    };
    return t * sem;
}

static string GetArg(string[] args, string key, string def)
{
    int i = Array.IndexOf(args, key);
    return (i >= 0 && i + 1 < args.Length) ? args[i + 1] : def;
}

// ----------------------------------------------------------------
// Records
// ----------------------------------------------------------------
class SubscriberTask
{
    public int SubscriberId;
    public EpConnection? Connection;
    public readonly List<IResource> Resources = new();
    internal long _received;
    internal long _lateDeliveries;
    public long Received => Interlocked.Read(ref _received);
    public long LateDeliveries => Interlocked.Read(ref _lateDeliveries);
    public void ResetCounters()
    {
        Interlocked.Exchange(ref _received, 0);
        Interlocked.Exchange(ref _lateDeliveries, 0);
    }
}

record RepResult
{
    public int N;
    public int Rep;
    public double MeanPerSub;
    public double StdPerSub;
    public double MinPerSub;
    public double MaxPerSub;
    public double Aggregate;
    public long LateDeliveries;
    public double ServerCpuAvg;
    public double ServerCpuPeak;
    public double ClientCpuAvg;
    public double ClientCpuPeak;
}

record SweepResult
{
    public int N;
    public int Replications;
    public double MeanPerSubRate;
    public double Ci95HalfWidth;
    public double MeanAggregate;
    public long TotalLate;
    public double MeanServerCpuAvg;
    public double MeanServerCpuPeak;
    public double MeanClientCpuAvg;
    public double MeanClientCpuPeak;
}
