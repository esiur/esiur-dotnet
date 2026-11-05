#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Esiur.Core;
using Esiur.Data;
using Esiur.Data.Types;
using Esiur.Misc;
using Esiur.Net.Packets;

namespace Esiur.Protocol;

// Client side of EpPacketRequest.SyncTypeDefs. See docs/typedef-sync.md.
partial class EpConnection
{
    /// <summary>
    /// How long connection establishment waits for the node to answer <c>SyncTypeDefs</c>. A node
    /// that predates the request ignores it; the cache is then dropped and the connection carries
    /// on with lazy fetching instead of waiting forever.
    /// </summary>
    public static TimeSpan TypeDefSyncTimeout { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Validates the warehouse's cached definitions for the remote domain with the node and
    /// brings the cache and this connection's id map in line with what the node answers. Always
    /// completes (true when the cache was validated or empty, false when it had to be dropped),
    /// so connection establishment can continue.
    /// </summary>
    internal AsyncReply<bool> SyncTypeDefCache()
    {
        var reply = new AsyncReply<bool>();
        var warehouse = Instance?.Warehouse;

        var cached = warehouse?.GetCachedRemoteTypeDefs(_remoteDomain)
                         .Where(definition => definition.Id != 0 && !string.IsNullOrEmpty(definition.Name))
                         .ToList()
                     ?? new List<RemoteTypeDef>();

        if (warehouse == null || cached.Count == 0)
        {
            reply.Trigger(true);
            return reply;
        }

        var entries = new object[cached.Count];
        for (var i = 0; i < cached.Count; i++)
        {
            var definition = cached[i];
            entries[i] = IndexedStructureCodec.ToMap(new TypeDefCacheEntry
            {
                Id = definition.Id,
                Name = definition.Name,
                Kind = definition.Kind,
                Version = definition.Version,
                Signature = definition.Signature,
            });
        }

        // Completes `reply` once, whichever of the answer, a failure or the timeout comes first.
        // (Not disposed here: this method returns before the node answers, and the token is still
        // needed then. It holds no timer or handle, so letting it be collected is enough.)
        var settled = 0;
        var answered = new CancellationTokenSource();

        void Succeeded()
        {
            if (Interlocked.Exchange(ref settled, 1) == 0)
                reply.Trigger(true);
        }

        void Dropped(Exception ex)
        {
            if (Interlocked.Exchange(ref settled, 1) != 0)
                return;

            Global.Log("EpConnection", LogType.Warning, "TypeDef cache sync failed, cache dropped: " + ex.Message);
            warehouse.ClearRemoteTypeDefs(_remoteDomain);
            _cachedTypeDefs.Clear();
            reply.Trigger(false);
        }

        try
        {
            SendRequest(EpPacketRequest.SyncTypeDefs, new object[] { entries })
                .Then(result =>
                {
                    // The node answered: from here the work is applying its answer, which may fetch
                    // referenced definitions and is not bounded by the timeout.
                    answered.Cancel();
                    try
                    {
                        var info = Codec.ParseIndexedType<TypeDefSyncInfo>(result);
                        ApplyTypeDefSync(cached, info)
                            .Then(_ => Succeeded())
                            .Error(ex => Dropped(ex));
                    }
                    catch (Exception ex)
                    {
                        Dropped(ex);
                    }
                })
                .Error(ex =>
                {
                    answered.Cancel();
                    Dropped(ex);
                });

            var timeout = TypeDefSyncTimeout;
            if (timeout > TimeSpan.Zero)
            {
                Task.Delay(timeout, answered.Token).ContinueWith(
                    task =>
                    {
                        if (!task.IsCanceled)
                            Dropped(new TimeoutException("The node did not answer SyncTypeDefs."));
                    },
                    TaskScheduler.Default);
            }
        }
        catch (Exception ex)
        {
            Dropped(ex);
        }

        return reply;
    }

    AsyncReply<bool> ApplyTypeDefSync(List<RemoteTypeDef> requested, TypeDefSyncInfo info)
    {
        var warehouse = Instance.Warehouse;
        var result = new AsyncReply<bool>();

        var removed = info.Removed ?? new List<ulong>();
        var updates = info.Updated ?? new List<TypeDefUpdate>();
        var moves = new List<(ulong OldId, ulong NewId)>();
        foreach (var move in info.Moved ?? new List<TypeDefIdMove>())
            moves.Add((move.OldId, move.NewId));
        foreach (var update in updates)
            moves.Add((update.OldId, update.NewId));

        // The layouts of definitions that are about to change in place, captured before the change.
        var previousLayouts = new Dictionary<RemoteTypeDef, TypeDefLayout>();
        var targets = new List<(RemoteTypeDef Definition, TypeDefUpdate Update)>();
        foreach (var update in updates)
        {
            var definition = requested.FirstOrDefault(x => x.Id == update.OldId);
            if (definition == null)
                continue;

            previousLayouts[definition] = definition.CaptureLayout();
            targets.Add((definition, update));
        }

        warehouse.ApplyRemoteTypeDefIdChanges(_remoteDomain, removed, moves);

        // This connection's id map is exactly the validated definitions under the ids the node
        // uses now. Built before any definition is parsed, so references between definitions
        // resolve to the existing objects and never need an id patch.
        _cachedTypeDefs.Clear();
        foreach (var definition in warehouse.GetCachedRemoteTypeDefs(_remoteDomain))
            _cachedTypeDefs[definition.Id] = definition;

        void Finished()
        {
            try
            {
                RebindResources(previousLayouts);
                result.Trigger(true);
            }
            catch (Exception ex)
            {
                result.TriggerError(new AsyncException(ErrorType.Exception, (ushort)ExceptionCode.GeneralFailure, ex.Message));
            }
        }

        if (targets.Count == 0)
        {
            Finished();
            return result;
        }

        var bag = new AsyncBag<object>();
        foreach (var (definition, update) in targets)
            bag.Add(RemoteTypeDef.Parse(definition, _remoteDomain, update.Definition, this, new[] { update.NewId }));

        bag.Seal();
        bag.Then(_ => Finished()).Error(ex => result.TriggerError(ex));
        return result;
    }

    // Resources already attached (a reconnect keeps them) map their index-keyed state onto the
    // updated definition by member name.
    void RebindResources(Dictionary<RemoteTypeDef, TypeDefLayout> previousLayouts)
    {
        if (previousLayouts.Count == 0)
            return;

        foreach (var references in new[] { _attachedResources, _suspendedResources })
        {
            foreach (var reference in references.Values.ToList())
            {
                if (!reference.TryGetTarget(out var resource))
                    continue;

                if (resource.ResourceDefinition is RemoteTypeDef definition
                    && previousLayouts.TryGetValue(definition, out var previous))
                    resource.RebindDefinition(previous);
            }
        }
    }
}
