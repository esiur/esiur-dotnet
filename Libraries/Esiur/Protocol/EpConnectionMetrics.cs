using System;
using System.Threading;

namespace Esiur.Protocol;

/// <summary>
/// Opt-in diagnostics for one EP connection. Esiur allocates no collector and
/// performs no metric increments until <see cref="EpConnection.EnableRuntimeMetrics"/>
/// is called.
/// </summary>
public sealed class EpConnectionRuntimeMetricsSnapshot
{
    public bool Enabled { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime SampledAt { get; set; }
    public double MonitoringDurationMilliseconds { get; set; }
    public bool Connected { get; set; }
    public bool Authenticated { get; set; }
    public bool Encrypted { get; set; }
    public long SentBytes { get; set; }
    public long ReceivedBytes { get; set; }
    public long SentPackets { get; set; }
    public long ReceivedPackets { get; set; }
    public long SentRequests { get; set; }
    public long ReceivedRequests { get; set; }
    public long SentReplies { get; set; }
    public long ReceivedReplies { get; set; }
    public long SentNotifications { get; set; }
    public long ReceivedNotifications { get; set; }
    public long SentPropertyModifications { get; set; }
    public long ReceivedPropertyModifications { get; set; }
    public long SentEvents { get; set; }
    public long ReceivedEvents { get; set; }
    public long SentErrors { get; set; }
    public long ReceivedErrors { get; set; }
    public double BytesPerSecond { get; set; }
    public double PacketsPerSecond { get; set; }
    public double NotificationsPerSecond { get; set; }
    public double PropertyModificationsPerSecond { get; set; }
    public double EventsPerSecond { get; set; }
    public int AttachedResources { get; set; }
    public int LocalResourceSubscriptions { get; set; }
    public int EventSubscriptions { get; set; }
    public int PendingRequests { get; set; }
    public int PendingResourceAttachments { get; set; }
    public int PendingTypeDefinitions { get; set; }
    public int CachedTypeDefinitions { get; set; }
    public int NeededResources { get; set; }
    public int NeededTypeDefinitions { get; set; }
    public int ActiveInvocations { get; set; }
    public int QueuedNotificationWork { get; set; }
    public uint RemoteMaximumPacketSize { get; set; }
    public uint RemoteMaximumAllocationSize { get; set; }
    public int RemoteMaximumCollectionItems { get; set; }
    public int RemoteMaximumTypeMetadataDepth { get; set; }
    public uint RemoteMaximumEncryptedRecordSize { get; set; }
}

sealed class EpConnectionMetricsCollector
{
    internal readonly DateTime StartedAt = DateTime.UtcNow;
    internal long SentBytes, ReceivedBytes, SentPackets, ReceivedPackets;
    internal long SentRequests, ReceivedRequests, SentReplies, ReceivedReplies;
    internal long SentNotifications, ReceivedNotifications;
    internal long SentPropertyModifications, ReceivedPropertyModifications;
    internal long SentEvents, ReceivedEvents, SentErrors, ReceivedErrors;
    readonly object sampleLock = new();
    DateTime previousSampledAt;
    long previousBytes, previousPackets, previousNotifications, previousProperties, previousEvents;

    internal EpConnectionMetricsCollector() => previousSampledAt = StartedAt;

    internal EpConnectionRuntimeMetricsSnapshot Snapshot(EpConnectionRuntimeMetricsSnapshot state)
    {
        lock (sampleLock)
        {
            var sampledAt = DateTime.UtcNow;
            var seconds = Math.Max((sampledAt - previousSampledAt).TotalSeconds, 0.001);
            var sentBytes = Interlocked.Read(ref SentBytes);
            var receivedBytes = Interlocked.Read(ref ReceivedBytes);
            var sentPackets = Interlocked.Read(ref SentPackets);
            var receivedPackets = Interlocked.Read(ref ReceivedPackets);
            var sentNotifications = Interlocked.Read(ref SentNotifications);
            var receivedNotifications = Interlocked.Read(ref ReceivedNotifications);
            var sentProperties = Interlocked.Read(ref SentPropertyModifications);
            var receivedProperties = Interlocked.Read(ref ReceivedPropertyModifications);
            var sentEvents = Interlocked.Read(ref SentEvents);
            var receivedEvents = Interlocked.Read(ref ReceivedEvents);
            var totalBytes = sentBytes + receivedBytes;
            var totalPackets = sentPackets + receivedPackets;
            var totalNotifications = sentNotifications + receivedNotifications;
            var totalProperties = sentProperties + receivedProperties;
            var totalEvents = sentEvents + receivedEvents;

            var snapshot = new EpConnectionRuntimeMetricsSnapshot
            {
                Enabled = true,
                StartedAt = StartedAt,
                SampledAt = sampledAt,
                MonitoringDurationMilliseconds = (sampledAt - StartedAt).TotalMilliseconds,
                Connected = state.Connected,
                Authenticated = state.Authenticated,
                Encrypted = state.Encrypted,
                SentBytes = sentBytes,
                ReceivedBytes = receivedBytes,
                SentPackets = sentPackets,
                ReceivedPackets = receivedPackets,
                SentRequests = Interlocked.Read(ref SentRequests),
                ReceivedRequests = Interlocked.Read(ref ReceivedRequests),
                SentReplies = Interlocked.Read(ref SentReplies),
                ReceivedReplies = Interlocked.Read(ref ReceivedReplies),
                SentNotifications = sentNotifications,
                ReceivedNotifications = receivedNotifications,
                SentPropertyModifications = sentProperties,
                ReceivedPropertyModifications = receivedProperties,
                SentEvents = sentEvents,
                ReceivedEvents = receivedEvents,
                SentErrors = Interlocked.Read(ref SentErrors),
                ReceivedErrors = Interlocked.Read(ref ReceivedErrors),
                BytesPerSecond = (totalBytes - previousBytes) / seconds,
                PacketsPerSecond = (totalPackets - previousPackets) / seconds,
                NotificationsPerSecond = (totalNotifications - previousNotifications) / seconds,
                PropertyModificationsPerSecond = (totalProperties - previousProperties) / seconds,
                EventsPerSecond = (totalEvents - previousEvents) / seconds,
                AttachedResources = state.AttachedResources,
                LocalResourceSubscriptions = state.LocalResourceSubscriptions,
                EventSubscriptions = state.EventSubscriptions,
                PendingRequests = state.PendingRequests,
                PendingResourceAttachments = state.PendingResourceAttachments,
                PendingTypeDefinitions = state.PendingTypeDefinitions,
                CachedTypeDefinitions = state.CachedTypeDefinitions,
                NeededResources = state.NeededResources,
                NeededTypeDefinitions = state.NeededTypeDefinitions,
                ActiveInvocations = state.ActiveInvocations,
                QueuedNotificationWork = state.QueuedNotificationWork,
                RemoteMaximumPacketSize = state.RemoteMaximumPacketSize,
                RemoteMaximumAllocationSize = state.RemoteMaximumAllocationSize,
                RemoteMaximumCollectionItems = state.RemoteMaximumCollectionItems,
                RemoteMaximumTypeMetadataDepth = state.RemoteMaximumTypeMetadataDepth,
                RemoteMaximumEncryptedRecordSize = state.RemoteMaximumEncryptedRecordSize,
            };
            previousSampledAt = sampledAt;
            previousBytes = totalBytes;
            previousPackets = totalPackets;
            previousNotifications = totalNotifications;
            previousProperties = totalProperties;
            previousEvents = totalEvents;
            return snapshot;
        }
    }
}

public partial class EpConnection
{
    EpConnectionMetricsCollector _runtimeMetrics;

    /// <summary>Starts a fresh per-connection diagnostics window.</summary>
    public EpConnectionRuntimeMetricsSnapshot EnableRuntimeMetrics()
    {
        Volatile.Write(ref _runtimeMetrics, new EpConnectionMetricsCollector());
        return GetRuntimeMetrics();
    }

    /// <summary>Stops diagnostics and releases the counter store.</summary>
    public void DisableRuntimeMetrics() => Volatile.Write(ref _runtimeMetrics, null);

    /// <summary>Reads totals, current rates, queues, attachments, and negotiated limits.</summary>
    public EpConnectionRuntimeMetricsSnapshot GetRuntimeMetrics()
    {
        int subscriptions;
        int eventSubscriptions;
        lock (_subscriptionsLock)
        {
            subscriptions = _subscriptions.Count;
            eventSubscriptions = 0;
            foreach (var indexes in _subscriptions.Values)
                eventSubscriptions += indexes.Count;
        }
        int invocations;
        lock (_invocationsLock)
            invocations = _invocations.Count;

        var state = new EpConnectionRuntimeMetricsSnapshot
        {
            SampledAt = DateTime.UtcNow,
            Connected = IsConnected,
            Authenticated = _authenticated,
            Encrypted = IsEncrypted,
            AttachedResources = _attachedResources.Count,
            LocalResourceSubscriptions = subscriptions,
            EventSubscriptions = eventSubscriptions,
            PendingRequests = _requests.Count,
            PendingResourceAttachments = _resourceRequests.Count,
            PendingTypeDefinitions = _typeDefRequests.Count,
            CachedTypeDefinitions = _cachedTypeDefs.Count,
            NeededResources = _neededResources.Count,
            NeededTypeDefinitions = _neededTypeDefs.Count,
            ActiveInvocations = invocations,
            QueuedNotificationWork = _queue.Count,
            RemoteMaximumPacketSize = _session?.RemoteMaximumPacketSize ?? 0,
            RemoteMaximumAllocationSize = _session?.RemoteMaximumAllocationSize ?? 0,
            RemoteMaximumCollectionItems = _session?.RemoteMaximumCollectionItems ?? 0,
            RemoteMaximumTypeMetadataDepth = _session?.RemoteMaximumTypeMetadataDepth ?? 0,
            RemoteMaximumEncryptedRecordSize = _session?.RemoteMaximumEncryptedRecordSize ?? 0,
        };
        var collector = Volatile.Read(ref _runtimeMetrics);
        return collector?.Snapshot(state) ?? state;
    }
}
