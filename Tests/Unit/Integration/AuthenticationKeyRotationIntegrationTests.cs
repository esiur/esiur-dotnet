using System.Threading;
using Esiur.Core;
using Esiur.Protocol;
using Esiur.Resource;
using Esiur.Security.Authority;
using Esiur.Security.Cryptography;
using Esiur.Stores;

namespace Esiur.Tests.Unit.Integration;

[Collection("Integration")]
public class AuthenticationKeyRotationIntegrationTests
{
    [Fact]
    public async Task RequiredRotation_CompletesBeforeEncryptedConnectionBecomesReady()
    {
        var serverProvider = new TestRotatingAuthenticationProvider();
        var clientProvider = new TestRotatingAuthenticationProvider();

        await using var cluster = await CustomAuthenticationIntegrationCluster.StartAsync(
                serverProvider,
                clientProvider,
                encrypted: true,
                populate: async warehouse =>
                    await warehouse.Put("sys/rotated", new Node { Id = 73 }))
            .WaitAsync(TimeSpan.FromSeconds(10));

        var serverConnection = Assert.Single(cluster.Server.Connections);

        Assert.True(clientProvider.RotationStarted);
        Assert.True(serverProvider.RotationCommitted);
        Assert.True(cluster.Connection.Session.Authenticated);
        Assert.True(serverConnection.Session.Authenticated);
        Assert.True(cluster.Connection.IsEncrypted);
        Assert.True(serverConnection.IsEncrypted);

        var resource = await Task.Run(async () =>
                await cluster.Connection.Get("sys/rotated"))
            .WaitAsync(TimeSpan.FromSeconds(10));
        Assert.NotNull(resource);
    }

    [Fact]
    public async Task RequiredRotation_RejectsPlaintextTransport()
    {
        var serverProvider = new TestRotatingAuthenticationProvider();
        var clientProvider = new TestRotatingAuthenticationProvider();

        var exception = await Assert.ThrowsAnyAsync<Exception>(async () =>
            await CustomAuthenticationIntegrationCluster.StartAsync(
                    serverProvider,
                    clientProvider,
                    encrypted: false)
                .WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.IsNotType<TimeoutException>(exception);
        Assert.False(clientProvider.RotationStarted);
        Assert.False(serverProvider.RotationCommitted);
    }

    [Fact]
    public async Task InvalidRotationProof_FailsClosedBeforeConnectionBecomesReady()
    {
        var serverProvider = new TestRotatingAuthenticationProvider();
        var clientProvider = new TestRotatingAuthenticationProvider(corruptProof: true);

        var exception = await Assert.ThrowsAnyAsync<Exception>(async () =>
            await CustomAuthenticationIntegrationCluster.StartAsync(
                    serverProvider,
                    clientProvider,
                    encrypted: true)
                .WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.IsNotType<TimeoutException>(exception);
        Assert.True(clientProvider.RotationStarted);
        Assert.True(serverProvider.RotationRejected);
        Assert.False(serverProvider.RotationCommitted);
    }
}

internal sealed class TestRotatingAuthenticationProvider : IAuthenticationProvider
{
    readonly bool _corruptProof;

    public TestRotatingAuthenticationProvider(bool corruptProof = false)
        => _corruptProof = corruptProof;

    public string DefaultName => "rotating-test";
    public bool RotationStarted { get; internal set; }
    public bool RotationCommitted { get; internal set; }
    public bool RotationRejected { get; internal set; }

    public IAuthenticationHandler CreateAuthenticationHandler(AuthenticationContext context)
        => new TestRotatingAuthenticationHandler(this, context.Direction, _corruptProof);

    public AsyncReply<bool> Login(Session session) => new(true);
    public AsyncReply<bool> Logout(Session session) => new(true);
}

internal sealed class TestRotatingAuthenticationHandler :
    IAuthenticationHandler,
    IAuthenticationKeyRotationHandler
{
    const string BeginToken = "rotation-begin";
    const string ChallengeToken = "rotation-challenge";
    const string ProofToken = "rotation-proof";
    const string InvalidProofToken = "rotation-proof-invalid";
    const string AcknowledgementToken = "rotation-ack";

    static readonly byte[] SessionKey =
        Enumerable.Range(1, 64).Select(value => (byte)value).ToArray();

    readonly TestRotatingAuthenticationProvider _provider;
    readonly AuthenticationDirection _direction;
    readonly bool _corruptProof;
    int _rotationStep;

    public TestRotatingAuthenticationHandler(
        TestRotatingAuthenticationProvider provider,
        AuthenticationDirection direction,
        bool corruptProof)
    {
        _provider = provider;
        _direction = direction;
        _corruptProof = corruptProof;
    }

    public IAuthenticationProvider Provider => _provider;
    public string Protocol => _provider.DefaultName;
    public bool RequiresKeyRotation => true;

    public AuthenticationResult Process(object authData)
        => new(
            AuthenticationRuling.Succeeded,
            authenticationData: Array.Empty<byte>(),
            localIdentity: _direction == AuthenticationDirection.Initiator ? "client" : "server",
            remoteIdentity: _direction == AuthenticationDirection.Initiator ? "server" : "client",
            sessionKey: (byte[])SessionKey.Clone());

    public AuthenticationKeyRotationResult BeginKeyRotation()
    {
        if (_direction != AuthenticationDirection.Initiator
            || Interlocked.CompareExchange(ref _rotationStep, 1, 0) != 0)
        {
            return Failed("Rotation was started in an invalid state.");
        }

        _provider.RotationStarted = true;
        return InProgress(BeginToken);
    }

    public AuthenticationKeyRotationResult ProcessKeyRotation(object data)
    {
        if (_direction == AuthenticationDirection.Initiator)
        {
            if (_rotationStep != 1 || !String.Equals(data as string, ChallengeToken, StringComparison.Ordinal))
                return Failed("The responder rotation challenge is invalid.");

            _rotationStep = 2;
            return Succeeded(_corruptProof ? InvalidProofToken : ProofToken);
        }

        if (_rotationStep == 0 && String.Equals(data as string, BeginToken, StringComparison.Ordinal))
        {
            _rotationStep = 1;
            return InProgress(ChallengeToken);
        }

        if (_rotationStep == 1)
        {
            if (!String.Equals(data as string, ProofToken, StringComparison.Ordinal))
            {
                _provider.RotationRejected = true;
                return Failed("The initiator rotation proof is invalid.");
            }

            _rotationStep = 2;
            _provider.RotationCommitted = true;
            return Succeeded(AcknowledgementToken);
        }

        return Failed("The rotation message arrived out of sequence.");
    }

    static AuthenticationKeyRotationResult InProgress(object data)
        => new(AuthenticationKeyRotationRuling.InProgress, data);

    static AuthenticationKeyRotationResult Succeeded(object data)
        => new(AuthenticationKeyRotationRuling.Succeeded, data);

    static AuthenticationKeyRotationResult Failed(string error)
        => new(AuthenticationKeyRotationRuling.Failed, error: error);
}

internal sealed class CustomAuthenticationIntegrationCluster : IAsyncDisposable
{
    static int _portCounter = 16400;

    public Warehouse ServerWarehouse { get; }
    public Warehouse ClientWarehouse { get; }
    public EpServer Server { get; }
    public EpConnection Connection { get; private set; } = null!;

    CustomAuthenticationIntegrationCluster(
        Warehouse serverWarehouse,
        Warehouse clientWarehouse,
        EpServer server)
    {
        ServerWarehouse = serverWarehouse;
        ClientWarehouse = clientWarehouse;
        Server = server;
    }

    public static async Task<CustomAuthenticationIntegrationCluster> StartAsync(
        IAuthenticationProvider serverProvider,
        IAuthenticationProvider clientProvider,
        bool encrypted,
        Func<Warehouse, Task>? populate = null,
        AuthenticationMode authenticationMode = AuthenticationMode.InitializerIdentity,
        string? identity = "client",
        string? responderIdentity = null,
        string? domain = "test",
        Action<EpServer>? serverCreated = null)
    {
        var port = Interlocked.Increment(ref _portCounter);
        var serverWarehouse = new Warehouse();
        var clientWarehouse = new Warehouse();

        serverWarehouse.RegisterAuthenticationProvider(serverProvider);
        clientWarehouse.RegisterAuthenticationProvider(clientProvider);

        if (encrypted)
        {
            serverWarehouse.RegisterEncryptionProvider(new AesEncryptionProvider());
            clientWarehouse.RegisterEncryptionProvider(new AesEncryptionProvider());
        }

        await serverWarehouse.Put("sys", new MemoryStore());
        var server = await serverWarehouse.Put("sys/server", new EpServer
        {
            Port = (ushort)port,
            AllowedAuthenticationProviders = new[] { serverProvider.DefaultName },
            AllowedEncryptionProviders = encrypted
                ? new[] { AesEncryptionProvider.Name }
                : Array.Empty<string>(),
        });
        serverCreated?.Invoke(server);

        if (populate != null)
            await populate(serverWarehouse);

        await serverWarehouse.Open();

        var cluster = new CustomAuthenticationIntegrationCluster(
            serverWarehouse,
            clientWarehouse,
            server);

        try
        {
            var context = new EpConnectionContext
            {
                AuthenticationMode = authenticationMode,
                AuthenticationProtocol = clientProvider.DefaultName,
                Identity = identity!,
                ResponderIdentity = responderIdentity!,
                EncryptionMode = encrypted
                    ? EncryptionMode.EncryptWithSessionKey
                    : EncryptionMode.None,
                EncryptionProviders = new[] { AesEncryptionProvider.Name },
            };
            if (domain != null)
                context.Domain = domain;

            cluster.Connection = await clientWarehouse.Get<EpConnection>(
                $"ep://localhost:{port}",
                context);

            return cluster;
        }
        catch
        {
            try { server.Destroy(); } catch { }
            try { await clientWarehouse.Close(); } catch { }
            try { await serverWarehouse.Close(); } catch { }
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try { Connection.Destroy(); } catch { }
        try { Server.Destroy(); } catch { }
        try { await ClientWarehouse.Close(); } catch { }
        try { await ServerWarehouse.Close(); } catch { }
        await Task.Delay(50);
    }
}
