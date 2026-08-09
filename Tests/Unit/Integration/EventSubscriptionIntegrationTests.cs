using Esiur.Protocol;
using Esiur.Resource;

namespace Esiur.Tests.Unit.Integration;

[Collection("Integration")]
public class EventSubscriptionIntegrationTests
{
    [Fact]
    public async Task PropertyAndEventNotifications_ShareOneOrderedResourceRevision()
    {
        await using var cluster = await StartClusterAsync(out var getBeacon).WaitAsync(TimeSpan.FromSeconds(10));
        var remote = await GetRemote(cluster);
        var beacon = getBeacon();
        var propertyRevisions = new List<ResourceCursor>();
        var eventRevisions = new List<ResourceCursor>();

        remote.Instance.PropertyModified += info => propertyRevisions.Add(info.Cursor);
        remote.Instance.EventOccurred += info => eventRevisions.Add(info.Cursor);
        await remote.OnAsync("Ping", _ => { });

        beacon.Fire("ping", "ordered");
        await WaitUntilAsync(
            () => propertyRevisions.Count == 1 && eventRevisions.Count == 1,
            TimeSpan.FromSeconds(3));

        Assert.Equal(propertyRevisions[0].Generation, eventRevisions[0].Generation);
        Assert.Equal(propertyRevisions[0].Revision + 1, eventRevisions[0].Revision);
        Assert.Equal(eventRevisions[0], remote.Instance.Cursor);
    }

    [Fact]
    public async Task QueryJournal_ReturnsHistoricalEventAfterCursor()
    {
        await using var cluster = await StartClusterAsync(out var getBeacon).WaitAsync(TimeSpan.FromSeconds(10));
        var remote = await GetRemote(cluster);
        var beacon = getBeacon();
        var attachedAt = remote.Instance.Cursor;
        var ping = remote.Instance.Definition.GetEventDefByName("Ping");

        beacon.Fire("ping", "retained");

        var page = await remote.QueryJournal(new ResourceJournalQuery
        {
            After = attachedAt,
            Kind = ResourceJournalEntryKind.EventOccurred,
            MemberIndex = ping.Index,
        });

        var entry = Assert.Single(page.Entries);
        Assert.False(page.CursorExpired);
        Assert.Equal(ResourceJournalEntryKind.EventOccurred, entry.Kind);
        Assert.Equal(ping.Index, entry.MemberIndex);
        Assert.Equal("retained", entry.Value);
        Assert.True(entry.Cursor.Revision > attachedAt.Revision);
    }

    [Fact]
    public async Task HistoricalSubscription_ReplaysOccurrenceMissedDuringReconnect()
    {
        await using var cluster = await StartClusterAsync(out var getBeacon).WaitAsync(TimeSpan.FromSeconds(10));
        cluster.Connection.AutoReconnect = true;
        cluster.Connection.ReconnectInterval = 1;
        var remote = await GetRemote(cluster);
        var beacon = getBeacon();
        var received = new List<string>();

        await remote.OnAsync("Ping", value => received.Add((string)value));
        beacon.Fire("ping", "before");
        await WaitUntilAsync(() => received.Count == 1, TimeSpan.FromSeconds(3));

        foreach (var serverConnection in cluster.Server.Connections.ToArray())
            serverConnection.Destroy();

        await WaitUntilAsync(() => !cluster.Connection.IsConnected, TimeSpan.FromSeconds(3));
        beacon.Fire("ping", "while-offline");
        await WaitUntilAsync(() => cluster.Connection.IsConnected, TimeSpan.FromSeconds(5));
        await WaitUntilAsync(() => received.Count == 2, TimeSpan.FromSeconds(5));

        Assert.Equal(new[] { "before", "while-offline" }, received);
    }

    [Fact]
    public async Task HistoricalSubscription_ReplaysEveryPageBeforeLiveDelivery()
    {
        const int occurrenceCount = 10_001;
        BeaconResource? beacon = null;
        await using var cluster = await IntegrationCluster.StartAsync(
            async warehouse =>
            {
                beacon = new BeaconResource();
                await warehouse.Put("sys/beacon", beacon);
            },
            resourceJournalCapacity: occurrenceCount + 1).WaitAsync(TimeSpan.FromSeconds(10));

        for (var index = 0; index < occurrenceCount; index++)
            beacon!.Fire("ping", index.ToString());

        var remote = await GetRemote(cluster);
        var received = new List<string>(occurrenceCount);
        await remote.OnFromAsync(
            "Ping",
            new ResourceCursor(remote.Instance.Generation, 0),
            value => received.Add((string)value));

        await WaitUntilAsync(
            () => received.Count == occurrenceCount,
            TimeSpan.FromSeconds(10));
        Assert.Equal("0", received[0]);
        Assert.Equal((occurrenceCount - 1).ToString(), received[^1]);
    }

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
    public async Task OnAsync_CompletesAfterWireSubscriptionIsReady()
    {
        await using var cluster = await StartClusterAsync(out var getBeacon).WaitAsync(TimeSpan.FromSeconds(10));
        var remote = await GetRemote(cluster);
        var beacon = getBeacon();
        var received = new List<string>();

        await remote.OnAsync("Ping", value => received.Add((string)value));
        beacon.Fire("ping", "immediate");

        await WaitUntilAsync(() => received.Count == 1, TimeSpan.FromSeconds(3));
        Assert.Equal(new[] { "immediate" }, received);
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
