
using Esiur.Net.IIP;
using Esiur.Resource;
using Esiur.Stores;

internal class Program
{
    private static async Task Main(string[] args)
    {
        // Create a store to keep objects.
        var system = await Warehouse.Put("sys", new MemoryStore());
        // Create a distibuted server
        var esiurServer = await Warehouse.Put("sys/server", new DistributedServer());
        // Add your object to the store
        var service = await Warehouse.Put("sys/demo", new EsiurGreeter());


        // Start your server
        await Warehouse.Open();

    }
}