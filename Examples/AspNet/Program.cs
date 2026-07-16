using Esiur.AspNetCore;
using Esiur.AspNetCore.Example;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:8080");

builder.Services.AddEsiur(esiur =>
{
    esiur
        .AddMemoryStore("sys")
        .AddResource<MyResource>("sys/service")
        // This sample is a development quick start. Production applications
        // should configure an authentication provider instead.
        .AllowAnonymous()
        .IncludeExceptionMessages();
});

var app = builder.Build();

var webSocketOptions = new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30),
};
webSocketOptions.AllowedOrigins.Add("http://localhost:8080");
app.UseWebSockets(webSocketOptions);

app.MapEsiur("/esiur");
app.MapGet("/", () => new
{
    message = "Esiur is available over WebSockets at /esiur.",
    resource = "sys/service",
    authentication = "Anonymous access is enabled for this development sample.",
});

await app.RunAsync();
