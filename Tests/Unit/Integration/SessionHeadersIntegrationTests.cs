namespace Esiur.Tests.Unit.Integration;

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
        Assert.Equal("hash", serverConnection.Session.RemoteHeaders.AuthenticationProtocol);
        Assert.Null(serverConnection.Session.RemoteHeaders.AuthenticationData);
    }
}
