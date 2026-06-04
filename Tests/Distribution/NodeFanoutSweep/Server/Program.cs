// ============================================================
// Scalability Extension: Fan-Out — SERVER NODE
// Hosts M sensor resources and emits Value updates at a fixed interval (the fan-out source). Also
// hosts sys/control, updated once per second with the server process CPU (% across all cores) and
// the live subscriber count, which the sweep orchestrator reads to characterise saturation.
// Anonymous (None-mode) access so subscribers connect without credentials.
//
// Usage: dotnet run -- --port 10900 --resources 100 --emit-interval-ms 50
// (Run the orchestrator from the sibling "Server" project against this host:port.)
// ============================================================

using Esiur.Protocol;
using Esiur.Resource;
using Esiur.Stores;
using System.Diagnostics;

var port           = int.Parse(GetArg(args, "--port", "10900"));
var resources      = int.Parse(GetArg(args, "--resources", "100"));
var emitIntervalMs = int.Parse(GetArg(args, "--emit-interval-ms", "50"));

Console.WriteLine($"[Server] resources={resources} emit-interval={emitIntervalMs}ms port={port} cores={Environment.ProcessorCount}");

var wh = new Warehouse();
await wh.Put("sys", new MemoryStore());
var server = await wh.Put("sys/server", new EpServer { Port = (ushort)port, AllowUnauthorizedAccess = true });

var sensors = new SensorResource[resources];
for (var i = 0; i < resources; i++) { sensors[i] = new SensorResource { SensorId = i }; await wh.Put($"sys/sensor_{i}", sensors[i]); }

var control = new ControlResource();
await wh.Put("sys/control", control);

await wh.Open();
Console.WriteLine($"[Server] Listening on port {port} with {resources} sensors + sys/control. Press Ctrl+C to stop.");

// Emit loop: drives property-change notifications to every attached subscriber.
var sw = Stopwatch.StartNew();
_ = Task.Run(async () =>
{
    while (true)
    {
        await Task.Delay(emitIntervalMs);
        var value = sw.Elapsed.TotalSeconds;
        foreach (var s in sensors) s.Value = value;
    }
});

// Telemetry loop: publish server CPU (% across all cores) and subscriber count once per second.
_ = Task.Run(async () =>
{
    var proc = Process.GetCurrentProcess();
    var prevCpu = proc.TotalProcessorTime;
    var prevWall = DateTime.UtcNow;
    while (true)
    {
        await Task.Delay(1000);
        proc.Refresh();
        var nowCpu = proc.TotalProcessorTime;
        var nowWall = DateTime.UtcNow;
        var wallMs = (nowWall - prevWall).TotalMilliseconds;
        control.CpuPercent = wallMs > 0 ? (nowCpu - prevCpu).TotalMilliseconds / wallMs * 100.0 : 0;
        control.ConnectedClients = server.Connections.Count;
        prevCpu = nowCpu;
        prevWall = nowWall;
    }
});

var stop = new TaskCompletionSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.TrySetResult(); };
await stop.Task;
await wh.Close();

static string GetArg(string[] args, string key, string def)
{
    var i = Array.IndexOf(args, key);
    return (i >= 0 && i + 1 < args.Length) ? args[i + 1] : def;
}
