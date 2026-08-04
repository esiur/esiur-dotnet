using Esiur.Protocol;
using Esiur.Resource;

namespace Esiur.Tests.Unit;

public sealed class EpPortTests
{
    [Fact]
    public void ServerDoesNotAssignAProtocolPort()
        => Assert.Equal(0, new EpServer().Port);

    [Fact]
    public async Task ClientEndpointWithoutPortIsRejected()
    {
        var warehouse = new Warehouse();

        var exception = await Assert.ThrowsAsync<FormatException>(async () =>
            await warehouse.Get<EpConnection>("ep://localhost/sys/resource"));

        Assert.Contains("explicit port", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
