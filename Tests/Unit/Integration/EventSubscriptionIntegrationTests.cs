using Esiur.Protocol;
using Esiur.Resource;

namespace Esiur.Tests.Unit.Integration;

[Collection("Integration")]
public class EventSubscriptionIntegrationTests
{
    [Fact]
    public async Task On_DeliversAutoDeliveredEventWithNoSubscribeNeeded()
    {
        await using var cluster = await StartClusterAsync(out var getBeacon).WaitAsync(TimeSpan.FromSeconds(10));
        var remote = await GetRemote(cluster);
        var beacon = getBeacon();

        var received = new List<string>();
        remote.On("Tick", v => received.Add((string)v));

        beacon.Fire("tick", "a");
        await WaitUntilAsync(() => received.Count == 1, TimeSpan.FromSeconds(3));

        Assert.Equal(new[] { "a" }, received);
    }

    [Fact]
    public async Task On_RefCountsListeners_OffOnlyUnsubscribesAtZero()
    {
        await using var cluster = await StartClusterAsync(out var getBeacon).WaitAsync(TimeSpan.FromSeconds(10));
        var remote = await GetRemote(cluster);
        var beacon = getBeacon();

        var a = new List<string>();
        var b = new List<string>();
        void CbA(object v) => a.Add((string)v);
        void CbB(object v) => b.Add((string)v);

        remote.On("Ping", CbA);
        // Give the wire Subscribe request time to round-trip before firing.
        await Task.Delay(200);

        remote.On("Ping", CbB); // 2nd listener on an already-subscribed event
        await Task.Delay(100);

        beacon.Fire("ping", "x");
        await WaitUntilAsync(() => a.Count == 1 && b.Count == 1, TimeSpan.FromSeconds(3));

        remote.Off("Ping", CbA); // one listener remains — must not unsubscribe yet
        await Task.Delay(100);

        beacon.Fire("ping", "y");
        await WaitUntilAsync(() => b.Count == 2, TimeSpan.FromSeconds(3));
        Assert.Equal(new[] { "x" }, a); // CbA got nothing after being removed

        remote.Off("Ping", CbB); // last listener — now it should unsubscribe
        await Task.Delay(200);

        beacon.Fire("ping", "z");
        await Task.Delay(200);
        Assert.Equal(new[] { "x" }, a);
        Assert.Equal(new[] { "x", "y" }, b); // neither received "z"
    }

    [Fact]
    public async Task NativePlusEquals_AutoSubscribesAndUnsubscribes()
    {
        // `r.Ping += handler` is the built-in, idiomatic C# way to listen —
        // TrySetMember already combines this into a proper multicast
        // delegate; what's new is that it now also drives Subscribe/Unsubscribe.
        await using var cluster = await StartClusterAsync(out var getBeacon).WaitAsync(TimeSpan.FromSeconds(10));
        var remote = await GetRemote(cluster);
        var beacon = getBeacon();
        dynamic dyn = remote;

        var received = new List<string>();
        EpResourceEvent handler = (_, arg) => received.Add((string)arg);

        dyn.Ping += handler;
        await Task.Delay(200); // let the wire Subscribe request round-trip

        beacon.Fire("ping", "x");
        await WaitUntilAsync(() => received.Count == 1, TimeSpan.FromSeconds(3));
        Assert.Equal(new[] { "x" }, received);

        dyn.Ping -= handler;
        await Task.Delay(200); // let the wire Unsubscribe request round-trip

        beacon.Fire("ping", "y");
        await Task.Delay(200);
        Assert.Equal(new[] { "x" }, received); // "y" never arrives once unsubscribed
    }

    [Fact]
    public async Task On_AndNativePlusEquals_ComposeCorrectly()
    {
        // `On()`'s ref-counted list and `+=`'s multicast delegate are two
        // independent subscriber sources for the same wire subscription —
        // either being non-empty must keep it subscribed.
        await using var cluster = await StartClusterAsync(out var getBeacon).WaitAsync(TimeSpan.FromSeconds(10));
        var remote = await GetRemote(cluster);
        var beacon = getBeacon();
        dynamic dyn = remote;

        var onReceived = new List<string>();
        var plusReceived = new List<string>();
        void OnCb(object v) => onReceived.Add((string)v);
        EpResourceEvent plusHandler = (_, arg) => plusReceived.Add((string)arg);

        remote.On("Ping", OnCb);
        await Task.Delay(200);

        dyn.Ping += plusHandler; // 2nd subscriber via the other mechanism
        await Task.Delay(100);

        beacon.Fire("ping", "x");
        await WaitUntilAsync(() => onReceived.Count == 1 && plusReceived.Count == 1, TimeSpan.FromSeconds(3));

        remote.Off("Ping", OnCb); // On()'s listener gone, but += subscriber remains
        await Task.Delay(100);

        beacon.Fire("ping", "y");
        await WaitUntilAsync(() => plusReceived.Count == 2, TimeSpan.FromSeconds(3));
        Assert.Equal(new[] { "x" }, onReceived); // no "y" — Off() already removed it

        dyn.Ping -= plusHandler; // last subscriber — now it should unsubscribe
        await Task.Delay(200);

        beacon.Fire("ping", "z");
        await Task.Delay(200);
        Assert.Equal(new[] { "x", "y" }, plusReceived); // neither got "z"
    }

    [Fact]
    public async Task On_ResubscribesAfterAutomaticReconnect()
    {
        await using var cluster = await StartClusterAsync(out var getBeacon).WaitAsync(TimeSpan.FromSeconds(10));
        cluster.Connection.AutoReconnect = true;
        cluster.Connection.ReconnectInterval = 1;
        var remote = await GetRemote(cluster);
        var beacon = getBeacon();

        var received = new List<string>();
        remote.On("Ping", v => received.Add((string)v));
        await Task.Delay(200); // let the initial Subscribe land

        beacon.Fire("ping", "before");
        await WaitUntilAsync(() => received.Count == 1, TimeSpan.FromSeconds(3));

        // Simulate an unexpected disconnect — the server-side subscription
        // state (keyed to the now-dead connection) is gone, but the client's
        // local `On()` listener is untouched, so it still believes it's
        // subscribed unless _Reattach's ReconcileAllSubscriptions() resets that.
        foreach (var serverConnection in cluster.Server.Connections.ToArray())
            serverConnection.Destroy();

        await WaitUntilAsync(() => !cluster.Connection.IsConnected, TimeSpan.FromSeconds(3));
        await WaitUntilAsync(() => cluster.Connection.IsConnected, TimeSpan.FromSeconds(5));
        await Task.Delay(300); // let the post-reattach resubscribe land

        beacon.Fire("ping", "after");
        await WaitUntilAsync(() => received.Count == 2, TimeSpan.FromSeconds(3));
        Assert.Equal(new[] { "before", "after" }, received);
    }

    [Fact]
    public async Task On_PropertyPrefix_ListensWithNoWireSubscription()
    {
        await using var cluster = await StartClusterAsync(out var getBeacon).WaitAsync(TimeSpan.FromSeconds(10));
        var remote = await GetRemote(cluster);
        var beacon = getBeacon();

        var seen = new List<object>();
        remote.On(":Pings", v => seen.Add(v));

        beacon.Fire("tick", "z");
        await WaitUntilAsync(() => seen.Count == 1, TimeSpan.FromSeconds(3));

        Assert.Equal(1, Convert.ToInt32(seen[0]));
    }

    static Task<IntegrationCluster> StartClusterAsync(out Func<BeaconResource> getBeacon)
    {
        BeaconResource? beacon = null;
        var clusterTask = IntegrationCluster.StartAsync(async warehouse =>
        {
            beacon = new BeaconResource();
            await warehouse.Put("sys/beacon", beacon);
        });
        getBeacon = () => beacon!;
        return clusterTask;
    }

    static async Task<EpResource> GetRemote(IntegrationCluster cluster)
        => (EpResource)await Task.Run(async () =>
            await cluster.Connection.Get("sys/beacon"))
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
}
