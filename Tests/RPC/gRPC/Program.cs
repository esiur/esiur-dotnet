

using Echo;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(o =>
{
    // Listen on 5300 and force HTTP/2 (h2c)
    o.ListenAnyIP(5300, lo => lo.Protocols = HttpProtocols.Http2);
});

builder.Services.AddGrpc();
var app = builder.Build();
app.MapGrpcService<EchoServiceImpl>();
app.Urls.Add("http://0.0.0.0:5300");
app.Run();