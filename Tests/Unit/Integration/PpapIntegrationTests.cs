using Esiur.Security.Authority;
using Esiur.Security.Authority.Providers.Ppap;

namespace Esiur.Tests.Unit.Integration;

[Collection("Integration")]
public class PpapIntegrationTests
{
    const string Domain = "ppap.test";
    const string Identity = "alice";
    const string Password = "correct horse battery staple";
    const string ServerIdentity = "server";
    const string ServerPassword = "server paper password";

    static readonly PpapKdfProfile TestKdf = new(
        PpapKdfProfile.Argon2Version13,
        memoryKiB: 8 * 1024,
        iterations: 1,
        parallelism: 1);

    [Fact]
    public async Task InitializerPassword_AuthenticatesWithAesAndRotatesOnlyAfterEncryption()
    {
        using var localIdentity = PpapLocalIdentity.FromPassword(
            Identity,
            Password,
            TestKdf);
        var initial = PpapRegistrationRecord.FromLocalIdentity(Domain, localIdentity);
        var serverStore = new ObservedPpapRegistrationStore();
        Assert.True(serverStore.TryAdd(Domain, initial));

        var clientProvider = new PpapAuthenticationProvider(
            localIdentity,
            new InMemoryPpapRegistrationStore());
        var serverProvider = new PpapAuthenticationProvider(
            localIdentity: null!,
            registrations: serverStore);
        await using var cluster = await CustomAuthenticationIntegrationCluster.StartAsync(
                serverProvider,
                clientProvider,
                encrypted: true,
                populate: async warehouse =>
                    await warehouse.Put("sys/ppap-rotated", new Node { Id = 313 }),
                authenticationMode: AuthenticationMode.InitializerIdentity,
                identity: Identity,
                domain: Domain,
                serverCreated: value =>
                {
                    serverStore.EncryptionProbe = () =>
                        value.Connections.SingleOrDefault()?.IsEncrypted == true;
                })
            .WaitAsync(TimeSpan.FromSeconds(15));

        var serverConnection = Assert.Single(cluster.Server.Connections);
        var rotated = Assert.IsType<PpapRegistrationRecord>(
            serverStore.Get(Domain, Identity));

        Assert.True(cluster.Connection.Session.Authenticated);
        Assert.True(serverConnection.Session.Authenticated);
        Assert.True(cluster.Connection.IsEncrypted);
        Assert.True(serverConnection.IsEncrypted);
        Assert.Equal(PpapProtocol.Name,
            cluster.Connection.Session.AuthenticationHandler.Protocol);
        Assert.Equal(PpapProtocol.SessionKeyLength,
            cluster.Connection.Session.Key.Length);
        Assert.Equal(cluster.Connection.Session.Key, serverConnection.Session.Key);

        Assert.True(serverStore.EncryptionObservedAtSuccessfulRotation);
        Assert.Equal(initial.Version + 1, rotated.Version);
        Assert.False(initial.Nonce.SequenceEqual(rotated.Nonce));
        Assert.False(initial.EncapsulationKey.SequenceEqual(rotated.EncapsulationKey));

        Assert.NotNull(await Task.Run(async () =>
                await cluster.Connection.Get("sys/ppap-rotated"))
            .WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task InitializerPassword_OmittedDomainDefaultsToHostnameAndRotates()
    {
        const string hostnameDomain = "localhost";
        using var localIdentity = PpapLocalIdentity.FromPassword(
            Identity,
            Password,
            TestKdf);
        var initial = PpapRegistrationRecord.FromLocalIdentity(
            hostnameDomain,
            localIdentity);
        var serverStore = new ObservedPpapRegistrationStore();
        Assert.True(serverStore.TryAdd(hostnameDomain, initial));

        var clientProvider = new PpapAuthenticationProvider(
            localIdentity,
            new InMemoryPpapRegistrationStore());
        var serverProvider = new PpapAuthenticationProvider(
            localIdentity: null!,
            registrations: serverStore);
        await using var cluster = await CustomAuthenticationIntegrationCluster.StartAsync(
                serverProvider,
                clientProvider,
                encrypted: true,
                authenticationMode: AuthenticationMode.InitializerIdentity,
                identity: Identity,
                domain: null,
                serverCreated: value =>
                {
                    serverStore.EncryptionProbe = () =>
                        value.Connections.SingleOrDefault()?.IsEncrypted == true;
                })
            .WaitAsync(TimeSpan.FromSeconds(15));

        var serverConnection = Assert.Single(cluster.Server.Connections);
        var rotated = Assert.IsType<PpapRegistrationRecord>(
            serverStore.Get(hostnameDomain, Identity));

        Assert.Equal(hostnameDomain, cluster.Connection.RemoteDomain);
        Assert.True(cluster.Connection.Session.Authenticated);
        Assert.True(serverConnection.Session.Authenticated);
        Assert.True(cluster.Connection.IsEncrypted);
        Assert.True(serverConnection.IsEncrypted);
        Assert.True(serverStore.EncryptionObservedAtSuccessfulRotation);
        Assert.Equal(initial.Version + 1, rotated.Version);
        Assert.False(initial.Nonce.SequenceEqual(rotated.Nonce));
        Assert.False(initial.EncapsulationKey.SequenceEqual(rotated.EncapsulationKey));
    }

    [Fact]
    public async Task ResponderPassword_AuthenticatesWithAesAndRotatesResponderRegistration()
    {
        using var serverIdentity = PpapLocalIdentity.FromPassword(
            ServerIdentity,
            ServerPassword,
            TestKdf);
        var initial = PpapRegistrationRecord.FromLocalIdentity(
            Domain,
            serverIdentity);
        var clientStore = new ObservedPpapRegistrationStore();
        Assert.True(clientStore.TryAdd(Domain, initial));

        var clientProvider = new PpapAuthenticationProvider(
            localIdentity: null!,
            registrations: clientStore);
        var serverProvider = new PpapAuthenticationProvider(
            serverIdentity,
            new InMemoryPpapRegistrationStore());
        await using var cluster = await CustomAuthenticationIntegrationCluster.StartAsync(
                serverProvider,
                clientProvider,
                encrypted: true,
                authenticationMode: AuthenticationMode.ResponderIdentity,
                identity: null,
                responderIdentity: ServerIdentity,
                domain: Domain,
                serverCreated: value =>
                {
                    clientStore.EncryptionProbe = () =>
                        value.Connections.SingleOrDefault()?.IsEncrypted == true;
                })
            .WaitAsync(TimeSpan.FromSeconds(15));

        var serverConnection = Assert.Single(cluster.Server.Connections);
        var rotated = Assert.IsType<PpapRegistrationRecord>(
            clientStore.Get(Domain, ServerIdentity));

        AssertEncryptedAndAuthenticated(cluster.Connection, serverConnection);
        Assert.Null(cluster.Connection.Session.LocalIdentity);
        Assert.Equal(ServerIdentity, cluster.Connection.Session.RemoteIdentity);
        Assert.Equal(ServerIdentity, serverConnection.Session.LocalIdentity);
        Assert.Null(serverConnection.Session.RemoteIdentity);
        Assert.True(clientStore.EncryptionObservedAtSuccessfulRotation);
        AssertRotated(initial, rotated);
    }

    [Fact]
    public async Task DualPassword_AuthenticatesWithAesAndRotatesBothRegistrations()
    {
        using var clientIdentity = PpapLocalIdentity.FromPassword(
            Identity,
            Password,
            TestKdf);
        using var serverIdentity = PpapLocalIdentity.FromPassword(
            ServerIdentity,
            ServerPassword,
            TestKdf);
        var initialClient = PpapRegistrationRecord.FromLocalIdentity(
            Domain,
            clientIdentity);
        var initialServer = PpapRegistrationRecord.FromLocalIdentity(
            Domain,
            serverIdentity);
        var serverStore = new ObservedPpapRegistrationStore();
        var clientStore = new ObservedPpapRegistrationStore();
        Assert.True(serverStore.TryAdd(Domain, initialClient));
        Assert.True(clientStore.TryAdd(Domain, initialServer));

        var clientProvider = new PpapAuthenticationProvider(clientIdentity, clientStore);
        var serverProvider = new PpapAuthenticationProvider(serverIdentity, serverStore);
        await using var cluster = await CustomAuthenticationIntegrationCluster.StartAsync(
                serverProvider,
                clientProvider,
                encrypted: true,
                authenticationMode: AuthenticationMode.DualIdentity,
                identity: Identity,
                responderIdentity: ServerIdentity,
                domain: Domain,
                serverCreated: value =>
                {
                    Func<bool> probe = () =>
                        value.Connections.SingleOrDefault()?.IsEncrypted == true;
                    serverStore.EncryptionProbe = probe;
                    clientStore.EncryptionProbe = probe;
                })
            .WaitAsync(TimeSpan.FromSeconds(15));

        var serverConnection = Assert.Single(cluster.Server.Connections);
        AssertEncryptedAndAuthenticated(cluster.Connection, serverConnection);
        Assert.Equal(Identity, cluster.Connection.Session.LocalIdentity);
        Assert.Equal(ServerIdentity, cluster.Connection.Session.RemoteIdentity);
        Assert.Equal(ServerIdentity, serverConnection.Session.LocalIdentity);
        Assert.Equal(Identity, serverConnection.Session.RemoteIdentity);
        Assert.True(serverStore.EncryptionObservedAtSuccessfulRotation);
        Assert.True(clientStore.EncryptionObservedAtSuccessfulRotation);
        AssertRotated(initialClient, serverStore.Get(Domain, Identity));
        AssertRotated(initialServer, clientStore.Get(Domain, ServerIdentity));
    }

    [Fact]
    public async Task DualMixedIdentity_RotatesOnlyPasswordRegistration()
    {
        using var clientIdentity = PpapLocalIdentity.CreateStatic(Identity);
        using var serverIdentity = PpapLocalIdentity.FromPassword(
            ServerIdentity,
            ServerPassword,
            TestKdf);
        var initialClient = PpapRegistrationRecord.FromLocalIdentity(
            Domain,
            clientIdentity);
        var initialServer = PpapRegistrationRecord.FromLocalIdentity(
            Domain,
            serverIdentity);
        var serverStore = new ObservedPpapRegistrationStore();
        var clientStore = new ObservedPpapRegistrationStore();
        Assert.True(serverStore.TryAdd(Domain, initialClient));
        Assert.True(clientStore.TryAdd(Domain, initialServer));

        var clientProvider = new PpapAuthenticationProvider(clientIdentity, clientStore);
        var serverProvider = new PpapAuthenticationProvider(serverIdentity, serverStore);
        await using var cluster = await CustomAuthenticationIntegrationCluster.StartAsync(
                serverProvider,
                clientProvider,
                encrypted: true,
                authenticationMode: AuthenticationMode.DualIdentity,
                identity: Identity,
                responderIdentity: ServerIdentity,
                domain: Domain,
                serverCreated: value =>
                {
                    Func<bool> probe = () =>
                        value.Connections.SingleOrDefault()?.IsEncrypted == true;
                    serverStore.EncryptionProbe = probe;
                    clientStore.EncryptionProbe = probe;
                })
            .WaitAsync(TimeSpan.FromSeconds(15));

        var serverConnection = Assert.Single(cluster.Server.Connections);
        AssertEncryptedAndAuthenticated(cluster.Connection, serverConnection);
        var unchangedClient = serverStore.Get(Domain, Identity);
        Assert.Equal(initialClient.Version, unchangedClient.Version);
        Assert.Equal(initialClient.EncapsulationKey, unchangedClient.EncapsulationKey);
        Assert.False(serverStore.EncryptionObservedAtSuccessfulRotation);
        Assert.True(clientStore.EncryptionObservedAtSuccessfulRotation);
        AssertRotated(initialServer, clientStore.Get(Domain, ServerIdentity));
    }

    static void AssertEncryptedAndAuthenticated(
        Esiur.Protocol.EpConnection client,
        Esiur.Protocol.EpConnection server)
    {
        Assert.True(client.Session.Authenticated);
        Assert.True(server.Session.Authenticated);
        Assert.True(client.IsEncrypted);
        Assert.True(server.IsEncrypted);
        Assert.Equal(client.Session.Key, server.Session.Key);
    }

    static void AssertRotated(
        PpapRegistrationRecord initial,
        PpapRegistrationRecord rotated)
    {
        Assert.NotNull(rotated);
        Assert.Equal(initial.Version + 1, rotated.Version);
        Assert.False(initial.Nonce.SequenceEqual(rotated.Nonce));
        Assert.False(initial.EncapsulationKey.SequenceEqual(rotated.EncapsulationKey));
    }
}

internal sealed class ObservedPpapRegistrationStore : IPpapRegistrationStore
{
    readonly InMemoryPpapRegistrationStore _inner = new();
    int _encryptionObservedAtSuccessfulRotation;

    public Func<bool>? EncryptionProbe { get; set; }

    public bool EncryptionObservedAtSuccessfulRotation
        => Volatile.Read(ref _encryptionObservedAtSuccessfulRotation) != 0;

    public bool TryAdd(string domain, PpapRegistrationRecord record)
        => _inner.TryAdd(domain, record);

    public PpapRegistrationRecord Get(string domain, string identity)
        => _inner.Get(domain, identity);

    public PpapRegistrationRecord ResolveMasked(
        string domain,
        byte[] mask,
        byte[] maskKey,
        byte[] maskedIdentity)
        => _inner.ResolveMasked(domain, mask, maskKey, maskedIdentity);

    public bool TryRotate(
        string domain,
        string identity,
        long expectedVersion,
        PpapRegistrationRecord replacement)
    {
        var encrypted = EncryptionProbe?.Invoke() == true;
        var rotated = _inner.TryRotate(
            domain,
            identity,
            expectedVersion,
            replacement);
        if (rotated && encrypted)
            Interlocked.Exchange(ref _encryptionObservedAtSuccessfulRotation, 1);
        return rotated;
    }
}
