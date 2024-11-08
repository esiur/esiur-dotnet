
using Esiur.Examples.StandaloneWebServerDemo;
using Esiur.Net.HTTP;
using Esiur.Net.IIP;
using Esiur.Resource;
using Esiur.Stores;
using Microsoft.AspNetCore.StaticFiles;

internal class Program
{

    static FileExtensionContentTypeProvider MIMEProvider = new FileExtensionContentTypeProvider();

    private static async Task Main(string[] args)
    {
        // Create a store to keep objects.
        var system = await Warehouse.Put("sys", new MemoryStore());
        // Create a distibuted server
        var esiurServer = await Warehouse.Put("sys/server", new DistributedServer());
        // Add your object to the store
        var service = await Warehouse.Put("sys/demo", new Demo());

        var http = await Warehouse.Put<HTTPServer>("sys/http", new HTTPServer() { Port = 8888 });


        http.MapGet("{url}", (string url, HTTPConnection sender) =>
        {
            var fn = "Web/" + (sender.Request.Filename == "/" ? "/index.html" : sender.Request.Filename);

            if (File.Exists(fn))
            {

                string contentType;

                if (!MIMEProvider.TryGetContentType(fn, out contentType))
                    contentType = "application/octet-stream";

                sender.Response.Headers["Content-Type"] = contentType;
                sender.SendFile(fn).Wait(20000);
            }
            else
            {
                sender.Response.Number = Esiur.Net.Packets.HTTP.HTTPResponseCode.NotFound;
                sender.Send("`" + fn + "` Not Found");
                sender.Close();
            }

        });


        // Start your server
        await Warehouse.Open();

        Console.WriteLine("Running on http://localhost:8888");

    }
}