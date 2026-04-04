// ============================================================
// Test 2: Resource Count Scalability — SERVER NODE
// Hosts an increasing number of resources to test warehouse
// lookup overhead and memory footprint as the store grows.
//
// Usage: dotnet run -- --resources 10000 --port 10901
// ============================================================

using Esiur.Resource;
using Esiur.Stores;
using Esiur.Net.IIP;

var resourceCount = int.Parse(GetArg(args, "--resources", "10000"));
var port          = int.Parse(GetArg(args, "--port",      "10901"));

Console.WriteLine($"[Server-T2] Creating {resourceCount} resources on port {port}");

await Warehouse.Put("sys", new MemoryStore());
await Warehouse.Put("sys/server", new DistributedServer() { Port = (ushort)port });

long memBefore = GC.GetTotalMemory(forceFullCollection: true);

for (int i = 0; i < resourceCount; i++)
{
    var s = new SensorResource { SensorId = i, Value = i * 0.1 };
    await Warehouse.Put($"sys/sensor_{i}", s);
}

await Warehouse.Open();

long memAfter = GC.GetTotalMemory(forceFullCollection: true);
double memMB = (memAfter - memBefore) / (1024.0 * 1024.0);

Console.WriteLine($"[Server-T2] Ready. Resources={resourceCount}  MemoryUsed={memMB:F1} MB");
Console.WriteLine($"[Server-T2] Per-resource ≈ {(memAfter - memBefore) / (double)resourceCount:F0} bytes");
Console.WriteLine("Press ENTER to stop.");
Console.ReadLine();
await Warehouse.Close();


static string GetArg(string[] args, string key, string def)
{
    int i = Array.IndexOf(args, key);
    return (i >= 0 && i + 1 < args.Length) ? args[i + 1] : def;
}
