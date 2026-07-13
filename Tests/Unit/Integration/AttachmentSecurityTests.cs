using Esiur.Core;
using Esiur.Protocol;

namespace Esiur.Tests.Unit.Integration;

[Collection("Integration")]
public class AttachmentSecurityTests
{
    [Fact]
    public async Task ConcurrentFetches_AreCoalescedIntoOneAttachRequest()
    {
        await using var cluster = await StartCluster().WaitAsync(TimeSpan.FromSeconds(10));

        var fetches = Enumerable.Range(0, 16)
            .Select(_ => Task.Run(async () => await cluster.Connection.Get("sys/first")))
            .ToArray();
        var resources = await Task.WhenAll(fetches).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.All(resources, resource => Assert.Same(resources[0], resource));
        Assert.Equal(1, cluster.Connection.ResourceAttachRequestCount);
    }

    [Fact]
    public async Task Connection_RefusesResourcesBeyondConfiguredAttachmentLimit()
    {
        await using var cluster = await StartCluster().WaitAsync(TimeSpan.FromSeconds(10));
        cluster.ClientWarehouse.Configuration.ResourceAttachments
            .MaximumAttachedResourcesPerConnection = 1;

        Assert.NotNull(await cluster.Connection.Get("sys/first"));

        var exception = await Assert.ThrowsAsync<AsyncException>(async () =>
            await cluster.Connection.Get("sys/second"));

        Assert.Equal(ExceptionCode.AttachmentLimitExceeded, exception.Code);
        Assert.Equal(1, cluster.Connection.ResourceAttachRequestCount);
    }

    static Task<IntegrationCluster> StartCluster()
        => IntegrationCluster.StartAsync(async warehouse =>
        {
            await warehouse.Put("sys/first", new RateLimitedResource());
            await warehouse.Put("sys/second", new RateLimitedResource());
        });
}
