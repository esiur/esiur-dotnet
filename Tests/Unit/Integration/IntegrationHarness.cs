using System;
using System.Threading;
using System.Threading.Tasks;
using Esiur.Core;
using Esiur.Protocol;
using Esiur.Resource;
using Esiur.Security.Authority;
using Esiur.Security.Authority.Providers;
using Esiur.Security.Cryptography;
using Esiur.Stores;

namespace Esiur.Tests.Unit.Integration;

// ---- hash auth providers (self-consistent: client password {1..5} || server salt {6..10}
//      == {1..10}, which is what the server stores the hash of) ------------------------------

internal class TestServerAuthProvider : PasswordAuthenticationProvider
{
    public override PasswordHash GetHostedAccountCredential(string identity, string domain)
        => identity == "tester" && domain == "test"
            ? new PasswordHash(
                PasswordAuthenticationHandler.ComputeSha3(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }),
                new byte[] { 6, 7, 8, 9, 10 })
            : new PasswordHash(null, null);
}

internal class TestClientAuthProvider : PasswordAuthenticationProvider
{
    public override byte[] GetSelfCredential(string identity, string domain, string hostname)
        => identity == "tester" && domain == "test" ? new byte[] { 1, 2, 3, 4, 5 } : null;

    public override IdentityPassword GetSelfIdentityAndCredential(string domain, string hostname)
        => domain == "test"
            ? new IdentityPassword { Identity = "tester", Password = new byte[] { 1, 2, 3, 4, 5 } }
            : new IdentityPassword { Identity = null, Password = null };
}

internal sealed class OneStepAuthenticationProvider : IAuthenticationProvider
{
    readonly byte _keyMarker;

    public OneStepAuthenticationProvider(byte keyMarker = 0)
        => _keyMarker = keyMarker;

    public string DefaultName => "one-step";

    public IAuthenticationHandler CreateAuthenticationHandler(AuthenticationContext context)
        => new OneStepAuthenticationHandler(this, _keyMarker);

    public AsyncReply<bool> Login(Session session) => new AsyncReply<bool>(true);
    public AsyncReply<bool> Logout(Session session) => new AsyncReply<bool>(true);
}

internal sealed class OneStepAuthenticationHandler : IAuthenticationHandler
{
    static readonly byte[] SharedKey = Enumerable.Range(1, 64).Select(x => (byte)x).ToArray();
    readonly OneStepAuthenticationProvider _provider;
    readonly byte _keyMarker;

    public OneStepAuthenticationHandler(OneStepAuthenticationProvider provider, byte keyMarker)
    {
        _provider = provider;
        _keyMarker = keyMarker;
    }

    public IAuthenticationProvider Provider => _provider;
    public string Protocol => _provider.DefaultName;

    public AuthenticationResult Process(object authData)
    {
        var key = (byte[])SharedKey.Clone();
        key[0] ^= _keyMarker;
        return new AuthenticationResult(
            AuthenticationRuling.Succeeded,
            null,
            "tester",
            "server",
            key);
    }
}

/// <summary>
/// Spins up an in-process Esiur server and an authenticated client connection over loopback TCP,
/// so the real socket + protocol + FetchResource stack is exercised end to end. Each instance
/// uses a distinct port. Dispose closes the connection and tears down the server.
/// </summary>
internal sealed class IntegrationCluster : IAsyncDisposable
{
    static int _portCounter = 14400;

    public Warehouse ServerWarehouse { get; }
    public Warehouse ClientWarehouse { get; }
    public EpServer Server { get; }
    public EpConnection Connection { get; private set; }
    public int Port { get; }

    IntegrationCluster(Warehouse serverWh, EpServer server, int port)
    {
        ServerWarehouse = serverWh;
        Server = server;
        Port = port;
        ClientWarehouse = new Warehouse();
    }

    /// <summary>
    /// Builds a server hosting resources under "sys/&lt;rootPath&gt;" populated by
    /// <paramref name="populate"/>, opens it, then connects an authenticated client.
    /// </summary>
    public static async Task<IntegrationCluster> StartAsync(
        Func<Warehouse, Task> populate,
        bool encrypted = false,
        bool requireEncryption = false,
        EncryptionMode encryptionMode = EncryptionMode.EncryptWithSessionKey,
        bool allowEncryption = true,
        bool oneStepAuthentication = false,
        bool useWebSocket = false,
        bool mismatchedSessionKeys = false,
        bool allowAuthentication = true,
        bool registerServerAuthenticationProvider = true)
    {
        var port = Interlocked.Increment(ref _portCounter);

        var serverWh = new Warehouse();
        if (registerServerAuthenticationProvider)
            serverWh.RegisterAuthenticationProvider(oneStepAuthentication
                ? new OneStepAuthenticationProvider()
                : new TestServerAuthProvider());
        if (encrypted || requireEncryption)
            serverWh.RegisterEncryptionProvider(new AesEncryptionProvider());

        await serverWh.Put("sys", new MemoryStore());
        var server = await serverWh.Put("sys/server", new EpServer
        {
            Port = (ushort)port,
            AllowedAuthenticationProviders = allowAuthentication
                ? new[] { oneStepAuthentication ? "one-step" : "hash" }
                : Array.Empty<string>(),
            AllowedEncryptionProviders = (encrypted || requireEncryption) && allowEncryption
                ? new[] { AesEncryptionProvider.Name }
                : Array.Empty<string>(),
            RequireEncryption = requireEncryption,
        });

        await populate(serverWh);

        await serverWh.Open();

        var cluster = new IntegrationCluster(serverWh, server, port);

        cluster.ClientWarehouse.RegisterAuthenticationProvider(oneStepAuthentication
            ? new OneStepAuthenticationProvider(mismatchedSessionKeys ? (byte)0x80 : (byte)0)
            : new TestClientAuthProvider());
        if (encrypted)
            cluster.ClientWarehouse.RegisterEncryptionProvider(new AesEncryptionProvider());

        try
        {
            cluster.Connection = await cluster.ClientWarehouse.Get<EpConnection>(
                $"ep://localhost:{port}",
                new EpConnectionContext
                {
                    AuthenticationMode = AuthenticationMode.InitializerIdentity,
                    Identity = "tester",
                    AuthenticationProtocol = oneStepAuthentication ? "one-step" : "hash",
                    Domain = "test",
                    EncryptionMode = encrypted
                        ? encryptionMode
                        : EncryptionMode.None,
                    EncryptionProviders = new[] { AesEncryptionProvider.Name },
                    UseWebSocket = useWebSocket,
                });

            return cluster;
        }
        catch
        {
            try { server.Destroy(); } catch { }
            try { await cluster.ClientWarehouse.Close(); } catch { }
            try { await serverWh.Close(); } catch { }
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try { Connection?.Destroy(); } catch { }
        try { Server?.Destroy(); } catch { }
        await Task.Delay(50); // let the listener socket release the port
    }
}
