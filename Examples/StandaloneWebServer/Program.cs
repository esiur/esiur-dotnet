
using Esiur.Examples.StandaloneWebServerDemo;
using Esiur.Net.Http;
using Esiur.Net.Packets.Http;
using Esiur.Protocol;
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
        var esiurServer = await wh.Put("sys/server", new EpServer());
        // Add your object to the store
        var service = await wh.Put("sys/demo", new Demo());

        var http = await wh.Put<HttpServer>("sys/http", new HttpServer() { Port = 8888 });
        var webRoot = Path.GetFullPath("Web");
        var webRootPrefix = webRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                            + Path.DirectorySeparatorChar;
        var pathComparison = Path.DirectorySeparatorChar == '\\'
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;


        http.MapGet("{url}", (string url, HttpConnection sender) =>
        {
            var requestedPath = sender.Request.Filename == "/"
                ? "index.html"
                : sender.Request.Filename.TrimStart('/', '\\');
            string fn;

            try
            {
                fn = Path.GetFullPath(Path.Combine(webRoot, requestedPath));
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is NotSupportedException ||
                exception is PathTooLongException)
            {
                sender.Response.Number = HttpResponseCode.BadRequest;
                sender.Send("Invalid path");
                sender.Close();
                return;
            }

            if (!fn.StartsWith(webRootPrefix, pathComparison))
            {
                sender.Response.Number = HttpResponseCode.Forbidden;
                sender.Send("Forbidden");
                sender.Close();
                return;
            }

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
                sender.Response.Number = HttpResponseCode.NotFound;
                sender.Send("Not Found");
                sender.Close();
            }

        });


        // Start your server
        await wh.Open();

        Console.WriteLine("Running on http://localhost:8888");

    }
}
