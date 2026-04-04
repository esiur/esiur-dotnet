// ============================================================
// Test 1: Node Fan-Out — CLIENT NODE
// Connects to the server, attaches to all sensor resources,
// and counts received property-change notifications.
//
// Run N instances of this client simultaneously to simulate
// N subscriber nodes. The server's fan-out load grows with N.
//
// Usage: dotnet run -- --host 127.0.0.1 --port 10900 --resources 100 --duration 30
// ============================================================

using Esiur.Resource;
using System.Diagnostics;

var host          = GetArg(args, "--host",      "127.0.0.1");
var port          = int.Parse(GetArg(args, "--port",      "10900"));
var resourceCount = int.Parse(GetArg(args, "--resources", "100"));
var durationSec   = int.Parse(GetArg(args, "--duration",  "30"));
var clientId      = GetArg(args, "--id", Environment.MachineName);

Console.WriteLine($"[Client {clientId}] Connecting to {host}:{port}, resources={resourceCount}, duration={durationSec}s");

// Counters
long   totalReceived    = 0;
long   lateCount        = 0;     // notifications arriving > 500ms after the previous
double sumLatencyMs     = 0;
long   latencySamples   = 0;

var latencyLock = new object();

// --- Attach all resources -------------------------------------------
var proxies = new dynamic[resourceCount];
var sw = Stopwatch.StartNew();

var wh = new Warehouse();

try
{
    for (int i = 0; i < resourceCount; i++)
    {
        proxies[i] = await wh.Get<IResource>($"iip://{host}:{port}/sys/sensor_{i}");

        // Subscribe to property change notifications via the Esiur event model
        double lastValue = (double)proxies[i].Value;
        long   lastTick  = Stopwatch.GetTimestamp();
        int    capturedI = i;

        proxies[i].OnPropertyModified += (string propName, object oldVal, object newVal) =>
        {
            if (propName != "Value") return;

            long nowTick = Stopwatch.GetTimestamp();
            double elapsedMs = (nowTick - lastTick) * 1000.0 / Stopwatch.Frequency;
            lastTick = nowTick;

            Interlocked.Increment(ref totalReceived);

            lock (latencyLock)
            {
                sumLatencyMs += elapsedMs;
                latencySamples++;
                if (elapsedMs > 500) lateCount++;
            }
        };
    }

    double attachTime = sw.Elapsed.TotalSeconds;
    Console.WriteLine($"[Client {clientId}] All {resourceCount} resources attached in {attachTime:F2}s");
}
catch (Exception ex)
{
    Console.WriteLine($"[Client {clientId}] Attach error: {ex.Message}");
    return;
}

// --- Measurement window ---------------------------------------------
sw.Restart();
long lastReceived = 0;
var results = new List<(double TimeSec, long ReceivedDelta, double AvgIntervalMs)>();

while (sw.Elapsed.TotalSeconds < durationSec)
{
    await Task.Delay(5000);

    long delta = totalReceived - lastReceived;
    lastReceived = totalReceived;

    double avgInterval;
    lock (latencyLock)
    {
        avgInterval = latencySamples > 0 ? sumLatencyMs / latencySamples : 0;
        sumLatencyMs = 0;
        latencySamples = 0;
    }

    double t = sw.Elapsed.TotalSeconds;
    results.Add((t, delta, avgInterval));
    Console.WriteLine($"[Client {clientId}] t={t:F0}s  recv/5s={delta}  rate={delta/5.0:F1}/s  avg_interval={avgInterval:F1}ms  late={lateCount}");
}

// --- CSV output -----------------------------------------------------
string csv = $"time_s,received_per_5s,rate_per_s,avg_interval_ms\n" +
             string.Join("\n", results.Select(r =>
                $"{r.TimeSec:F1},{r.ReceivedDelta},{r.ReceivedDelta/5.0:F1},{r.AvgIntervalMs:F2}"));

string outFile = $"client_{clientId}_results.csv";
await File.WriteAllTextAsync(outFile, csv);
Console.WriteLine($"[Client {clientId}] Results written to {outFile}");
Console.WriteLine($"[Client {clientId}] Total received={totalReceived}  late(>500ms)={lateCount}");


static string GetArg(string[] args, string key, string def)
{
    int i = Array.IndexOf(args, key);
    return (i >= 0 && i + 1 < args.Length) ? args[i + 1] : def;
}
