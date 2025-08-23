/*

MIT License

Copyright (c) 2024 Esiur Foundation, Ahmed Kh. Zamil.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/

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

 
await Warehouse.Default.Put("sys", new MemoryStore());
await Warehouse.Default.Put("sys/service", new MyResource());
var server = await Warehouse.Default.Put("sys/server", new DistributedServer());

await Warehouse.Default.Open();

app.UseEsiur(new EsiurOptions() { Server = server });


await app.RunAsync();
