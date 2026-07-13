using Esiur.Core;
using Esiur.Data;
using Esiur.Data.Types;
using Esiur.Protocol;
using Esiur.Resource;
using Esiur.Security.Authority;
using Esiur.Security.Permissions;
using Esiur.Security.RateLimiting;
using System.Diagnostics;

namespace Esiur.Tests.Unit.Integration;

[Collection("Integration")]
public class RateControlIntegrationTests
{
    [Fact]
    public async Task FunctionCalls_AreDeniedWhenPolicyIsExhausted()
    {
        await using var cluster = await StartCluster().WaitAsync(TimeSpan.FromSeconds(10));
        var remote = await GetRemote(cluster);
        var function = remote.Instance.Definition.GetFunctionDefByName(nameof(RateLimitedResource.Call));

        var first = await remote._Invoke(function.Index, Array.Empty<object>());
        var exception = await Assert.ThrowsAsync<AsyncException>(async () =>
            await remote._Invoke(function.Index, Array.Empty<object>()));

        Assert.Equal(1, Convert.ToInt32(first));
        Assert.Equal(ExceptionCode.RateLimitExceeded, exception.Code);
    }

    [Fact]
    public async Task PropertySets_AreDeniedWhenPolicyIsExhausted()
    {
        await using var cluster = await StartCluster().WaitAsync(TimeSpan.FromSeconds(10));
        var remote = await GetRemote(cluster);
        var property = remote.Instance.Definition.GetPropertyDefByName(nameof(RateLimitedResource.Value));

        await remote.SetResourcePropertyAsync(property.Index, 10);
        var exception = await Assert.ThrowsAsync<AsyncException>(async () =>
            await remote.SetResourcePropertyAsync(property.Index, 20));

        Assert.Equal(ExceptionCode.RateLimitExceeded, exception.Code);
    }

    [Fact]
    public async Task RepeatedDenials_BlockTheConnection()
    {
        await using var cluster = await StartCluster(denialsBeforeBlock: 1)
            .WaitAsync(TimeSpan.FromSeconds(10));
        cluster.Connection.AutoReconnect = false;
        var remote = await GetRemote(cluster);
        var function = remote.Instance.Definition.GetFunctionDefByName(nameof(RateLimitedResource.Call));

        await remote._Invoke(function.Index, Array.Empty<object>());
        var exception = await Assert.ThrowsAsync<AsyncException>(async () =>
            await remote._Invoke(function.Index, Array.Empty<object>()));

        Assert.Equal(ExceptionCode.RateLimitExceeded, exception.Code);
        await WaitUntilAsync(() => !cluster.Connection.IsConnected, TimeSpan.FromSeconds(3));
        Assert.False(cluster.Connection.IsConnected);
    }

    [Fact]
    public async Task QueuedCalls_AreDelayedAndQueueOverflowIsDenied()
    {
        await using var cluster = await StartCluster(
            queueLimit: 1,
            period: TimeSpan.FromMilliseconds(250)).WaitAsync(TimeSpan.FromSeconds(10));
        var remote = await GetRemote(cluster);
        var function = remote.Instance.Definition.GetFunctionDefByName(nameof(RateLimitedResource.Call));

        await remote._Invoke(function.Index, Array.Empty<object>());

        var stopwatch = Stopwatch.StartNew();
        var queued = remote._Invoke(function.Index, Array.Empty<object>());
        var exception = await Assert.ThrowsAsync<AsyncException>(async () =>
            await remote._Invoke(function.Index, Array.Empty<object>()));
        var result = await queued;

        Assert.Equal(ExceptionCode.RateLimitExceeded, exception.Code);
        Assert.Equal(2, Convert.ToInt32(result));
        Assert.True(stopwatch.Elapsed >= TimeSpan.FromMilliseconds(150));
    }

    static Task<IntegrationCluster> StartCluster(
        int denialsBeforeBlock = 10,
        int queueLimit = 0,
        TimeSpan? period = null)
        => IntegrationCluster.StartAsync(async warehouse =>
        {
            warehouse.Configuration.RateControl.DenialsBeforeConnectionBlock = denialsBeforeBlock;
            warehouse.Configuration.RateControl.ConnectionBlockDelay = TimeSpan.FromMilliseconds(100);

            warehouse.AddRatePolicy(new BurstRatePolicy("standard-call")
            {
                PermitLimit = 1,
                Period = period ?? TimeSpan.FromMinutes(1),
                QueueLimit = queueLimit,
            });
            warehouse.AddRatePolicy(new BurstRatePolicy("standard-set")
            {
                PermitLimit = 1,
                Period = period ?? TimeSpan.FromMinutes(1),
                QueueLimit = queueLimit,
            });

            var resource = await warehouse.Put("sys/rate", new RateLimitedResource());
            resource.Instance!.Managers.Add(new AllowPropertySetPermissions());
        });

    static async Task<EpResource> GetRemote(IntegrationCluster cluster)
        => await Task.Run(async () =>
            (EpResource)await cluster.Connection.Get("sys/rate"))
            .WaitAsync(TimeSpan.FromSeconds(10));

    static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException("The expected condition was not reached.");
            await Task.Delay(20);
        }
    }

    sealed class AllowPropertySetPermissions : IPermissionsManager
    {
        public Map<string, object> Settings { get; } = new();

        public Ruling Applicable(
            IResource resource,
            Session session,
            ActionType action,
            MemberDef member,
            object inquirer = null!)
            => action == ActionType.SetProperty ? Ruling.Allowed : Ruling.DontCare;

        public bool Initialize(Map<string, object> settings, IResource resource) => true;
    }
}
