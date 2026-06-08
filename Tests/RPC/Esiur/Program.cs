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

if (args.Count() > 0)
    port = ushort.Parse(args[0]);

Console.WriteLine($"Esiur server listening on port {port}...");

var wh = Warehouse.Default;
var mem = await wh.Put("sys", new MemoryStore());
var service = await wh.Put("sys/service", new Service());
var ds = await wh.Put("sys/server", new EpServer() { Port = port, EntryPoint = service, 
    AllowUnauthorizedAccess = true });

await wh.Open();


Console.WriteLine("Open");

if (!Directory.Exists("template"))
    Directory.CreateDirectory("template");

TypeDefGenerator.GetTypes("ep://localhost:5005/sys/service", "template");