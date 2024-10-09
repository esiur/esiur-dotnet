using Esiur.AspNetCore;
using Esiur.AspNetCore.Example;
using Esiur.Core;
using Esiur.Net.IIP;
using Esiur.Net.Sockets;
using Esiur.Resource;
using Esiur.Stores;
using Microsoft.AspNetCore.Hosting.Server;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

//builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
//builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen();

builder.WebHost.UseUrls("http://localhost:8080");

var app = builder.Build();

var webSocketOptions = new WebSocketOptions()
{
    KeepAliveInterval = TimeSpan.FromSeconds(120), 
};

app.UseWebSockets(webSocketOptions);


// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
//   app.UseSwagger();
//    app.UseSwaggerUI();
//}

//app.UseHttpsRedirection();

//app.UseAuthorization();

//app.MapControllers();


await Warehouse.Put("sys", new MemoryStore());
await Warehouse.Put("sys/service", new MyResource());
var server = await Warehouse.Put("sys/server", new DistributedServer());

await Warehouse.Open();

app.UseEsiur(new EsiurOptions() { Server = server });


await app.RunAsync();
