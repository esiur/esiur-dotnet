// See https://aka.ms/new-console-template for more information
using Esiur.Data;
using Esiur.Protocol;
using Esiur.Proxy;
using Esiur.Resource;
using Esiur.Stores;
using Esiur.Tests.RPC.EsiurServer;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

ushort port = 5005;

var portArg = args.FirstOrDefault(x => ushort.TryParse(x, out _));
if (portArg != null)
    port = ushort.Parse(portArg);

Console.WriteLine($"Esiur server listening on port {port}...");

var wh = Warehouse.Default;
var mem = await wh.Put("sys", new MemoryStore());
var service = await wh.Put("sys/service", new Service());
var ds = await wh.Put("sys/server", new EpServer() { Port = port, EntryPoint = service, 
    AllowUnauthorizedAccess = true });

await wh.Open();


Console.WriteLine("Open");

if (args.Contains("--generate-client"))
{
    if (!Directory.Exists("template"))
        Directory.CreateDirectory("template");

    TypeDefGenerator.GetTypes($"ep://localhost:{port}/sys/service", "template");
}

await Task.Delay(Timeout.Infinite);
