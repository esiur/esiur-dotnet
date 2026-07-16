# Esiur for ASP.NET Core

This package supports ASP.NET Core applications targeting .NET 8 or .NET 10.

`Esiur.AspNetCore` exposes an Esiur EP server through ASP.NET Core endpoint
routing and WebSockets. ASP.NET Core owns the application lifetime, while Esiur
continues to own its session authentication, encryption, and resource
authorization.

The integration does not open Esiur's separate TCP listener. Clients connect to
the route mapped with `MapEsiur`.

## Installation

```shell
dotnet add package Esiur.AspNetCore
```

## Development quick start

The following example deliberately enables anonymous Esiur sessions to keep a
local development service easy to try. Do not expose resources this way unless
their authorization policy is safe for anonymous callers.

```csharp
using Esiur.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:8080");

builder.Services.AddEsiur(esiur =>
{
    esiur
        .AddMemoryStore("sys")
        .AddResource<HelloResource>("sys/service")
        .AllowAnonymous() // Development only.
        .IncludeExceptionMessages(); // Development diagnostics only.
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

```csharp
using Esiur.Resource;

[Resource]
public partial class HelloResource
{
    private int count;

    [Export]
    public string Hello(string name) => $"Hello, {name}!";

    [Export]
    public int Increment() => Interlocked.Increment(ref count);

    [Export]
    public int CurrentCount() => Volatile.Read(ref count);
}
```

`AddEsiur` registers a host-managed Warehouse and EP server. The hosted runtime
adds the resources, opens the Warehouse during application startup, and closes
it during graceful shutdown. Shutdown stops admission and drains or aborts active
WebSockets before resource termination begins. Configuration is validated when
the host starts.

## Browser origin policy

Call `UseWebSockets` before the mapped endpoint. The Esiur endpoint accepts
native clients that omit the HTTP `Origin` header. For browser clients, it
allows the request's own origin when no Esiur origin list is configured.

Once origins are configured, they become the complete browser-origin
allowlist:

```csharp
builder.Services.AddEsiur(esiur =>
{
    esiur
        .AddMemoryStore("sys")
        .AddResource<HelloResource>("sys/service")
        .AllowAnonymous() // Development only; use authentication in production.
        .AllowWebSocketOrigins("https://app.example.com");
});
```

If `WebSocketOptions.AllowedOrigins` is also restricted, add the same origins
there. Set ASP.NET Core `AllowedHosts` to the service's real host names in
production; do not leave it as `*`, because implicit same-origin checks rely on
the validated request host. `AllowAnyWebSocketOrigin` is available for
exceptional cases, but it should not be the production default.

## Production authentication and encryption

Register an application authentication provider and authenticated record
encryption together. `UseAuthentication` and `UseEncryption` register their
`DefaultName` protocol names with the Warehouse and allow them on the EP server.
The facade deliberately does not offer protocol aliases: the provider and its
handshake handler must negotiate the same stable name across every Esiur
language implementation.

```csharp
using Esiur.AspNetCore;
using Esiur.Security.Authority.Providers;
using Esiur.Security.Cryptography;

// Load Esiur's precomputed salted password credentials from the application's
// account store. Do not keep raw passwords in this dictionary.
var passwordCredentials =
    new Dictionary<(string Domain, string Identity), PasswordHash>();

builder.Services.AddEsiur(esiur =>
{
    esiur
        .AddMemoryStore("sys")
        .AddResource<HelloResource>("sys/service")
        .UsePasswordAuthentication((identity, domain) =>
            passwordCredentials.TryGetValue((domain, identity), out var credential)
                ? credential
                : null)
        .UseEncryption(new AesEncryptionProvider())
        .AllowWebSocketOrigins("https://app.example.com")
        .RequireEncryption();
});
```

`UsePasswordAuthentication` is the simple server shortcut: the callback returns
a `PasswordHash` for an account or `null` when it is unknown, so applications do
not need to subclass `PasswordAuthenticationProvider`. The lookup is synchronous
and should use a memory cache or another non-blocking credential store. Advanced
protocols and asynchronous backends can still use `UseAuthentication(provider)`,
`UseAuthentication<TProvider>()`, or the provider factory overload. Each form
uses the provider's `DefaultName`.

The shortcut continues the same challenge-response shape for an unknown
identity using provider-local random dummy material. The application should
still make known and unknown lookups take comparable time and apply handshake
rate limits; the shortcut cannot hide timing differences in the application's
credential store. Identities and domains are limited to 512 UTF-8 bytes before
the application callback runs.

Create each stored value once during account enrollment with
`PasswordAuthenticationProvider.CreateCredential(passwordBytes)`, persist its
hash and salt, then clear and discard `passwordBytes`. Do not regenerate the
credential inside the authentication callback. The stored hash is a
password-equivalent verifier: anyone who obtains it can authenticate through
this protocol without recovering the original password (a pass-the-hash
attack). Keep it secret like a plaintext password: restrict access, encrypt it
at rest where appropriate, and never put it in logs or client-visible data.

For new production deployments, prefer `PpapAuthenticationProvider`. PPAP uses
Argon2id as part of its password-derived ML-KEM identity flow. An Argon2id hash
cannot simply replace `PasswordHash.Hash` here: `password-sha3-v1` fixes the
wire verifier to SHA3-256 of the password and protocol salt, so changing only
server-side storage would make clients compute a different value and fail
authentication. Memory-hard derivation therefore needs a protocol designed for
it on both peers, such as PPAP.

Omitting `AllowAnonymous` makes Esiur authentication mandatory.
`RequireEncryption` rejects sessions that do not negotiate both authentication
and an allowed encryption provider.

Remote errors contain stable Esiur codes only by default. Development services
can call `IncludeExceptionMessages()`. Source and stack details require an
explicit advanced `ConfigureServer` setting and should not be exposed publicly.

## Connection and memory limits

Esiur applies global and per-IP concurrent-connection and attempt limits. Tune
them for the deployment, and reduce the per-connection WebSocket send queue if
the application does not send large values:

```csharp
builder.Services.AddEsiur(esiur => esiur
    .AddMemoryStore("sys")
    .AddResource<HelloResource>("sys/service")
    .UsePasswordAuthentication((identity, domain) =>
        passwordCredentials.TryGetValue((domain, identity), out var credential)
            ? credential
            : null)
    .LimitPendingWebSocketSendBytes(2 * 1024 * 1024)
    .LimitTotalPendingWebSocketSendBytes(256L * 1024 * 1024)
    .ConfigureWarehouse(configuration =>
    {
        configuration.Connections.MaximumConnections = 500;
        configuration.Connections.MaximumConnectionsPerIpAddress = 20;
        configuration.Connections.MaximumConnectionAttempts = 2_000;
    }));
```

ASP.NET Core endpoint rate limiting can add a separate handshake policy. A
handshake limiter does not limit traffic after the WebSocket is established.
The total pending-send limit is shared by every Esiur WebSocket in the host, so
the per-connection allowance cannot multiply without bound under many slow
clients. Exceeding either limit closes the affected connection rather than
dropping a protocol frame and continuing with a corrupted stream.

The mapped endpoint participates in standard ASP.NET Core endpoint conventions:

```csharp
app.MapEsiur("/esiur")
    .RequireAuthorization("EsiurUpgrade")
    .RequireRateLimiting("EsiurHandshakes");
```

ASP.NET Core authorization only gates the HTTP WebSocket upgrade. It does not
replace Esiur session authentication or Esiur resource authorization.

## Reverse proxies

When TLS terminates at a trusted reverse proxy, apply forwarded headers before
`UseWebSockets` and `MapEsiur`. The original scheme and host are needed for
same-origin validation and correct connection metadata.

```csharp
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto |
        ForwardedHeaders.XForwardedHost;

    // Trust only the proxy addresses used by this deployment.
    options.KnownProxies.Add(IPAddress.Parse("10.0.0.10"));
});

// After builder.Build():
app.UseForwardedHeaders();
app.UseWebSockets();
app.MapEsiur("/esiur");
```

The proxy must support WebSocket upgrades and supply the corresponding
`X-Forwarded-*` headers. Do not trust arbitrary proxy addresses.

## Connecting from .NET through WebSockets

Set `EpConnectionContext.WebSocketUri` to the public `ws` or `wss` endpoint,
including the route mapped by `MapEsiur`:

```csharp
using Esiur.Protocol;
using Esiur.Resource;

var clientWarehouse = new Warehouse();

var service = await clientWarehouse.Get<IResource>(
    "ep://api.example.com/sys/service",
    new EpConnectionContext
    {
        WebSocketUri = new Uri("wss://api.example.com/esiur"),
        // Add the authentication mode, identity, and protocol required by the server.
    });
```

Every WebSocket client, including clients in other languages, must request the
exact, case-sensitive `EP` WebSocket subprotocol (`Sec-WebSocket-Protocol: EP`).
The Esiur client transports do this automatically.

The `ep://` URL identifies the logical EP connection and resource path;
`WebSocketUri` selects its WebSocket transport and carries the ASP.NET route.
When using address-bound encryption, use an IP-literal `WebSocketUri`: the .NET
`ClientWebSocket` API does not expose the endpoint selected for a DNS host, so
Esiur fails address binding closed instead of recording a separate DNS guess.

## Existing Warehouse and server

Advanced applications can supply their own instances while still using the
ASP.NET Core endpoint and host lifecycle:

```csharp
builder.Services.AddEsiur()
    .UseWarehouse(warehouse)
    .UseServer(server, "sys/server");
```

Use `UseWarehouse(warehouse, manageLifecycle: false)` only when another
component owns the Warehouse lifecycle and the supplied server and resources
are already attached and initialized.
