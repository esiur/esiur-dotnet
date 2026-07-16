namespace Esiur.Tests.Unit.Integration;

using Esiur.Data;
using Esiur.Net.Sockets;
using Esiur.Protocol;
using Esiur.Security.Authority.Providers;
using Esiur.Security.Cryptography;

[Collection("Integration")]
public class SessionHeadersIntegrationTests
{
    [Fact]
    public async Task AuthenticatedHandshake_StoresTypedHeadersWithoutAuthenticationData()
    {
        await using var cluster = await IntegrationCluster
            .StartAsync(_ => Task.CompletedTask)
            .WaitAsync(TimeSpan.FromSeconds(10));

        var serverConnection = Assert.Single(cluster.Server.Connections);

        Assert.NotNull(cluster.Connection.Session.LocalHeaders.IPAddress);
        Assert.Null(cluster.Connection.Session.RemoteHeaders.AuthenticationData);

        Assert.Equal("test", serverConnection.Session.RemoteHeaders.Domain);
        Assert.Equal(PasswordAuthenticationProvider.ProtocolName,
            serverConnection.Session.RemoteHeaders.AuthenticationProtocol);
        Assert.Null(serverConnection.Session.RemoteHeaders.AuthenticationData);
    }

    [Fact]
    public async Task AuthenticatedHandshake_NegotiatesAesAndEncryptsApplicationTraffic()
    {
        await using var cluster = await IntegrationCluster
            .StartAsync(
                async warehouse =>
                {
                    await warehouse.Put("sys/encrypted", new Node { Id = 42 });
                },
                encrypted: true)
            .WaitAsync(TimeSpan.FromSeconds(10));

        var serverConnection = Assert.Single(cluster.Server.Connections);

        Assert.True(cluster.Connection.IsEncrypted);
        Assert.True(serverConnection.IsEncrypted);
        Assert.Equal(EncryptionMode.EncryptWithSessionKey,
                     cluster.Connection.Session.EncryptionMode);
        Assert.Equal(AesEncryptionProvider.Name,
                     cluster.Connection.Session.RemoteHeaders.CipherType);
        Assert.Equal(AesEncryptionProvider.Name,
                     serverConnection.Session.LocalHeaders.CipherType);
        Assert.NotNull(cluster.Connection.Session.SymetricCipher);
        Assert.NotNull(serverConnection.Session.SymetricCipher);
        Assert.Null(cluster.Connection.Session.LocalHeaders.CipherKey);
        Assert.Null(cluster.Connection.Session.RemoteHeaders.CipherKey);

        // Fetch crosses the protected record layer in both directions.
        Assert.NotNull(await Task.Run(async () =>
            await cluster.Connection.Get("sys/encrypted"))
            .WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task ServerRequiredEncryption_RejectsPlaintextWithoutDowngrade()
    {
        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await IntegrationCluster
                .StartAsync(
                    _ => Task.CompletedTask,
                    encrypted: false,
                    requireEncryption: true)
                .WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task DisallowedAuthenticationProvider_FailsClosedPromptly()
    {
        var exception = await Assert.ThrowsAnyAsync<Exception>(async () =>
            await IntegrationCluster
                .StartAsync(
                    _ => Task.CompletedTask,
                    allowAuthentication: false)
                .WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.IsNotType<TimeoutException>(exception);
    }

    [Fact]
    public async Task AllowlistedButUnregisteredAuthenticationProvider_FailsClosedPromptly()
    {
        var exception = await Assert.ThrowsAnyAsync<Exception>(async () =>
            await IntegrationCluster
                .StartAsync(
                    _ => Task.CompletedTask,
                    registerServerAuthenticationProvider: false)
                .WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.IsNotType<TimeoutException>(exception);
    }

    [Fact]
    public async Task AddressBoundEncryption_DerivesMatchingCiphersAcrossPeers()
    {
        await using var cluster = await IntegrationCluster
            .StartAsync(
                async warehouse =>
                {
                    await warehouse.Put("sys/address-bound", new Node { Id = 7 });
                },
                encrypted: true,
                encryptionMode: EncryptionMode.EncryptWithSessionKeyAndAddress)
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(cluster.Connection.IsEncrypted);
        Assert.Equal(EncryptionMode.EncryptWithSessionKeyAndAddress,
                     cluster.Connection.Session.EncryptionMode);
        Assert.NotNull(await Task.Run(async () =>
            await cluster.Connection.Get("sys/address-bound"))
            .WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task DisallowedEncryptionProvider_FailsWithoutPlaintextFallback()
    {
        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await IntegrationCluster
                .StartAsync(
                    _ => Task.CompletedTask,
                    encrypted: true,
                    allowEncryption: false)
                .WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task OneStepAuthentication_ConfirmsEncryptionBeforeFetchAndRpc()
    {
        await using var cluster = await IntegrationCluster
            .StartAsync(
                async warehouse =>
                {
                    await warehouse.Put("sys/one-step-encrypted", new EncryptedEchoResource());
                },
                encrypted: true,
                oneStepAuthentication: true)
            .WaitAsync(TimeSpan.FromSeconds(10));

        var remote = (EpResource)await Task.Run(async () =>
            await cluster.Connection.Get("sys/one-step-encrypted"))
            .WaitAsync(TimeSpan.FromSeconds(10));
        var function = remote.Instance.Definition
            .GetFunctionDefByName(nameof(EncryptedEchoResource.Echo));
        var result = await remote._Invoke(
            function.Index,
            new Map<byte, object> { [0] = 117 });

        Assert.True(cluster.Connection.Session.Authenticated);
        Assert.True(cluster.Connection.IsEncrypted);
        var serverConnection = Assert.Single(cluster.Server.Connections);
        Assert.True(serverConnection.Session.Authenticated);
        Assert.True(serverConnection.IsEncrypted);
        Assert.Equal(117, Convert.ToInt32(result));
    }

    [Fact]
    public async Task RequiredAuthenticationKeyRotation_CompletesBeforeConnectionsBecomeReady()
    {
        await using var cluster = await IntegrationCluster
            .StartAsync(
                async warehouse =>
                {
                    await warehouse.Put("sys/key-rotation", new EncryptedEchoResource());
                },
                encrypted: true,
                oneStepAuthentication: true,
                requireKeyRotation: true)
            .WaitAsync(TimeSpan.FromSeconds(10));

        var clientHandler = Assert.IsType<OneStepAuthenticationHandler>(
            cluster.Connection.Session.AuthenticationHandler);
        var serverConnection = Assert.Single(cluster.Server.Connections);
        var serverHandler = Assert.IsType<OneStepAuthenticationHandler>(
            serverConnection.Session.AuthenticationHandler);

        Assert.True(clientHandler.KeyRotationCompleted);
        Assert.True(serverHandler.KeyRotationCompleted);
        Assert.True(cluster.Connection.IsEncrypted);
        Assert.True(serverConnection.IsEncrypted);

        Assert.NotNull(await Task.Run(async () =>
            await cluster.Connection.Get("sys/key-rotation"))
            .WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task RequiredAuthenticationKeyRotation_RejectsUnencryptedSession()
    {
        var exception = await Assert.ThrowsAnyAsync<Exception>(async () =>
            await IntegrationCluster.StartAsync(
                    _ => Task.CompletedTask,
                    encrypted: false,
                    oneStepAuthentication: true,
                    requireKeyRotation: true)
                .WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.IsNotType<TimeoutException>(exception);
    }

    [Fact]
    public async Task WebSocketTransport_EncryptsFetchAndRpc()
    {
        await using var cluster = await IntegrationCluster
            .StartAsync(
                async warehouse =>
                {
                    await warehouse.Put("sys/websocket-encrypted", new EncryptedEchoResource());
                },
                encrypted: true,
                useWebSocket: true)
            .WaitAsync(TimeSpan.FromSeconds(10));

        var remote = (EpResource)await Task.Run(async () =>
            await cluster.Connection.Get("sys/websocket-encrypted"))
            .WaitAsync(TimeSpan.FromSeconds(10));
        var function = remote.Instance.Definition
            .GetFunctionDefByName(nameof(EncryptedEchoResource.Echo));
        var result = await remote._Invoke(
            function.Index,
            new Map<byte, object> { [0] = 203 });

        Assert.True(cluster.Connection.IsEncrypted);
        Assert.IsType<FrameworkWebSocket>(cluster.Connection.Socket);
        Assert.True(Assert.Single(cluster.Server.Connections).IsEncrypted);
        Assert.Equal(203, Convert.ToInt32(result));
    }

    [Fact]
    public async Task WrongAuthenticatedSessionKey_FailsPromptlyDuringKeyConfirmation()
    {
        var exception = await Assert.ThrowsAnyAsync<Exception>(async () =>
            await IntegrationCluster.StartAsync(
                    _ => Task.CompletedTask,
                    encrypted: true,
                    oneStepAuthentication: true,
                    mismatchedSessionKeys: true)
                .WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.IsNotType<TimeoutException>(exception);
    }

    [Fact]
    public async Task AddressBoundEncryption_RejectsWebSocketWithoutConcreteEndpoints()
    {
        var exception = await Assert.ThrowsAnyAsync<Exception>(async () =>
            await IntegrationCluster.StartAsync(
                    _ => Task.CompletedTask,
                    encrypted: true,
                    encryptionMode: EncryptionMode.EncryptWithSessionKeyAndAddress,
                    useWebSocket: true)
                .WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.IsNotType<TimeoutException>(exception);
    }
}
