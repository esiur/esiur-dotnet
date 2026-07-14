# Esiur functional smoke test

This project starts an in-process Esiur server and an authenticated client,
runs its scenarios over a real loopback connection, and exits with a non-zero
status if any assertion fails.

Coverage includes:

- resource attachment and property serialization;
- procedure and resource calls, optional arguments, tuples, and events;
- named rate policies for function calls and property setters;
- rate-limit denial propagation to callers;
- pull streams backed by `IAsyncEnumerable<T>`;
- `TerminateExecution` through async-enumerator disposal;
- `HaltExecution` and `ResumeExecution` for a cooperative push stream;
- AES-256-GCM provider negotiation and encrypted authenticated-session traffic;
- parser allocation and collection budgets, attachment quotas, and per-IP connection limits.

Run it from the repository root:

```powershell
dotnet run --project Tests\Features\Functional\Esiur.Tests.Functional.csproj
```

The runner selects an available loopback port and shuts down its client,
server, and warehouses when complete.

Rate policies are registered by name on the server Warehouse:

```csharp
warehouse.AddRatePolicy(new BurstRatePolicy("standard-call")
{
    PermitLimit = 100,
    Period = TimeSpan.FromSeconds(1),
    BurstLimit = 20,
    QueueLimit = 50,
});

warehouse.Configuration.RateControl.DenialsBeforeConnectionBlock = 5;

[RateControl("standard-call")]
public void Call()
{
}
```

Security limits are configured per Warehouse. A value of zero disables an individual limit:

```csharp
warehouse.Configuration.Parser.MaximumPacketSize = 8 * 1024 * 1024;
warehouse.Configuration.Parser.MaximumAllocationSize = 4 * 1024 * 1024;
warehouse.Configuration.Parser.MaximumCollectionItems = 65_536;

warehouse.Configuration.ResourceAttachments.MaximumAttachedResourcesPerConnection = 4_096;
warehouse.Configuration.ResourceAttachments.MaximumPendingAttachmentsPerConnection = 128;
warehouse.Configuration.ResourceAttachments.RejectDuplicateAttachments = true;

warehouse.Configuration.Connections.MaximumConnectionsPerIpAddress = 64;
warehouse.Configuration.Encryption.MaximumRecordSize = 8 * 1024 * 1024 + 1024;
```

Authenticated encryption is opt-in and fails closed when requested. Register the
provider on both Warehouses, allow it on the server, and request it from the client:

```csharp
serverWarehouse.RegisterEncryptionProvider(new AesEncryptionProvider());
server.AllowedEncryptionProviders = new[] { AesEncryptionProvider.Name };
server.RequireEncryption = true;

clientWarehouse.RegisterEncryptionProvider(new AesEncryptionProvider());
var context = new EpConnectionContext
{
    AuthenticationMode = AuthenticationMode.InitializerIdentity,
    EncryptionMode = EncryptionMode.EncryptWithSessionKey,
    EncryptionProviders = new[] { AesEncryptionProvider.Name },
};
```
