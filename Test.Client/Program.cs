

using Esiur.Resource;

namespace Test.Client;

public class App
{
    static async Task Main(string[] args)
    {
        
        var remote = await Warehouse.Get<MyService>("iip://localhost/mem/service");
        var (i, s) = await remote.Tuple2(22, "ZZZZ");
        remote.ArrayEvent += (x) => Console.WriteLine(x); 
        remote.StringEvent += (x)=>Console.WriteLine(x);
        await remote.InvokeEvents("Client");

        Console.WriteLine(remote);
    }

 }