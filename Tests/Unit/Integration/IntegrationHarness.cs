using System;
using System.Net;
using System.Net.Sockets;
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

// ---- password auth providers (self-consistent: client password {1..5} || server salt {6..10}
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
    readonly bool _requiresKeyRotation;

    public OneStepAuthenticationProvider(byte keyMarker = 0, bool requiresKeyRotation = false)
    {
        _keyMarker = keyMarker;
        _requiresKeyRotation = requiresKeyRotation;
    }

    public string DefaultName => "one-step";

    public IAuthenticationHandler CreateAuthenticationHandler(AuthenticationContext context)
        => new OneStepAuthenticationHandler(
            this,
            _keyMarker,
            context.Direction,
            _requiresKeyRotation);

    public AsyncReply<bool> Login(Session session) => new AsyncReply<bool>(true);
    public AsyncReply<bool> Logout(Session session) => new AsyncReply<bool>(true);
}

internal sealed class OneStepAuthenticationHandler :
    IAuthenticationHandler,
    IAuthenticationKeyRotationHandler
{
    static readonly byte[] SharedKey = Enumerable.Range(1, 64).Select(x => (byte)x).ToArray();
    readonly OneStepAuthenticationProvider _provider;
    readonly byte _keyMarker;
    readonly AuthenticationDirection _direction;
    int _keyRotationStep;

    public OneStepAuthenticationHandler(
        OneStepAuthenticationProvider provider,
        byte keyMarker,
        AuthenticationDirection direction,
        bool requiresKeyRotation)
    {
        _provider = provider;
        _keyMarker = keyMarker;
        _direction = direction;
        RequiresKeyRotation = requiresKeyRotation;
    }

    public IAuthenticationProvider Provider => _provider;
    public string Protocol => _provider.DefaultName;
    public bool RequiresKeyRotation { get; }
    public bool KeyRotationCompleted { get; private set; }

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

    public AuthenticationKeyRotationResult BeginKeyRotation()
    {
        if (!RequiresKeyRotation || _direction != AuthenticationDirection.Initiator)
            return new(AuthenticationKeyRotationRuling.Failed, error: "Invalid key-rotation initiator.");

        _keyRotationStep = 1;
        return new(AuthenticationKeyRotationRuling.InProgress, new byte[] { 0xA1 });
    }

    public AuthenticationKeyRotationResult ProcessKeyRotation(object data)
    {
        if (!RequiresKeyRotation || data is not byte[] message || message.Length != 1)
            return new(AuthenticationKeyRotationRuling.Failed, error: "Invalid key-rotation data.");

        if (_direction == AuthenticationDirection.Responder
            && _keyRotationStep == 0
            && message[0] == 0xA1)
        {
            _keyRotationStep = 1;
            return new(AuthenticationKeyRotationRuling.InProgress, new byte[] { 0xB2 });
        }

        if (_direction == AuthenticationDirection.Initiator
            && _keyRotationStep == 1
            && message[0] == 0xB2)
        {
            _keyRotationStep = 2;
            KeyRotationCompleted = true;
            return new(AuthenticationKeyRotationRuling.Succeeded, new byte[] { 0xC3 });
        }

        if (_direction == AuthenticationDirection.Responder
            && _keyRotationStep == 1
            && message[0] == 0xC3)
        {
            _keyRotationStep = 2;
            KeyRotationCompleted = true;
            return new(AuthenticationKeyRotationRuling.Succeeded);
        }

        return new(AuthenticationKeyRotationRuling.Failed, error: "Invalid key-rotation sequence.");
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

    static int NextAvailablePort()
    {
        while (true)
        {
            var candidate = Interlocked.Increment(ref _portCounter);
            using var probe = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Stream,
                ProtocolType.Tcp);

            try
            {
                probe.Bind(new IPEndPoint(IPAddress.Any, candidate));
                return candidate;
            }
            catch (SocketException)
            {
                // Tests share the host with other applications. Skip ports already
                // bound or reserved instead of making the suite environment-dependent.
            }
        }
    }

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
        bool requireKeyRotation = false,
        bool allowAuthentication = true,
        bool registerServerAuthenticationProvider = true)
    {
        var port = NextAvailablePort();

        var serverWh = new Warehouse();
        if (registerServerAuthenticationProvider)
            serverWh.RegisterAuthenticationProvider(oneStepAuthentication
                ? new OneStepAuthenticationProvider(requiresKeyRotation: requireKeyRotation)
                : new TestServerAuthProvider());
        if (encrypted || requireEncryption)
            serverWh.RegisterEncryptionProvider(new AesEncryptionProvider());

        await serverWh.Put("sys", new MemoryStore());
        var server = await serverWh.Put("sys/server", new EpServer
        {
            Port = (ushort)port,
            AllowedAuthenticationProviders = allowAuthentication
                ? new[]
                {
                    oneStepAuthentication
                        ? "one-step"
                        : PasswordAuthenticationProvider.ProtocolName,
                }
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
            ? new OneStepAuthenticationProvider(
                mismatchedSessionKeys ? (byte)0x80 : (byte)0,
                requireKeyRotation)
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
                    AuthenticationProtocol = oneStepAuthentication
                        ? "one-step"
                        : PasswordAuthenticationProvider.ProtocolName,
                    Domain = "test",
                    EncryptionMode = encrypted
                        ? encryptionMode
                        : EncryptionMode.None,
                    EncryptionProviders = new[] { AesEncryptionProvider.Name },
                    WebSocketUri = useWebSocket
                        ? new Uri($"ws://127.0.0.1:{port}")
                        : null,
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
