// ============================================================
// Test 4: Fork-Join Queueing Test — SERVER NODE//
// Usage: dotnet run -- --port 10901
// ============================================================

using Esiur.Resource;
using Esiur.Stores;
using Esiur.Protocol;
using Esiur.Tests.Queueing.Server;

var port          = int.Parse(GetArg(args, "--port",      "10901"));


Console.WriteLine($"[Server] Listening on port {port}...");

var wh = Warehouse.Default;
var mem = await wh.Put("sys", new MemoryStore());
var service = await wh.Put("sys/queueing", new QueueingService());
var server = await wh.Put("sys/server", new EpServer() { Port = (ushort)port, 
                                                EntryPoint = service });


long memBefore = GC.GetTotalMemory(forceFullCollection: true);

await wh.Open();


long memAfter = GC.GetTotalMemory(forceFullCollection: true);
double memMB = (memAfter - memBefore) / (1024.0 * 1024.0);

Console.WriteLine("Press ENTER to stop.");
Console.ReadLine();
await wh.Close();


static string GetArg(string[] args, string key, string def)
{
    int i = Array.IndexOf(args, key);
    return (i >= 0 && i + 1 < args.Length) ? args[i + 1] : def;
}
