# Esiur ASP.Net Core Middleware

This project brings Esiur distributed resource framework to ASP.Net using WebSockets in the ASP.Net pipeline.
# Installation 
- Nuget
```Install-Package Esiur.AspNetCore```
- Command-line
``` dotnet add package Esiur.AspNetCore ```
# Example
```C#
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:8080");
var app = builder.Build();

app.UseWebSockets();

await Warehouse.Put("sys", new MemoryStore());
await Warehouse.Put("sys/service", new MyResource());
var server = await Warehouse.Put("sys/server", new DistributedServer());
await Warehouse.Open();

app.UseEsiur(new EsiurOptions() { Server = server });

await app.RunAsync();
```

## MyResource.cs

```c#
    [Resource]
    public partial class MyResource
    {
        [Export] int number;
        [Export] public string Hello() => "Hi";
    }
```


## Calling from JavaScript

Esiur provides a command line interpreter  for debugging using Node.JS which can be installed using 
```npm install -g esiur```

To access the shell
```esiur shell```

Now you can simply test the running service typing
```javascript
let x = await wh.get("iip://localhost:8080/sys/service", {secure: false});
await x.Hello();
```
