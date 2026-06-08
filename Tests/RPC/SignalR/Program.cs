
using Esiur.Tests.RPC.SignalRServer;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR(o =>
{
    o.EnableDetailedErrors = true;
    o.MaximumReceiveMessageSize = 10 * 1024 * 1024; // 10 MB
});

var app = builder.Build();

app.MapHub<EchoHub>("/hub/echo");
app.Urls.Add("http://0.0.0.0:5200");
app.Run();