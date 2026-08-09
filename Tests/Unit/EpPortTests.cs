using Esiur.Protocol;
using Esiur.Resource;

namespace Esiur.Tests.Unit;

public sealed class EpPortTests
{
    [Fact]
    public void ServerUsesTheProtocolDefaultPort()
        => Assert.Equal(EpProtocol.DefaultPort, new EpServer().Port);

    [Fact]
    public void ClientEndpointWithoutPortUsesTheProtocolDefault()
    {
        var endpoint = new Uri("ep://localhost/sys/resource");
        Assert.Equal(EpProtocol.DefaultPort, EpConnection.ResolveEndpointPort(endpoint));
    }

    [Fact]
    public void ExplicitClientPortStillOverridesTheProtocolDefault()
        => Assert.Equal(
            12345,
            EpConnection.ResolveEndpointPort(new Uri("ep://localhost:12345/sys/resource")));
}
