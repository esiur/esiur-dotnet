using Esiur.Core;
using Esiur.Data;
using Esiur.Data.Types;
using Esiur.Protocol;
using Esiur.Resource;
using Esiur.Security.Authority;
using Esiur.Security.Permissions;

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

    [Fact]
    public async Task PropertyNotifications_RespectPerPropertyReadPermission()
    {
        PropertyBroadcastResource? source = null;
        await using var cluster = await IntegrationCluster.StartAsync(async warehouse =>
        {
            var permissions = new PropertyBroadcastPermissions();
            warehouse.RegisterManager(permissions);
            source = await warehouse.Put("sys/property-broadcast", new PropertyBroadcastResource());
            source.Instance!.Managers.Add(permissions);
        }).WaitAsync(TimeSpan.FromSeconds(10));

        var remote = (EpResource)await cluster.Connection.Get("sys/property-broadcast");
        var received = new List<string>();
        remote.Instance.PropertyModified += info => received.Add(info.PropertyDef.Name);

        source!.Secret = 7;
        source.Visible = 9;

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
        while (!received.Contains(nameof(PropertyBroadcastResource.Visible)) &&
               DateTime.UtcNow < deadline)
            await Task.Delay(20);

        Assert.Contains(nameof(PropertyBroadcastResource.Visible), received);
        Assert.DoesNotContain(nameof(PropertyBroadcastResource.Secret), received);
    }

    static Task<IntegrationCluster> StartCluster()
        => IntegrationCluster.StartAsync(async warehouse =>
        {
            await warehouse.Put("sys/first", new RateLimitedResource());
            await warehouse.Put("sys/second", new RateLimitedResource());
        });

    sealed class PropertyBroadcastPermissions : IPermissionsManager
    {
        public Map<string, object> Settings { get; } = new();

        public Ruling Applicable(
            IResource resource,
            Session session,
            ActionType action,
            MemberDef member,
            object inquirer = null!)
        {
            if (action == ActionType.Attach)
                return Ruling.Allowed;
            if (action == ActionType.GetProperty)
                return member?.Name == nameof(PropertyBroadcastResource.Secret)
                    ? Ruling.Denied
                    : Ruling.Allowed;
            return Ruling.DontCare;
        }

        public bool Initialize(Map<string, object> settings, IResource resource) => true;
    }
}

[Resource]
public partial class PropertyBroadcastResource
{
    [Export] int secret;
    [Export] int visible;
}
