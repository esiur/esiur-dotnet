// ============================================================
// Test 1: Node Fan-Out — SERVER NODE
// One server hosts N resources. Clients attach and subscribe.
// The server emits property updates at a fixed rate and measures
// notification throughput vs. subscriber count.
// ============================================================
// Usage: dotnet run -- --resources 100 --interval 50
// ============================================================

using Esiur.Resource;
using Esiur.Stores;
 using System.Diagnostics;
using Esiur.Protocol;

var resourceCount = int.Parse(GetArg(args, "--resources", "100"));
var intervalMs    = int.Parse(GetArg(args, "--interval",  "50"));
var port          = int.Parse(GetArg(args, "--port",      "10900"));

Console.WriteLine($"[Server] resources={resourceCount}  interval={intervalMs}ms  port={port}");

var wh = new Warehouse();
// --- Warehouse setup -------------------------------------------------
await wh.Put("sys", new MemoryStore());
await wh.Put("sys/server", new EpServer() { Port = (ushort)port });

// Create and register all sensor resources
var sensors = new SensorResource[resourceCount];
for (int i = 0; i < resourceCount; i++)
{
    sensors[i] = new SensorResource { SensorId = i };
    await wh.Put($"sys/sensor_{i}", sensors[i]);
}

await wh.Open();
Console.WriteLine($"[Server] Listening on port {port} with {resourceCount} resources.");

// --- Emit loop -------------------------------------------------------
// Continuously update all resource properties at the given interval.
// This drives property-change notifications to all attached clients.
long totalEmitted = 0;
var sw = Stopwatch.StartNew();

_ = Task.Run(async () =>
{
    while (true)
    {
        await Task.Delay(intervalMs);

        double value = sw.Elapsed.TotalSeconds;
        foreach (var s in sensors)
            s.Value = value;          // triggers PropertyModified → propagate to peers

        totalEmitted += resourceCount;
    }
});

// --- Stats reporter --------------------------------------------------
_ = Task.Run(async () =>
{
    long lastEmitted = 0;
    while (true)
    {
        await Task.Delay(5000);
        long delta = totalEmitted - lastEmitted;
        lastEmitted = totalEmitted;
        Console.WriteLine($"[Server] {DateTime.Now:HH:mm:ss}  emitted/5s={delta}  rate={delta/5.0:F0}/s");
    }
});

Console.WriteLine("Press ENTER to stop.");
Console.ReadLine();
await wh.Close();


// --- Helpers ---------------------------------------------------------
static string GetArg(string[] args, string key, string def)
{
    int i = Array.IndexOf(args, key);
    return (i >= 0 && i + 1 < args.Length) ? args[i + 1] : def;
}
