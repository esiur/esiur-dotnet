using System.Reflection;
using Esiur.Data.Types;
using Esiur.Protocol;
using Esiur.Resource;
using Esiur.Security.Authority;
using Esiur.Security.Authority.Providers;

namespace Esiur.Tests.Unit.Integration;

[Resource]
public partial class SyncProbeA
{
    [Export] public int Alpha { get; set; }
}

[Resource]
public partial class SyncProbeB
{
    [Export] public string? Beta { get; set; }
}

// docs/typedef-sync.md: a client that keeps definitions between connections must never bind a
// resource to the wrong definition after the serving node restarted and renumbered its types.
[Collection("Integration")]
public class TypeDefSyncIntegrationTests
{
    const string DomainName = "test";

    static EpConnectionContext Context() => new()
    {
        AuthenticationMode = AuthenticationMode.InitializerIdentity,
        Identity = "tester",
        AuthenticationProtocol = PasswordAuthenticationProvider.ProtocolName,
        Domain = DomainName,
    };

    // Registers A then B (A gets the lower id) or B then A, like two builds of the same service.
    static Task<IntegrationCluster> StartAsync(bool aFirst)
        => IntegrationCluster.StartAsync(async warehouse =>
        {
            if (aFirst)
            {
                await warehouse.Put("sys/a", new SyncProbeA { Alpha = 1 });
                await warehouse.Put("sys/b", new SyncProbeB { Beta = "one" });
            }
            else
            {
                await warehouse.Put("sys/b", new SyncProbeB { Beta = "two" });
                await warehouse.Put("sys/a", new SyncProbeA { Alpha = 2 });
            }
        });

    static async Task<EpResource> GetAsync(EpConnection connection, string path)
        => (EpResource)await Task.Run(async () => await connection.Get(path))
            .WaitAsync(TimeSpan.FromSeconds(10));

    static async Task<EpConnection> ConnectAsync(Warehouse clientWarehouse, int port)
        => await Task.Run(async () =>
                await clientWarehouse.Get<EpConnection>($"ep://localhost:{port}", Context()))
            .WaitAsync(TimeSpan.FromSeconds(10));

    [Fact]
    public async Task ServerRestartWithSwappedIds_KeepsEveryResourceOnItsOwnDefinition()
    {
        await using var first = await StartAsync(aFirst: true).WaitAsync(TimeSpan.FromSeconds(10));
        var clientWarehouse = first.ClientWarehouse;

        var a1 = await GetAsync(first.Connection, "sys/a");
        var b1 = await GetAsync(first.Connection, "sys/b");
        var idA1 = a1.Instance.Definition.Id;
        var idB1 = b1.Instance.Definition.Id;
        Assert.NotEqual(idA1, idB1);

        await using var second = await StartAsync(aFirst: false).WaitAsync(TimeSpan.FromSeconds(10));
        // Same client warehouse, new node whose type ids are the other way round.
        var connection = await ConnectAsync(clientWarehouse, second.Port);

        var a2 = await GetAsync(connection, "sys/a");
        var b2 = await GetAsync(connection, "sys/b");

        Assert.EndsWith("SyncProbeA", a2.Instance.Definition.Name);
        Assert.EndsWith("SyncProbeB", b2.Instance.Definition.Name);
        Assert.True(a2.TryGetPropertyValue(a2.Instance.Definition.GetPropertyDefByName("Alpha")!.Index, out var alpha));
        Assert.Equal(2, Convert.ToInt32(alpha));
        Assert.True(b2.TryGetPropertyValue(b2.Instance.Definition.GetPropertyDefByName("Beta")!.Index, out var beta));
        Assert.Equal("two", beta);

        // The definition objects were kept (identity is what resources and type descriptors hold),
        // re-keyed to the ids the new node uses.
        Assert.Same(a1.Instance.Definition, a2.Instance.Definition);
        Assert.Same(b1.Instance.Definition, b2.Instance.Definition);
        Assert.Equal(idB1, a2.Instance.Definition.Id);
        Assert.Equal(idA1, b2.Instance.Definition.Id);

        connection.Destroy();
    }

    [Fact]
    public async Task UnchangedServer_SecondConnectionStartsWithTheValidatedCache()
    {
        await using var cluster = await StartAsync(aFirst: true).WaitAsync(TimeSpan.FromSeconds(10));
        var a1 = await GetAsync(cluster.Connection, "sys/a");

        var connection = await ConnectAsync(cluster.ClientWarehouse, cluster.Port);

        // Nothing was fetched yet, but the connection already knows the definition under its id.
        Assert.True(connection.GetRuntimeMetrics().CachedTypeDefinitions >= 1);

        var a2 = await GetAsync(connection, "sys/a");
        Assert.Same(a1.Instance.Definition, a2.Instance.Definition);

        connection.Destroy();
    }

    [Fact]
    public async Task ChangedDefinition_IsUpdatedInPlaceAndTheNewMemberIsUsable()
    {
        await using var first = await StartAsync(aFirst: true).WaitAsync(TimeSpan.FromSeconds(10));
        var a1 = await GetAsync(first.Connection, "sys/a");
        var definition = (RemoteTypeDef)a1.Instance.Definition;
        var signatureBefore = definition.Signature;
        Assert.NotEqual(0UL, signatureBefore);
        Assert.Null(definition.GetFunctionDefByName("Gamma"));

        await using var second = await IntegrationCluster.StartAsync(async warehouse =>
        {
            await warehouse.Put("sys/a", new SyncProbeA { Alpha = 7 });
            // A newer build of the same service: SyncProbeA gained a function.
            var served = (LocalTypeDef)warehouse.FindServedTypeDefByName(definition.Name)!;
            AddFunction(served, "Gamma");
        });

        var connection = await ConnectAsync(first.ClientWarehouse, second.Port);

        Assert.NotEqual(signatureBefore, definition.Signature);
        Assert.NotNull(definition.GetFunctionDefByName("Gamma"));
        Assert.False(definition.Removed);

        var a2 = await GetAsync(connection, "sys/a");
        Assert.Same(definition, a2.Instance.Definition);
        Assert.True(a2.TryGetPropertyValue(definition.GetPropertyDefByName("Alpha")!.Index, out var alpha));
        Assert.Equal(7, Convert.ToInt32(alpha));

        connection.Destroy();
    }

    [Fact]
    public async Task RemovedDefinition_LeavesTheCacheAndIsFlagged()
    {
        await using var first = await StartAsync(aFirst: true).WaitAsync(TimeSpan.FromSeconds(10));
        var b1 = await GetAsync(first.Connection, "sys/b");
        var definition = (RemoteTypeDef)b1.Instance.Definition;

        // The new node no longer serves SyncProbeB at all.
        await using var second = await IntegrationCluster.StartAsync(async warehouse =>
            await warehouse.Put("sys/a", new SyncProbeA { Alpha = 3 }));

        var connection = await ConnectAsync(first.ClientWarehouse, second.Port);

        Assert.True(definition.Removed);
        Assert.DoesNotContain(definition, first.ClientWarehouse.GetCachedRemoteTypeDefs(DomainName));

        connection.Destroy();
    }

    // Simulates a newer build of the service: the served definition gets one more function.
    // (A function needs no per-instance state, unlike a property.)
    static void AddFunction(LocalTypeDef served, string name)
    {
        var functions = (List<FunctionDef>)typeof(TypeDef)
            .GetField("_functions", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(served)!;
        functions.Add(new FunctionDef
        {
            Name = name,
            Index = (byte)functions.Count,
            ReturnType = served.Properties[0].ValueType,
            Arguments = Array.Empty<ArgumentDef>(),
            Definition = served,
        });
        typeof(TypeDef).GetField("_signature", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(served, 0UL);
    }
}
