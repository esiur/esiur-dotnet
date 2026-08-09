using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Esiur.Resource;

public enum ResourceJournalEntryKind : byte
{
    PropertyModified = 0,
    EventOccurred = 1,
}

/// <summary>An ordered, optionally retained resource change.</summary>
public sealed class ResourceJournalEntry
{
    public ResourceCursor Cursor { get; }
    public DateTime RecordedAt { get; }
    public ResourceJournalEntryKind Kind { get; }
    public byte MemberIndex { get; }
    public object Value { get; }

    public ResourceJournalEntry(
        ResourceCursor cursor,
        DateTime recordedAt,
        ResourceJournalEntryKind kind,
        byte memberIndex,
        object value)
    {
        Cursor = cursor;
        RecordedAt = recordedAt.Kind == DateTimeKind.Utc
            ? recordedAt
            : recordedAt.ToUniversalTime();
        Kind = kind;
        MemberIndex = memberIndex;
        Value = value;
    }
}

/// <summary>Filters a bounded resource-journal query.</summary>
public sealed class ResourceJournalQuery
{
    public ResourceCursor After { get; set; }
    public ulong? ThroughRevision { get; set; }
    public DateTime? FromTime { get; set; }
    public DateTime? ToTime { get; set; }
    public ResourceJournalEntryKind? Kind { get; set; }
    public byte? MemberIndex { get; set; }
    public int Limit { get; set; } = 1000;
}

/// <summary>A journal page and the stream boundaries used to read it.</summary>
public sealed class ResourceJournalPage
{
    public ResourceCursor OldestAvailable { get; }
    public ResourceCursor HighWatermark { get; }
    public ResourceCursor Next { get; }
    public bool CursorExpired { get; }
    public bool HasMore { get; }
    public ResourceJournalEntry[] Entries { get; }

    public ResourceJournalPage(
        ResourceCursor oldestAvailable,
        ResourceCursor highWatermark,
        ResourceCursor next,
        bool cursorExpired,
        bool hasMore,
        ResourceJournalEntry[] entries)
    {
        OldestAvailable = oldestAvailable;
        HighWatermark = highWatermark;
        Next = next;
        CursorExpired = cursorExpired;
        HasMore = hasMore;
        Entries = entries ?? Array.Empty<ResourceJournalEntry>();
    }
}

/// <summary>
/// Optional store capability for persisting historical property changes and
/// event occurrences. The resource instance owns ordering; the store owns
/// retention and persistence.
/// </summary>
public interface IResourceJournalStore
{
    ResourceCursor OpenJournal(
        IResource resource,
        string resourceKey,
        ResourceCursor proposedCursor);
    bool AppendJournalEntry(IResource resource, ResourceJournalEntry entry, bool retain);
    ResourceJournalPage QueryJournal(IResource resource, ResourceJournalQuery query);
    void RemoveJournal(IResource resource);
}

/// <summary>
/// Bounded in-memory implementation for volatile stores and tests. Persistent
/// stores can implement <see cref="IResourceJournalStore"/> with their own
/// transactional backend while preserving the same protocol contract.
/// </summary>
public sealed class ResourceJournalBuffer : IResourceJournalStore
{
    sealed class State
    {
        public ResourceCursor Head;
        public ulong DiscardedThroughRevision;
        public readonly LinkedList<ResourceJournalEntry> Entries = new();
    }

    sealed class ResourceReferenceComparer : IEqualityComparer<IResource>
    {
        public bool Equals(IResource x, IResource y) => ReferenceEquals(x, y);
        public int GetHashCode(IResource obj) => RuntimeHelpers.GetHashCode(obj);
    }

    readonly object sync = new();
    readonly Dictionary<IResource, State> states =
        new(new ResourceReferenceComparer());
    readonly int maximumEntriesPerResource;

    public ResourceJournalBuffer(int maximumEntriesPerResource = 10000)
    {
        if (maximumEntriesPerResource < 1)
            throw new ArgumentOutOfRangeException(nameof(maximumEntriesPerResource));
        this.maximumEntriesPerResource = maximumEntriesPerResource;
    }

    public ResourceCursor OpenJournal(
        IResource resource,
        string resourceKey,
        ResourceCursor proposedCursor)
    {
        if (resource == null) throw new ArgumentNullException(nameof(resource));
        lock (sync)
        {
            if (states.TryGetValue(resource, out var existing))
                return existing.Head;

            var generation = proposedCursor.Generation == Guid.Empty
                ? Guid.NewGuid()
                : proposedCursor.Generation;
            var cursor = new ResourceCursor(generation, proposedCursor.Revision);
            states.Add(resource, new State { Head = cursor });
            return cursor;
        }
    }

    public bool AppendJournalEntry(IResource resource, ResourceJournalEntry entry, bool retain)
    {
        if (resource == null) throw new ArgumentNullException(nameof(resource));
        if (entry == null) throw new ArgumentNullException(nameof(entry));

        lock (sync)
        {
            if (!states.TryGetValue(resource, out var state))
            {
                state = new State { Head = new ResourceCursor(entry.Cursor.Generation, 0) };
                states.Add(resource, state);
            }

            if (state.Head.Generation != entry.Cursor.Generation ||
                entry.Cursor.Revision <= state.Head.Revision)
                return false;

            state.Head = entry.Cursor;
            if (retain)
                state.Entries.AddLast(entry);

            while (state.Entries.Count > maximumEntriesPerResource)
            {
                state.DiscardedThroughRevision = Math.Max(
                    state.DiscardedThroughRevision,
                    state.Entries.First.Value.Cursor.Revision);
                state.Entries.RemoveFirst();
            }

            return true;
        }
    }

    public ResourceJournalPage QueryJournal(IResource resource, ResourceJournalQuery query)
    {
        if (resource == null) throw new ArgumentNullException(nameof(resource));
        query ??= new ResourceJournalQuery();
        var limit = Math.Max(1, Math.Min(query.Limit, 10000));

        lock (sync)
        {
            var proposed = new ResourceCursor(Guid.NewGuid(), 0);
            if (!states.TryGetValue(resource, out var state))
            {
                state = new State { Head = proposed };
                states.Add(resource, state);
            }

            var after = query.After;
            var generationMismatch = after.Generation != Guid.Empty &&
                                     after.Generation != state.Head.Generation;
            var expired = generationMismatch ||
                          (!generationMismatch &&
                           after.Revision < state.DiscardedThroughRevision);

            if (generationMismatch)
                after = new ResourceCursor(state.Head.Generation, 0);

            IEnumerable<ResourceJournalEntry> filtered = state.Entries
                .Where(entry => entry.Cursor.Revision > after.Revision);

            if (query.ThroughRevision.HasValue)
                filtered = filtered.Where(entry => entry.Cursor.Revision <= query.ThroughRevision.Value);
            if (query.FromTime.HasValue)
                filtered = filtered.Where(entry => entry.RecordedAt >= query.FromTime.Value.ToUniversalTime());
            if (query.ToTime.HasValue)
                filtered = filtered.Where(entry => entry.RecordedAt <= query.ToTime.Value.ToUniversalTime());
            if (query.Kind.HasValue)
                filtered = filtered.Where(entry => entry.Kind == query.Kind.Value);
            if (query.MemberIndex.HasValue)
                filtered = filtered.Where(entry => entry.MemberIndex == query.MemberIndex.Value);

            var selected = filtered.Take(limit + 1).ToArray();
            var hasMore = selected.Length > limit;
            var entries = hasMore ? selected.Take(limit).ToArray() : selected;
            var next = entries.Length == 0 ? after : entries[entries.Length - 1].Cursor;
            var oldest = state.Entries.First?.Value.Cursor ?? state.Head;

            return new ResourceJournalPage(
                oldest,
                state.Head,
                next,
                expired,
                hasMore,
                entries);
        }
    }

    public void RemoveJournal(IResource resource)
    {
        if (resource == null) return;
        lock (sync) states.Remove(resource);
    }
}
