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
- `HaltExecution` and `ResumeExecution` for a cooperative push stream.

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
