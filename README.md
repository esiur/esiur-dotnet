# Esiur for .NET

[![CI](https://github.com/esiur/esiur-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/esiur/esiur-dotnet/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Esiur.svg)](https://www.nuget.org/packages/Esiur)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

Esiur is a distributed resource framework for building real-time services without
maintaining a separate RPC contract. A resource can expose properties, methods,
events, records, and related resources over the self-describing EP protocol.

Version 3 focuses on a smaller public API, first-class ASP.NET Core hosting,
extensible authentication, authenticated encryption, bounded resource usage, and
predictable lifecycle behavior. The protocol remains designed for multiple
language implementations: wire concepts and protocol names stay consistent,
while each implementation can present idiomatic awaitables such as .NET
`AsyncReply<T>`, Dart `Future`, or JavaScript `Promise`.

## Highlights

- Live distributed properties, method calls, and events.
- Self-describing resource, record, enum, and type metadata.
- C# source generation with `[Resource]` and `[Export]`.
- Native TCP and ASP.NET Core WebSocket transports.
- Isolated `Warehouse` runtimes with memory, Entity Framework Core, and MongoDB stores.
- Multi-step authentication providers with initializer, responder, and dual identities.
- Password challenge-response and PPAP ML-KEM-768 authentication.
- AES-256-GCM encrypted records and protected post-authentication key rotation.
- Permissions, rate-control, auditing, parser limits, connection limits, and attachment limits.
- Graceful startup and shutdown that settle resource lifecycle operations.

## Packages

All first-party v3 packages share major version `3` to make compatibility clear.

| Package | Purpose | Target framework |
| --- | --- | --- |
| [`Esiur`](https://www.nuget.org/packages/Esiur) | Core runtime, EP protocol, stores, security, and source generator | .NET Standard 2.0 |
| [`Esiur.AspNetCore`](https://www.nuget.org/packages/Esiur.AspNetCore) | ASP.NET Core hosting, dependency injection, endpoint routing, and WebSockets | .NET 8 / .NET 10 |
| [`Esiur.Stores.EntityCore`](https://www.nuget.org/packages/Esiur.Stores.EntityCore) | Entity Framework Core 10 resource store | .NET 10 |
| [`Esiur.Stores.MongoDB`](https://www.nuget.org/packages/Esiur.Stores.MongoDB) | MongoDB resource store | .NET 10 |
| [`Esiur.CLI`](https://www.nuget.org/packages/Esiur.CLI) | Generate typed C# models from a remote EP resource | .NET 10 tool |

The core runtime can be consumed by any compatible .NET Standard 2.0
application. Projects using the bundled source generator need a Roslyn 4.8 or
newer toolchain, such as .NET SDK 8 or newer. Building this repository requires
.NET SDK 10.

## Install

For an ASP.NET Core service:

```shell
dotnet add package Esiur.AspNetCore --version 3.0.0
```

For the standalone runtime or a client:

```shell
dotnet add package Esiur --version 3.0.0
```

## Define a resource

Mark the class `partial` so Esiur's source generator can implement the resource
lifecycle and generate exported properties from fields.

```csharp
using Esiur.Resource;

[Resource]
public partial class CounterResource
{
    [Export]
    int count;

    [Export]
    public event ResourceEventHandler<int>? Changed;

    [Export]
    public string Hello(string name) => $"Hello, {name}!";

    [Export]
    public int Increment()
    {
        Count++;
        Changed?.Invoke(Count);
        return Count;
    }
}
```

The private `count` field becomes the exported `Count` property. Assigning it
after the resource is attached publishes the modification to subscribed peers.
Methods and events explicitly marked with `[Export]` become part of the remote
definition.

## ASP.NET Core quick start

`Esiur.AspNetCore` is the recommended way to host Esiur in a web application.
The host owns startup and shutdown, while Esiur owns session authentication,
encryption, resource authorization, and the resource graph.

```csharp
using Esiur.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:8080");

builder.Services.AddEsiur(esiur =>
{
    esiur
        .AddMemoryStore("sys")
        .AddResource<CounterResource>("sys/counter")
        .AllowAnonymous()             // Development only.
        .IncludeExceptionMessages();  // Development only.
});

var app = builder.Build();

var webSockets = new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30),
};
webSockets.AllowedOrigins.Add("http://localhost:8080");

app.UseWebSockets(webSockets);
app.MapEsiur("/esiur");

await app.RunAsync();
```

`AddEsiur` registers one host-managed `Warehouse` and `EpServer`. Resources are
constructed through dependency injection, configuration is validated at host
startup, and active WebSockets are drained or aborted before resources are
terminated during shutdown.

The endpoint returned by `MapEsiur` supports normal ASP.NET Core conventions:

```csharp
app.MapEsiur("/esiur")
    .RequireAuthorization("EsiurUpgrade")
    .RequireRateLimiting("EsiurHandshakes");
```

ASP.NET Core authorization protects the HTTP upgrade only. It does not replace
Esiur authentication or per-resource authorization.

## Connect a .NET client

Use the logical `ep://` resource URL and select the WebSocket transport with an
`EpConnectionContext`:

```csharp
using Esiur.Protocol;
using Esiur.Resource;

var client = new Warehouse();

dynamic counter = await client.Get<IResource>(
    "ep://localhost/sys/counter",
    new EpConnectionContext
    {
        WebSocketUri = new Uri("ws://localhost:8080/esiur"),
    });

Console.WriteLine(await counter.Hello("Ada"));
Console.WriteLine(await counter.Increment());

await client.Close();
```

The `ep://` URL identifies the logical connection and resource path;
`WebSocketUri` identifies the transport endpoint. Esiur automatically requests
the case-sensitive `EP` WebSocket subprotocol.

For native TCP, omit `WebSocketUri` and include the EP port in the logical URL:

```csharp
dynamic counter = await client.Get<IResource>(
    "ep://localhost:10518/sys/counter");
```

## Standalone hosting

Applications that do not use ASP.NET Core can compose the runtime directly.
`Warehouse` is the root container for stores, resources, protocol handlers,
security providers, policies, and lifecycle state.

```csharp
using Esiur.Protocol;
using Esiur.Resource;
using Esiur.Stores;

var warehouse = new Warehouse();

await warehouse.Put("sys", new MemoryStore());
await warehouse.Put("sys/counter", new CounterResource());
await warehouse.Put("sys/server", new EpServer
{
    Port = 10518,
    AllowUnauthorizedAccess = true, // Development only.
});

await warehouse.Open();

try
{
    await Task.Delay(Timeout.InfiniteTimeSpan);
}
finally
{
    await warehouse.Close();
}
```

Use a separate `Warehouse` for each isolated server or client runtime. Avoid
relying on the static default Warehouse in applications that host more than one
Esiur environment.

## Authentication and encryption

Anonymous access is opt-in. Production services should omit `AllowAnonymous`,
register an authentication provider, register an encryption provider, and
require encryption.

The ASP.NET Core password shortcut accepts precomputed Esiur credentials from
an application-owned cache or account store:

```csharp
using Esiur.AspNetCore;
using Esiur.Security.Authority.Providers;
using Esiur.Security.Cryptography;

var credentials =
    new Dictionary<(string Domain, string Identity), PasswordHash>();

builder.Services.AddEsiur(esiur =>
{
    esiur
        .AddMemoryStore("sys")
        .AddResource<CounterResource>("sys/counter")
        .UsePasswordAuthentication((identity, domain) =>
            credentials.TryGetValue((domain, identity), out var credential)
                ? credential
                : null)
        .UseEncryption(new AesEncryptionProvider())
        .RequireEncryption()
        .AllowWebSocketOrigins("https://app.example.com");
});
```

Create a `PasswordHash` once during enrollment with
`PasswordAuthenticationProvider.CreateCredential(passwordBytes)`. Clear the
plaintext bytes after use and protect the stored verifier like a password: it
is sufficient to authenticate through the `password-sha3-v1` protocol if
stolen.

Esiur v3 currently includes these canonical protocol names:

| Protocol | Role |
| --- | --- |
| `password-sha3-v1` | Simple password challenge-response for initializer, responder, or dual identity authentication |
| `ppap-mlkem768-v1` | ML-KEM-768 authentication with password-derived or static identities |
| `aes-gcm` | AES-256-GCM record protection derived from the authenticated session key |

The authentication provider abstraction supports multi-message handshakes.
PPAP uses that pipeline for initializer, responder, or dual identity
authentication and supports password-derived Argon2id identities as well as
static ML-KEM identities. Password registrations use a versioned nonce and
encapsulation key. Successful rotation is performed only through a dedicated
exchange after encryption is active, preventing the registration nonce from
becoming a permanent network identifier. Persistent PPAP registration stores
must implement atomic compare-and-rotate behavior.

Applications with custom protocols can implement `IAuthenticationProvider` and
register the provider through `UseAuthentication`. Protocol names are exact and
case-sensitive; v3 does not negotiate legacy aliases.

## Entity Framework Core 10

`Esiur.Stores.EntityCore` integrates Esiur resource materialization and paths
with EF Core 10.

```shell
dotnet add package Esiur.Stores.EntityCore --version 3.0.0
dotnet add package Microsoft.EntityFrameworkCore.Sqlite --version 10.0.10
```

Configure the database provider first, then attach the `EntityStore` with the
typed `UseEsiur` extension:

```csharp
using Esiur.Resource;
using Esiur.Stores.EntityCore;
using Microsoft.EntityFrameworkCore;

var warehouse = new Warehouse();
var store = await warehouse.Put("database", new EntityStore());

DbContextOptions<AppDbContext>? options = null;
options = new DbContextOptionsBuilder<AppDbContext>()
    .UseSqlite("Data Source=app.db")
    .UseEsiur(store, () => new AppDbContext(options!))
    .Options;

await warehouse.Open();

await using var db = new AppDbContext(options);
await db.Database.EnsureCreatedAsync();
var device = await db.Devices.AddResourceAsync(new Device { Name = "sensor-1" });
```

```csharp
using System.ComponentModel.DataAnnotations;
using Esiur.Resource;
using Microsoft.EntityFrameworkCore;

[Resource]
public partial class Device
{
    [Key, Export]
    int id;

    [Export]
    string name = string.Empty;
}

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options)
{
    public DbSet<Device> Devices => Set<Device>();
}
```

The repository test suite runs a complete SQLite schema, insert,
materialization, and Warehouse lookup cycle. PostgreSQL through Npgsql and
MySQL through Oracle's EF provider are also checked for model creation and SQL
translation without requiring external database servers.

## Generate typed client models

EP is self-describing, so clients can work dynamically or generate strongly
typed models. Install the v3 CLI as a .NET tool:

```shell
dotnet tool install --global Esiur.CLI --version 3.0.0
esiur get-template ep://localhost:10518/sys/counter --dir Generated
```

Use `--async-setters` to generate asynchronous property setters. The CLI also
accepts `--username` and `--password`, but command-line secrets may be visible
to other processes or shell history; prefer a safer credential workflow when
the environment is shared.

## Runtime limits and security defaults

`WarehouseConfiguration` bounds work performed on untrusted input. The default
configuration includes limits for:

- Packet size, decoded allocation size, collection items, and type-metadata depth.
- Attached and pending resources per connection.
- Concurrent connections and connection attempts globally and per IP address.
- Encrypted record size.
- Repeated rate-control denials.

ASP.NET Core hosting also provides per-connection and host-wide pending
WebSocket send limits. Configure these for the expected workload rather than
disabling them:

```csharp
builder.Services.AddEsiur(esiur => esiur
    .AddMemoryStore("sys")
    .AddResource<CounterResource>("sys/counter")
    .LimitPendingWebSocketSendBytes(2 * 1024 * 1024)
    .LimitTotalPendingWebSocketSendBytes(256L * 1024 * 1024)
    .ConfigureWarehouse(configuration =>
    {
        configuration.Connections.MaximumConnections = 500;
        configuration.Connections.MaximumConnectionsPerIpAddress = 20;
        configuration.Connections.MaximumConnectionAttempts = 2_000;
    }));
```

When deploying behind a reverse proxy, trust only known proxy addresses and
apply forwarded headers before `UseWebSockets` and `MapEsiur`. Browser origin
checks, ASP.NET Core endpoint authorization, Esiur session authentication, and
resource permissions are separate layers and should be configured together.

## Build and test

The repository is pinned to .NET SDK 10:

```shell
dotnet restore Esiur.sln
dotnet build Esiur.sln --configuration Release
dotnet test Esiur.sln --configuration Release
dotnet list Esiur.sln package --vulnerable --include-transitive
```

The main automated suite covers packet parsing, serialization, authentication,
PPAP registration rotation, encrypted records, resource attachment limits,
connection admission, WebSockets, ASP.NET Core hosting, Warehouse lifecycle,
and EF Core provider integration.

## Version 3 compatibility

Version 3 is an intentional compatibility boundary:

- All distributed first-party packages use major version `3`.
- ASP.NET Core applications use `AddEsiur` and `MapEsiur` instead of the legacy integration.
- Authentication and encryption providers negotiate canonical protocol names without aliases.
- EntityCore targets .NET 10 and EF Core 10.
- Legacy packet and hosting APIs removed during the v3 cleanup are not retained as compatibility shims.

Within the v3 family, minor and patch package versions may advance
independently. Consumers should keep the same major version across Esiur
packages.

## License

Esiur is available under the [MIT License](LICENSE).
