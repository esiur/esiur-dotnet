
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
        var wh = new Warehouse();

        // Create a store to keep objects.
        var system = await wh.Put("sys", new MemoryStore());
        // Create a distibuted server
        var esiurServer = await wh.Put("sys/server", new DistributedServer());
        // Add your object to the store
        var service = await wh.Put("sys/demo", new Demo());

        var http = await wh.Put<HTTPServer>("sys/http", new HTTPServer() { Port = 8888 });


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
        await wh.Open();

        Console.WriteLine("Running on http://localhost:8888");

    }
}