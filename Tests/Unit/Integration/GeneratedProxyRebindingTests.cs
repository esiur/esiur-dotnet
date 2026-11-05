using System.Reflection;
using System.Runtime.Loader;
using Esiur.Data.Types;
using Esiur.Protocol;
using Esiur.Proxy;
using Esiur.Resource;
using Esiur.Security.Authority;
using Esiur.Security.Authority.Providers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Esiur.Tests.Unit.Integration;

[Resource]
public partial class ProxyProbe
{
    [Export] public int Echo(int value) => value;

    [Export] public string Shout(string text) => text.ToUpperInvariant();

    [Export] public int Level { get; set; }
}

// docs/typedef-sync.md "Rebinding in place": a compiled, generated proxy must keep working when
// the node renumbers its members, and must fail with a clear error for a member that is gone.
[Collection("Integration")]
public class GeneratedProxyRebindingTests
{
    static EpConnectionContext Context() => new()
    {
        AuthenticationMode = AuthenticationMode.InitializerIdentity,
        Identity = "tester",
        AuthenticationProtocol = PasswordAuthenticationProvider.ProtocolName,
        Domain = "test",
    };

    static Task<IntegrationCluster> StartAsync(Action<LocalTypeDef>? newerBuild = null, int level = 5)
        => IntegrationCluster.StartAsync(async warehouse =>
        {
            await warehouse.Put("sys/probe", new ProxyProbe { Level = level });
            if (newerBuild != null)
                newerBuild((LocalTypeDef)warehouse.FindServedTypeDefByName(typeof(ProxyProbe).FullName!)!);
        });

    static Type CompileProxy(RemoteTypeDef definition)
    {
        var source = TypeDefGenerator.GenerateClass(definition, new TypeDef[] { definition }, asyncSetters: false);
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();
        references.Add(MetadataReference.CreateFromFile(typeof(EpResource).Assembly.Location));

        var compilation = CSharpCompilation.Create(
            "Generated.Proxy." + Guid.NewGuid().ToString("N"),
            new[] { CSharpSyntaxTree.ParseText(source) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var stream = new MemoryStream();
        var emitted = compilation.Emit(stream);
        Assert.True(emitted.Success,
            string.Join(Environment.NewLine, emitted.Diagnostics.Where(x => x.Severity == DiagnosticSeverity.Error))
            + Environment.NewLine + source);

        stream.Position = 0;
        return AssemblyLoadContext.Default.LoadFromStream(stream)
            .GetType(typeof(ProxyProbe).FullName!, throwOnError: true)!;
    }

    // Fetch the definition once with a plain dynamic client, as the generator tool would.
    static async Task<RemoteTypeDef> DefinitionOf(IntegrationCluster cluster)
    {
        var resource = (EpResource)await Task.Run(async () => await cluster.Connection.Get("sys/probe"))
            .WaitAsync(TimeSpan.FromSeconds(10));
        return (RemoteTypeDef)resource.Instance.Definition;
    }

    static async Task<EpResource> ConnectAndGetAsync(Warehouse clientWarehouse, int port)
    {
        var connection = await Task.Run(async () =>
                await clientWarehouse.Get<EpConnection>($"ep://localhost:{port}", Context()))
            .WaitAsync(TimeSpan.FromSeconds(10));
        return (EpResource)await Task.Run(async () => await connection.Get("sys/probe"))
            .WaitAsync(TimeSpan.FromSeconds(10));
    }

    static async Task<object?> CallAsync(object proxy, string method, params object[] args)
    {
        var reply = proxy.GetType().GetMethod(method)!.Invoke(proxy, args)!;
        dynamic awaitable = reply;
        // Bounded: a call that never gets an answer must fail the test, not hang the run.
        return await ((Task<object?>)Task.Run(async () => (object?)await awaitable)).WaitAsync(TimeSpan.FromSeconds(10));
    }

    static void SwapFunctionIndexes(LocalTypeDef served, string a, string b)
    {
        var functions = served.Functions;
        var first = functions.First(x => x.Name == a);
        var second = functions.First(x => x.Name == b);
        (first.Index, second.Index) = (second.Index, first.Index);
        // Keep the list ordered by index, like a freshly built definition.
        var list = (List<FunctionDef>)typeof(TypeDef)
            .GetField("_functions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(served)!;
        list.Sort((x, y) => x.Index.CompareTo(y.Index));
        typeof(TypeDef).GetField("_signature", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(served, 0UL);
    }

    static void RemoveFunction(LocalTypeDef served, string name)
    {
        var list = (List<FunctionDef>)typeof(TypeDef)
            .GetField("_functions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(served)!;
        list.RemoveAll(x => x.Name == name);
        for (byte i = 0; i < list.Count; i++)
            list[i].Index = i;
        typeof(TypeDef).GetField("_signature", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(served, 0UL);
    }

    [Fact]
    public async Task GeneratedProxy_KeepsWorkingWhenTheNodeRenumbersItsFunctions()
    {
        await using var first = await StartAsync().WaitAsync(TimeSpan.FromSeconds(10));
        var definition = await DefinitionOf(first);
        var proxyType = CompileProxy(definition);

        // The application's own client: the generated proxy is registered before connecting.
        var clientWarehouse = new Warehouse();
        clientWarehouse.RegisterAuthenticationProvider(new TestClientAuthProvider());
        clientWarehouse.RegisterProxyType(proxyType);

        var probe1 = await ConnectAndGetAsync(clientWarehouse, first.Port);
        Assert.IsType(proxyType, probe1);
        Assert.Equal(3, Convert.ToInt32(await CallAsync(probe1, "Echo", 3)));
        Assert.Equal("HI", await CallAsync(probe1, "Shout", "hi"));
        Assert.Equal(5, Convert.ToInt32(proxyType.GetProperty("Level")!.GetValue(probe1)));

        // A newer build of the node: Echo and Shout swapped places (every index shifted).
        await using var second = await StartAsync(served => SwapFunctionIndexes(served, "Echo", "Shout"), level: 9)
            .WaitAsync(TimeSpan.FromSeconds(10));
        var probe2 = await ConnectAndGetAsync(clientWarehouse, second.Port);

        Assert.IsType(proxyType, probe2);
        Assert.Equal(4, Convert.ToInt32(await CallAsync(probe2, "Echo", 4)));
        Assert.Equal("YO", await CallAsync(probe2, "Shout", "yo"));
        Assert.Equal(9, Convert.ToInt32(proxyType.GetProperty("Level")!.GetValue(probe2)));
    }

    [Fact]
    public async Task GeneratedProxy_ThrowsAClearErrorForAFunctionTheNodeRemoved()
    {
        await using var first = await StartAsync().WaitAsync(TimeSpan.FromSeconds(10));
        var definition = await DefinitionOf(first);
        var proxyType = CompileProxy(definition);

        var clientWarehouse = new Warehouse();
        clientWarehouse.RegisterAuthenticationProvider(new TestClientAuthProvider());
        clientWarehouse.RegisterProxyType(proxyType);
        _ = await ConnectAndGetAsync(clientWarehouse, first.Port);

        await using var second = await StartAsync(served => RemoveFunction(served, "Shout"))
            .WaitAsync(TimeSpan.FromSeconds(10));
        var probe = await ConnectAndGetAsync(clientWarehouse, second.Port);

        Assert.Equal(6, Convert.ToInt32(await CallAsync(probe, "Echo", 6)));

        var error = Assert.Throws<TargetInvocationException>(() => proxyType.GetMethod("Shout")!.Invoke(probe, new object[] { "x" }));
        var unavailable = Assert.IsType<EpMemberUnavailableException>(error.InnerException);
        Assert.Equal("Shout", unavailable.MemberName);
        Assert.Equal("function", unavailable.MemberKind);
        Assert.Contains("ProxyProbe", unavailable.Message);
        Assert.Contains("update the client", unavailable.Message);
    }
}
