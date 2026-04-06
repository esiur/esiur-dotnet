// ============================================================
// Test 2: Resource Count Scalability — CLIENT NODE
// Sequentially attaches to each resource on the server and
// records per-resource attach latency.  Reports p50/p95/p99.
//
// Also tests notification latency after all resources are ready.
//
// Usage: dotnet run -- --host 127.0.0.1 --port 10901 --resources 10000
// ============================================================

using Esiur.Protocol;
using Esiur.Resource;
using System.Diagnostics;
using System.Text.RegularExpressions;

var host = GetArg(args, "--host", "127.0.0.1");
var port = int.Parse(GetArg(args, "--port", "10901"));
var resourceCount = int.Parse(GetArg(args, "--resources", "10000"));
var batchSize = int.Parse(GetArg(args, "--batch", "10000"));

Console.WriteLine($"[Client-T2] Connecting to {host}:{port}, resources={resourceCount}");

var wh = new Warehouse();

var connnection = await wh.Get<EpConnection>(
                $"ep://{host}:{port}");

var attachLatencies = new List<double>(resourceCount);
var proxies = new IResource[resourceCount];

// --- Attach in batches to avoid overwhelming the runtime -------------
var totalSw = Stopwatch.StartNew();

for (int batch = 0; batch < resourceCount; batch += batchSize)
{

    int end = Math.Min(batch + batchSize, resourceCount);
    var batchTasks = new Task[end - batch];

    for (int i = batch; i < end; i++)
    {
        int capturedI = i;
        batchTasks[i - batch] = Task.Run(async () =>
        {
            var sw = Stopwatch.StartNew();

            Console.WriteLine(capturedI);
            proxies[capturedI] = await connnection.Get($"sys/sensor_{capturedI}");

            Console.WriteLine(proxies[capturedI].Instance.Link);

            sw.Stop();

            lock (attachLatencies)
                attachLatencies.Add(sw.Elapsed.TotalMilliseconds);
        });
    }

    await Task.WhenAll(batchTasks);
    //Console.WriteLine("D");
    if (batch % 1000 == 0)
        Console.WriteLine($"[Client-T2] Attached {Math.Min(batch + batchSize, resourceCount)}/{resourceCount}  " +
                          $"elapsed={totalSw.Elapsed.TotalSeconds:F1}s");
}

totalSw.Stop();
Console.WriteLine($"[Client-T2] All attached in {totalSw.Elapsed.TotalSeconds:F2}s");

// --- Latency statistics ---------------------------------------------
attachLatencies.Sort();
int n = attachLatencies.Count;

Console.WriteLine($"[Client-T2] Attach latency (ms):");
Console.WriteLine($"  min={attachLatencies[0]:F2}");
Console.WriteLine($"  p50={attachLatencies[(int)(n * 0.50)]:F2}");
Console.WriteLine($"  p95={attachLatencies[(int)(n * 0.95)]:F2}");
Console.WriteLine($"  p99={attachLatencies[(int)(n * 0.99)]:F2}");
Console.WriteLine($"  max={attachLatencies[n - 1]:F2}");
Console.WriteLine($"  mean={attachLatencies.Average():F2}");

// --- Notification round-trip after full load ------------------------
Console.WriteLine("[Client-T2] Measuring notification latency under full resource load...");
long received = 0;
double sumLatencyMs = 0;

for (int i = 0; i < resourceCount; i++)
{
    int capturedI = i;
    proxies[capturedI].Instance.PropertyModified += (PropertyModificationInfo data) =>
    {
        if (data.Name == "Value")
            Interlocked.Increment(ref received);
    };
}

await connnection.Call("UpdateValues");

await Task.Delay(10000);   // observe for 10s
Console.WriteLine($"[Client-T2] Received {received} notifications in 10s from first 500 resources");

// --- CSV output -----------------------------------------------------
string csv = "attach_latency_ms\n" + string.Join("\n", attachLatencies.Select(l => l.ToString("F3")));
await File.WriteAllTextAsync("test2_attach_latencies.csv", csv);
Console.WriteLine("[Client-T2] Attach latencies written to test2_attach_latencies.csv");


static string GetArg(string[] args, string key, string def)
{
    int i = Array.IndexOf(args, key);
    return (i >= 0 && i + 1 < args.Length) ? args[i + 1] : def;
}
