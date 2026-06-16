/*
 
Copyright (c) 2017-2025 Ahmed Kh. Zamil

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/

using Esiur.Core;
using Esiur.Data;
using Esiur.Data.Types;
using Esiur.Misc;
using Esiur.Net;
using Esiur.Net.Packets;
using Esiur.Resource;
using Esiur.Security.Authority;
using Esiur.Security.Permissions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Protocol;

partial class EpConnection
{
    KeyList<ulong, RemoteTypeDef> _neededTypeDefs = new KeyList<ulong, RemoteTypeDef>();
    KeyList<ulong, RemoteTypeDef> _cachedTypeDefs = new KeyList<ulong, RemoteTypeDef>();
    KeyList<ulong, FetchRequestInfo<RemoteTypeDef, ulong>> _typeDefRequests = new KeyList<ulong, FetchRequestInfo<RemoteTypeDef, ulong>>();

    //KeyList<ulong, AsyncReply<RemoteTypeDef>> _typeDefsByIdRequests = new KeyList<ulong, AsyncReply<RemoteTypeDef>>();

    KeyList<uint, EpResource> _neededResources = new KeyList<uint, EpResource>();
    KeyList<uint, WeakReference<EpResource>> _attachedResources = new KeyList<uint, WeakReference<EpResource>>();
    KeyList<uint, WeakReference<EpResource>> _suspendedResources = new KeyList<uint, WeakReference<EpResource>>();
    KeyList<uint, FetchRequestInfo<EpResource, uint>> _resourceRequests = new KeyList<uint, FetchRequestInfo<EpResource, uint>>();

    // Wait-for graph for in-flight resource fetches: maps a resource id to the set of in-flight
    // child resource ids its attachment is currently blocked on. Used to detect genuine cycles
    // (e.g. two concurrent fetches A<->B) so a placeholder can break the deadlock, while
    // independent/app-facing fetches of an in-flight resource simply wait for full attachment.
    readonly Dictionary<uint, HashSet<uint>> _fetchBlockedOn = new Dictionary<uint, HashSet<uint>>();

    // Same wait-for graph as above, but for in-flight remote type definition parsing.
    readonly Dictionary<ulong, HashSet<ulong>> _typeDefFetchBlockedOn = new Dictionary<ulong, HashSet<ulong>>();
    readonly object _deliveredRootsLock = new object();
    readonly Dictionary<uint, WeakReference<EpResource>> _deliveredRoots = new Dictionary<uint, WeakReference<EpResource>>();

    /// <summary>
    /// Strategy fetches use for in-flight resources and type definitions. Defaults to the new wait + cycle
    /// detection. Selectable for experimental evaluation (see <see cref="DeadlockResolutionMode"/>).
    /// </summary>
    public DeadlockResolutionMode DeadlockResolution { get; set; } = DeadlockResolutionMode.WaitWithCycleDetection;

    // Per-connection diagnostics (free of the cross-connection contamination that the shared
    // Global.Counters suffer from). Used by the deadlock experiments.
    /// <summary>Number of resources fully attached on this connection (a monotonic progress signal).</summary>
    public long AttachedResourceCount { get; private set; }
    /// <summary>Number of wait-for-cycle breaks (placeholders returned to break a cycle) on this connection.</summary>
    public long CycleBreakCount { get; private set; }
    /// <summary>Number of placeholders returned where no genuine cycle existed (legacy resolver only).</summary>
    public long UnnecessaryPlaceholderCount { get; private set; }
    //KeyList<ulong, AsyncReply<RemoteTypeDef>> _typeDefsByIdRequests = new KeyList<ulong, AsyncReply<RemoteTypeDef>>();

    //KeyList<string, AsyncReply<RemoteTypeDef>> _typeDefsByNameRequests = new KeyList<string, AsyncReply<RemoteTypeDef>>();


    //Dictionary<Uuid, TypeDef> typeDefs = new Dictionary<Uuid, TypeDef>();

    object _typeDefsLock = new object();

    KeyList<uint, AsyncReply> _requests = new KeyList<uint, AsyncReply>();

    volatile int _callbackCounter = 0;

    Dictionary<IResource, List<byte>> _subscriptions = new Dictionary<IResource, List<byte>>();

    // resources might get attached by the client
    internal KeyList<IResource, DateTime> _cache = new();

    object _subscriptionsLock = new object();

    AsyncQueue<EpResourceQueueItem> _queue = new();



    /// <summary>
    /// Send EP request.
    /// </summary>
    /// <param name="action">Packet action.</param>
    /// <param name="args">Arguments to send.</param>
    /// <returns></returns>
    /// 

    AsyncReply SendRequest(EpPacketRequest action, params object[] args)
    {
#if VERBOSE
        Console.WriteLine($"Send request {action}");
#endif
        var reply = new AsyncReply();
        var c = (uint)Interlocked.Increment(ref _callbackCounter);
        //callbackCounter++; // avoid thread racing
        _requests.Add(c, reply);

        if (args.Length == 0)
        {
            var bl = new BinaryList();
            bl.AddUInt8((byte)(0x40 | (byte)action))
              .AddUInt32(c);
            Send(bl.ToArray());
        }
        if (args.Length == 1)
        {
            var bl = new BinaryList();
            bl.AddUInt8((byte)(0x60 | (byte)action))
              .AddUInt32(c)
              .AddUInt8Array(Codec.Compose(args[0], this.Instance?.Warehouse ?? _serverWarehouse, this));
            Send(bl.ToArray());
        }
        else
        {
            var bl = new BinaryList();
            bl.AddUInt8((byte)(0x60 | (byte)action))
              .AddUInt32(c)
              .AddUInt8Array(Codec.Compose(args, this.Instance?.Warehouse ?? _serverWarehouse, this));
            Send(bl.ToArray());
        }

        return reply;
    }

    //void SendAuthMaterials(EpAuthPacketMethod method, AuthenticationMaterial[] authenticationMaterials)
    //{
    //    if (authenticationMaterials != null)
    //    {
    //        var authMap = new Map<byte, object>();
    //        foreach (var material in authenticationMaterials)
    //            authMap.Add(material.Type, material.Value);

    //        var bl = new BinaryList();
    //        bl.AddUInt8((byte)((byte)method | 0x20));
    //        bl.AddUInt8Array(Codec.Compose(authMap, Instance.Warehouse, this));
    //        Send(bl.ToArray());
    //    }
    //    else
    //    {
    //        Send(new byte[] { (byte)method });
    //    }
    //}
    void SendAuthData(EpAuthPacketMethod method, object data)
    {
        if (data != null)
        {
            var bl = new BinaryList();
            bl.AddUInt8((byte)((byte)method | 0x20));
            bl.AddUInt8Array(Codec.Compose(data, this.Instance?.Warehouse ?? _serverWarehouse, this));
            Send(bl.ToArray());
        }
        else
        {
            Send(new byte[] { (byte)method });
        }
    }

    void SendAuth(EpAuthPacketMethod method)
    {
        Send(new byte[] { (byte)method });
    }

    void SendAuthMessage(EpAuthPacketMethod method, string message)
    {
        var bl = new BinaryList();
        bl.AddUInt8((byte)((byte)method | 0x20));
        bl.AddUInt8Array(Codec.Compose(message, this.Instance?.Warehouse ?? _serverWarehouse, this));
        Send(bl.ToArray());
    }

    void SendAuthHeaders(EpAuthPacketMethod method,
        Map<byte, object> authHeaders)
    {
        if (authHeaders != null)
        {
            //var authMap = new Map<byte, object>();

            //foreach (var header in authHeaders)
            //{
            //    authMap.Add(header.Key, header.Value);
            //}

            var bl = new BinaryList();
            bl.AddUInt8((byte)((byte)method | 0x20));
            bl.AddUInt8Array(Codec.Compose(authHeaders, 
                                            this.Instance?.Warehouse ?? _serverWarehouse, 
                                            this));
            Send(bl.ToArray());
        }
        else
        {
            Send(new byte[] { (byte)method });
        }
    }


    /// <summary>
    /// Send EP notification.
    /// </summary>
    /// <param name="action">Packet action.</param>
    /// <param name="args">Arguments to send.</param>
    /// <returns></returns>
    AsyncReply SendNotification(EpPacketNotification action, params object[] args)
    {

        var reply = new AsyncReply();

        if (args.Length == 0)
        {
            Send(new byte[] { (byte)action });
        }
        if (args.Length == 1)
        {
            var bl = new BinaryList();
            bl.AddUInt8((byte)(0x20 | (byte)action))
              .AddUInt8Array(Codec.Compose(args[0], this.Instance?.Warehouse ?? _serverWarehouse, this));
            Send(bl.ToArray());
        }
        else
        {
            var bl = new BinaryList();
            bl.AddUInt8((byte)(0x20 | (byte)action))
              .AddUInt8Array(Codec.Compose(args, this.Instance?.Warehouse ?? _serverWarehouse, this));
            Send(bl.ToArray());
        }

        return reply;
    }

    void SendReply(EpPacketReply action, uint callbackId, params object[] args)
    {
        if (Instance == null)
            return;

        if (args.Length == 0)
        {
            var bl = new BinaryList();
            bl.AddUInt8((byte)(0x80 | (byte)action))
              .AddUInt32(callbackId);
            Send(bl.ToArray());
        }
        if (args.Length == 1)
        {
            var bl = new BinaryList();
            bl.AddUInt8((byte)(0xA0 | (byte)action))
              .AddUInt32(callbackId)
              .AddUInt8Array(Codec.Compose(args[0], this.Instance?.Warehouse ?? _serverWarehouse, this));
            Send(bl.ToArray());
        }
        else
        {
            var bl = new BinaryList();
            bl.AddUInt8((byte)(0xA0 | (byte)action))
              .AddUInt32(callbackId)
              .AddUInt8Array(Codec.Compose(args, this.Instance?.Warehouse ?? _serverWarehouse, this));
            Send(bl.ToArray());
        }
    }


    internal AsyncReply SendSubscribeRequest(uint instanceId, byte index)
    {
        return SendRequest(EpPacketRequest.Subscribe, instanceId, index);
    }

    internal AsyncReply SendUnsubscribeRequest(uint instanceId, byte index)
    {
        return SendRequest(EpPacketRequest.Unsubscribe, instanceId, index);
    }


    public AsyncReply StaticCall(ulong typeId, byte index, object parameters)
    {
        return SendRequest(EpPacketRequest.StaticCall, typeId, index, parameters);
    }

    public AsyncReply Call(string procedureCall, params object[] parameters)
    {
        //var args = new Map<byte, object>();
        //for (byte i = 0; i < parameters.Length; i++)
        //    args.Add(i, parameters[i]);
        //        return Call(procedureCall, parameters);

        return SendRequest(EpPacketRequest.ProcedureCall, procedureCall, parameters);
    }

    public AsyncReply Call(string procedureCall, Map<byte, object> parameters)
    {
        return SendRequest(EpPacketRequest.ProcedureCall, procedureCall, parameters);
    }

    internal AsyncReply SendInvoke(uint instanceId, byte index, object parameters)
    {
        return SendRequest(EpPacketRequest.InvokeFunction, instanceId, index, parameters);
    }

    internal AsyncReply SendSetProperty(uint instanceId, byte index, object value)
    {
        return SendRequest(EpPacketRequest.SetProperty, instanceId, index, value);
    }

    internal AsyncReply SendDetachRequest(uint instanceId)
    {
        try
        {
            var sendDetach = false;

            if (_attachedResources.ContainsKey(instanceId))
            {
                _attachedResources.Remove(instanceId);
                sendDetach = true;
            }

            if (_suspendedResources.ContainsKey(instanceId))
            {
                _suspendedResources.Remove(instanceId);
                sendDetach = true;
            }

            if (sendDetach)
                return SendRequest(EpPacketRequest.DetachResource, instanceId);

            return null; // no one is waiting for this
        }
        catch
        {
            return null;
        }
    }

    void SendError(ErrorType type, uint callbackId, ushort errorCodeOrWarningLevel, string message = "")
    {
        if (type == ErrorType.Management)
            SendReply(EpPacketReply.PermissionError, callbackId, errorCodeOrWarningLevel, message);
        else if (type == ErrorType.Exception)
            SendReply(EpPacketReply.ExecutionError, callbackId, errorCodeOrWarningLevel, message);
        else if (type == ErrorType.Warning)
            SendReply(EpPacketReply.Warning, callbackId, (byte)errorCodeOrWarningLevel, message);
    }

    internal void SendProgress(uint callbackId, uint value, uint max)
    {
        SendReply(EpPacketReply.Progress, callbackId, value, max);
    }

    internal void SendWarning(uint callbackId, byte level, string message)
    {
        SendReply(EpPacketReply.Warning, callbackId, level, message);
    }

    internal void SendChunk(uint callbackId, object chunk)
    {
        SendReply(EpPacketReply.Chunk, callbackId, chunk);
    }

    void EpReplyCompleted(uint callbackId, PlainTdu tdu)
    {
        var req = _requests.Take(callbackId);

        //Console.WriteLine("Completed " + callbackId);

        if (req == null)
        {
            // @TODO: Send general failure
            return;
        }

        var pr = Codec.Parse(tdu, this, null);

        if (pr is AsyncReply asyncReply)
        {
            asyncReply.Then(req.Trigger)
                      .Error(req.TriggerError);
        }
        else
        {
            req.Trigger(pr);
        }

        //var pr = Codec.ParseAsync(dataType, this, null).Then(pr =>
        //{
        //    if (pr.Value is AsyncReply asyncReply)
        //    {
        //        asyncReply.Then(req.Trigger)
        //                  .Error(req.TriggerError);
        //    }
        //    else
        //    {
        //        req.Trigger(pr.Value);
        //    }
        //}).Error(req.TriggerError);
    }

    void EpExtensionAction(byte actionId, PlainTdu? tdu)
    {
        // nothing is supported now
    }

    void EpReplyPropagated(uint callbackId, PlainTdu tdu)
    {
        var req = _requests[callbackId];

        if (req == null)
        {
            // @TODO: Send general failure
            return;
        }

        var value = Codec.Parse(tdu, this, null);

        if (value is AsyncReply reply)
        {
            reply.Then(req.TriggerPropagation)
                 .Error(req.TriggerError);
        }
        else
        {
            req.TriggerPropagation(value);
        }

        //var pr = Codec.ParseAsync(dataType, this, null).Then(pr =>
        //{
        //    if (pr.Value is AsyncReply reply)
        //    {
        //        reply.Then(req.TriggerPropagation)
        //             .Error(req.TriggerError);
        //    }
        //    else
        //    {
        //        req.TriggerPropagation(pr.Value);
        //    }
        //}).Error(req.TriggerError);
    }

    void EpReplyError(uint callbackId, PlainTdu plainTdu, ErrorType type)
    {
        var req = _requests.Take(callbackId);

        if (req == null)
        {
            // @TODO: Send general failure
            return;
        }

        var tdu = ParsedTdu.ParseSync(plainTdu.Data, plainTdu.TduOffset, plainTdu.Ends, Instance.Warehouse);
        var args = DataDeserializer.ListParser(tdu, Instance.Warehouse)
                                                as object[];

        var errorCode = Convert.ToUInt16(args[0]);
        var errorMsg = (string)args[1];

        req.TriggerError(new AsyncException(type, errorCode, errorMsg));
    }

    void EpReplyProgress(uint callbackId, PlainTdu plainTdu)
    {
        var req = _requests[callbackId];

        if (req == null)
        {
            // @TODO: Send general failure
            return;
        }

        var tdu = ParsedTdu.ParseSync(plainTdu.Data, plainTdu.TduOffset, plainTdu.Ends, Instance.Warehouse);
        var args = DataDeserializer.ListParser(tdu, Instance.Warehouse)
                                                as object[];

        var current = (uint)args[0];
        var total = (uint)args[1];

        req.TriggerProgress(ProgressType.Execution, current, total);
    }

    void EpReplyWarning(uint callbackId, PlainTdu plainTdu)
    {
        var req = _requests[callbackId];

        if (req == null)
        {
            // @TODO: Send general failure
            return;
        }

        var tdu = ParsedTdu.ParseSync(plainTdu.Data, plainTdu.TduOffset, plainTdu.Ends, Instance.Warehouse);
        var args = DataDeserializer.ListParser(tdu, Instance.Warehouse)
                                                as object[];

        var level = (byte)args[0];
        var message = (string)args[1];

        req.TriggerWarning(level, message);
    }



    void EpReplyChunk(uint callbackId, PlainTdu tdu)
    {
        var req = _requests[callbackId];

        if (req == null)
            return;

        var value = Codec.Parse(tdu, this, null);

        if (value is AsyncReply asyncReply)
        {
            asyncReply.Then(req.TriggerChunk)
                      .Error(req.TriggerError);
        }
        else
        {
            req.TriggerChunk(value);
        }

        //Codec.ParseAsync(dataType, this, null).Then(pr =>
        //{
        //    if (pr.Value is AsyncReply asyncReply)
        //    {
        //        asyncReply.Then(req.TriggerChunk)
        //                  .Error(req.TriggerError);
        //    }
        //    else
        //    {
        //        req.TriggerChunk(pr.Value);
        //    }
        //}).Error(req.TriggerError);
    }

    void EpNotificationResourceReassigned(PlainTdu dataType)
    {
        // uint resourceId, uint newResourceId
    }

    void EpNotificationResourceMoved(PlainTdu tdu) { }

    void EpNotificationSystemFailure(PlainTdu tdu) { }

    void EpNotificationResourceDestroyed(PlainTdu tdu)
    {
        var (size, rt) = Codec.ParseSync(tdu.Data, tdu.TduOffset, Instance.Warehouse);

        var resourceId = Convert.ToUInt32(rt);

        if (_attachedResources.Contains(resourceId))
        {
            EpResource r;

            if (_attachedResources[resourceId].TryGetTarget(out r))
            {
                // remove from attached to avoid sending unnecessary detach request when Destroy() is called
                _attachedResources.Remove(resourceId);
                r.Destroy();
            }
            else
            {
                _attachedResources.Remove(resourceId);
            }


        }
        else if (_neededResources.Contains(resourceId))
        {
            // @TODO: handle this mess
            _neededResources.Remove(resourceId);
        }

    }

    void EpNotificationPropertyModified(PlainTdu tdu)
    {
        // resourceId, index, value
        var (valueOffset, valueSize, args) =
            DataDeserializer.LimitedCountListParser(tdu.Data, tdu.PayloadOffset, tdu.PayloadLength, Instance.Warehouse, 2);

        var rid = Convert.ToUInt32(args[0]);
        var index = (byte)args[1];

        FetchResource(rid, null).Then(r =>
        {
            var pt = r.Instance.Definition.GetPropertyDefByIndex(index);
            if (pt == null)
                return;

            void EnqueueParsedProperty(ParsedTdu parsed)
            {
                var value = Codec.ParseAsync(parsed, this, null);
                if (value is AsyncReply asyncReply)
                {
                    var item = new AsyncReply<EpResourceQueueItem>();
                    _queue.Add(item, hasResource: true);

                    asyncReply.Then((result) =>
                    {
                        item.Trigger(new EpResourceQueueItem((EpResource)r,
                                                        EpResourceQueueItem.DistributedResourceQueueItemType.Propery,
                                                        result, index));
                    });
                }
                else
                {
                    _queue.Add(new AsyncReply<EpResourceQueueItem>(new EpResourceQueueItem((EpResource)r,
                                                    EpResourceQueueItem.DistributedResourceQueueItemType.Propery,
                                                    value, index)), hasResource: false);
                }
            }

            var parsed = ParsedTdu.Parse(tdu.Data, valueOffset, (uint)tdu.Data.Length, this);
            if (parsed is ParsedTdu parsedTdu)
            {
                EnqueueParsedProperty(parsedTdu);
            }
            else if (parsed is AsyncReply<ParsedTdu> parsedReply)
            {
                parsedReply.Then(EnqueueParsedProperty).Error((ex) =>
                {
                    //.Error(x => SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ParseError));
                    throw ex;
                });
            }
            else
            {
                throw new NullReferenceException("DataType can't be parsed.");
            }

        });
    }


    void EpNotificationEventOccurred(PlainTdu tdu)
    {
        // resourceId, index, value
        var (valueOffset, valueSize, args) =
            DataDeserializer.LimitedCountListParser(tdu.Data, tdu.PayloadOffset,
                                                    tdu.PayloadLength, Instance.Warehouse, 2);

        var resourceId = Convert.ToUInt32(args[0]);
        var index = (byte)args[1];

        FetchResource(resourceId, null).Then(r =>
        {
            var et = r.Instance.Definition.GetEventDefByIndex(index);

            if (et == null) // this should never happen
                return;

            // push to the queue to guarantee serialization
            var item = new AsyncReply<EpResourceQueueItem>();
            _queue.Add(item);


            Codec.ParseAsync(tdu.Data, valueOffset, this, null).Then(pr =>
            {
                if (pr.Value is AsyncReply asyncReply)
                {
                    asyncReply.Then((result) =>
                    {
                        item.Trigger(new EpResourceQueueItem((EpResource)r,
                                     EpResourceQueueItem.DistributedResourceQueueItemType.Event, result, index));
                    });
                }
                else
                {
                    item.Trigger(new EpResourceQueueItem((EpResource)r,
                                  EpResourceQueueItem.DistributedResourceQueueItemType.Event, pr.Value, index));
                }

            }).Error((ex) => throw ex);
            // @TODO: Send general error
            //.Error(x => SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ParseError));
        });

    }

    void EpEventRenamed(uint resourceId, string name)
    {
        FetchResource(resourceId, null).Then(resource =>
        {
            resource.Instance.Variables["name"] = name;
        });
    }

    void EpRequestAttachResource(uint callback, PlainTdu tdu)
    {

        var value = Codec.ParseSync(tdu, Instance.Warehouse);

        var resourceId = Convert.ToUInt32(value);

        Instance.Warehouse.GetById(resourceId).Then((res) =>
        {
            if (res != null)
            {
                if (res.Instance.Applicable(_session, ActionType.Attach, null) == Ruling.Denied)
                {
                    SendError(ErrorType.Management, callback, 6);
                    return;
                }

                var r = res as IResource;

                // unsubscribe
                Unsubscribe(r);

                // reply ok
                SendReply(EpPacketReply.Completed, callback,
                    r.Instance.Definition.Id,
                    r.Instance.Age,
                    r.Instance.Link,
                    r.Instance.Hops,
                    r.Instance.Serialize());

                // subscribe
                Subscribe(r);
            }
            else
            {
                // reply failed
                Global.Log("EpConnection", LogType.Debug, "Not found " + resourceId);
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ResourceNotFound);
            }
        });
    }

    void EpRequestReattachResource(uint callback, PlainTdu tdu)
    {
        // resourceId, index, value
        var (valueOffset, valueSize, args) =
            DataDeserializer.LimitedCountListParser(tdu.Data, tdu.PayloadOffset,
                                                    tdu.PayloadLength, Instance.Warehouse, 2);

        var resourceId = Convert.ToUInt32(args[0]);

        var age = Convert.ToUInt64(args[1]);

        Instance.Warehouse.GetById(resourceId).Then((res) =>
        {
            if (res != null)
            {
                if (res.Instance.Applicable(_session, ActionType.Attach, null) == Ruling.Denied)
                {
                    SendError(ErrorType.Management, callback, 6);
                    return;
                }

                var r = res as IResource;

                // unsubscribe
                Unsubscribe(r);


                // reply ok
                SendReply(EpPacketReply.Completed, callback,
                    r.Instance.Definition.Id,
                    r.Instance.Age,
                    r.Instance.Link,
                    r.Instance.Hops,
                    r.Instance.SerializeAfter(age));


                // subscribe
                Subscribe(r);
            }
            else
            {
                // reply failed
                Global.Log("EpConnection", LogType.Debug, "Not found " + resourceId);
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ResourceNotFound);
            }
        });
    }

    void EpRequestDetachResource(uint callback, PlainTdu tdu)
    {
        var value = Codec.ParseSync(tdu, Instance.Warehouse);

        var resourceId = Convert.ToUInt32(value);

        Instance.Warehouse.GetById(resourceId).Then((res) =>
        {
            if (res != null)
            {

                // unsubscribe
                Unsubscribe(res);
                // remove from cache
                _cache.Remove(res);

                // remove from attached resources
                //attachedResources.Remove(res);

                // reply ok
                SendReply(EpPacketReply.Completed, callback);
            }
            else
            {
                // reply failed
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ResourceNotFound);
            }
        });
    }

    void EpRequestCreateResource(uint callback, PlainTdu tdu)
    {
        Codec.ParseAsync(tdu.Data, tdu.TduOffset, this, null).Then(pr =>
        {
            var args = (object[])pr.Value;

            var path = (string)args[0];

            TypeDef typeDef = null;

            if (args[1] is uint || args[1] is byte || args[1] is ushort) // @TODO: this is a mess, we should have a better way to distinguish between type id and name
                typeDef = Instance.Warehouse.GetLocalTypeDefById(Convert.ToUInt64(args[1]));
            else if (args[1] is string)
                typeDef = Instance.Warehouse.GetLocalTypeDefByName((string)args[1]);

            if (typeDef == null || typeDef is not LocalTypeDef)
            {
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ClassNotFound);
                return;
            }

            var localTypeDef = typeDef as LocalTypeDef;


            var props = (Map<byte, object>)((object[])args)[2];
            var attrs = (Map<string, object>)((object[])args)[3];

            // Get store
            var sc = path.Split('/');

            Instance.Warehouse.Get<IResource>(string.Join("/", sc.Take(sc.Length - 1)))
                 .Then(r =>
                 {
                     if (r == null)
                     {
                         SendError(ErrorType.Management, callback, (ushort)ExceptionCode.StoreNotFound);
                         return;
                     }

                     var store = r.Instance.Store;

                     // check security
                     if (store.Instance.Applicable(_session, ActionType.CreateResource, null) != Ruling.Allowed)
                     {
                         SendError(ErrorType.Management, callback, (ushort)ExceptionCode.CreateDenied);
                         return;
                     }

                     Instance.Warehouse.New(localTypeDef.DefinedType, path,
                         new ResourceContext(0,
                                             attrs,
                                             props.Select(x => new KeyValuePair<string, object>
                                                               (localTypeDef.GetPropertyDefByIndex(x.Key).Name, x.Value)),
                                             null))
                     .Then(resource =>
                     {
                         SendReply(EpPacketReply.Completed, callback, resource.Instance.Id);

                     }).Error(e =>
                     {
                         SendError(e.Type, callback, (ushort)e.Code, e.Message);
                     });

                 }).Error(e =>
                 {
                     SendError(e.Type, callback, (ushort)e.Code, e.Message);
                 });

        }).Error(x => SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ParseError));
    }


    void EpRequestDeleteResource(uint callback, PlainTdu tdu)
    {

        var value = Codec.ParseSync(tdu, Instance.Warehouse);

        var resourceId = Convert.ToUInt32(value);

        Instance.Warehouse.GetById(resourceId).Then(r =>
        {
            if (r == null)
            {
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ResourceNotFound);
                return;
            }

            if (r.Instance.Store.Instance.Applicable(_session, ActionType.Delete, null) != Ruling.Allowed)
            {
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.DeleteDenied);
                return;
            }

            if (Instance.Warehouse.Remove(r))
                SendReply(EpPacketReply.Completed, callback);

            else
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.DeleteFailed);
        });
    }

    void EpRequestMoveResource(uint callback, PlainTdu tdu)
    {

        var (offset, length, args) = DataDeserializer.LimitedCountListParser(tdu.Data, tdu.PayloadOffset,
                                                                     tdu.PayloadLength, Instance.Warehouse);


        var resourceId = Convert.ToUInt32(args[0]);

        var name = (string)args[1];

        if (name.Contains("/"))
        {
            SendError(ErrorType.Management, callback, (ushort)ExceptionCode.NotSupported);
            return;
        }

        Instance.Warehouse.GetById(resourceId).Then(resource =>
        {
            if (resource == null)
            {
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ResourceNotFound);
                return;
            }

            if (resource.Instance.Applicable(this._session, ActionType.Rename, null) != Ruling.Allowed)
            {
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.RenameDenied);
                return;
            }


            resource.Instance.Name = name;
            SendReply(EpPacketReply.Completed, callback);
        });
    }





    void EpRequestToken(uint callback, PlainTdu tdu)
    {
        // @TODO: To be implemented
    }

    void EpRequestLinkTypeDefs(uint callback, PlainTdu tdu)
    {
        var value = Codec.ParseSync(tdu, Instance.Warehouse);

        var resourceLink = (string)value;

        Action<IResource> queryCallback = (r) =>
        {
            if (r == null)
            {
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ResourceNotFound);
                return;
            }

            if (r.Instance.Applicable(_session, ActionType.ViewTypeDef, null) == Ruling.Denied)
            {
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.NotAllowed);
                return;
            }

            // make sure the resource is a local type def.
            if (r.Instance.Definition is LocalTypeDef localTypeDef)
            {
                var typeDefs = LocalTypeDef.GetDependencies(localTypeDef, Instance.Warehouse);
                // Send
                SendReply(EpPacketReply.Completed, callback, typeDefs.Select(x => x.Compose(this)).ToArray());
            }
            else
            {
                // @TODO: Add support for remote type defs
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.NotSupported);
            }
        };

        if (Server?.EntryPoint != null)
            Server.EntryPoint.Query(resourceLink, this).Then(queryCallback);
        else
            Instance.Warehouse.Query(resourceLink).Then(queryCallback);
    }

    void EpRequestTypeDefIdsByNames(uint callback, PlainTdu tdu)
    {
        var value = Codec.ParseSync(tdu, Instance.Warehouse);

        var classNames = (string[])value;

        var typeDefs = new List<ulong>();

        foreach (var className in classNames)
        {
            //@TODO: need to search in remoteTypeDefs as well  
            var typeDef = Instance.Warehouse.GetLocalTypeDefByName(className);
            if (typeDef != null)
                typeDefs.Add(typeDef.Id);
        }

        if (typeDefs.Count > 0)
        {
            SendReply(EpPacketReply.Completed, callback, typeDefs.ToArray());
        }
        else
        {
            // reply failed
            SendError(ErrorType.Management, callback, (ushort)ExceptionCode.TypeDefNotFound);
        }
    }

    void EpRequestTypeDefById(uint callback, PlainTdu tdu)
    {

        var value = Codec.ParseSync(tdu, Instance.Warehouse);

        var typeId = Convert.ToUInt32(value);

        var t = Instance.Warehouse.GetLocalTypeDefById(typeId);

        if (t != null)
        {
            SendReply(EpPacketReply.Completed, callback, t.Compose(this));
        }
        else
        {
            // reply failed
            SendError(ErrorType.Management, callback, (ushort)ExceptionCode.TypeDefNotFound);
        }
    }



    void EpRequestTypeDefByResourceId(uint callback, PlainTdu tdu)
    {

        var value = Codec.ParseSync(tdu, Instance.Warehouse);

        var resourceId = Convert.ToUInt32(value);

        Instance.Warehouse.GetById(resourceId).Then((r) =>
        {
            if (r != null)
            {
                SendReply(EpPacketReply.Completed, callback, r.Instance.Definition.Compose(this));
            }
            else
            {
                // reply failed
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ResourceNotFound);
            }
        });
    }



    void EpRequestGetResourceIdByLink(uint callback, PlainTdu tdu)
    {
        var parsed = Codec.ParseSync(tdu, Instance.Warehouse);
        var resourceLink = (string)parsed;

        Action<IResource> queryCallback = (r) =>
        {
            if (r == null)
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ResourceNotFound);
            else
            {
                if (r.Instance.Applicable(_session, ActionType.Attach, null) == Ruling.Denied)
                {
                    SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ResourceNotFound);
                    return;
                }

                SendReply(EpPacketReply.Completed, callback, r);
            }
        };

        if (Server?.EntryPoint != null)
            Server.EntryPoint.Query(resourceLink, this).Then(queryCallback);
        else
            Instance.Warehouse.Query(resourceLink).Then(queryCallback);

    }

    void EpRequestQueryResources(uint callback, PlainTdu tdu)
    {
        var parsed = Codec.ParseSync(tdu, Instance.Warehouse);

        var resourceLink = (string)parsed;

        Action<IResource> queryCallback = (r) =>
        {
            if (r == null)
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ResourceNotFound);
            else
            {
                if (r.Instance.Applicable(_session, ActionType.Attach, null) == Ruling.Denied)
                {
                    SendError(ErrorType.Management, callback, (ushort)ExceptionCode.NotAllowed);
                    return;
                }

                r.Instance.Children<IResource>().Then(children =>
                {
                    var list = children.Where(x => x.Instance.Applicable(_session, ActionType.Attach, null) != Ruling.Denied).ToArray();
                    SendReply(EpPacketReply.Completed, callback, list);
                }).Error(e =>
                {
                    SendError(e.Type, callback, (ushort)e.Code, e.Message);
                });
            }
        };

        if (Server?.EntryPoint != null)
            Server.EntryPoint.Query(resourceLink, this)
                             .Then(queryCallback)
                             .Error(e => SendError(e.Type, callback, (ushort)e.Code, e.Message));
        else
            Instance.Warehouse.Query(resourceLink)
                             .Then(queryCallback)
                             .Error(e => SendError(e.Type, callback, (ushort)e.Code, e.Message));
    }

    void EpRequestResourceAttribute(uint callback, uint resourceId)
    {

    }


    private Tuple<ushort, string> SummerizeException(Exception ex)
    {
        ex = ex.InnerException != null ? ex.InnerException : ex;

        var code = (ExceptionLevel & ExceptionLevel.Code) == 0 ? 0 : ex is AsyncException ae ? ae.Code : 0;
        var msg = (ExceptionLevel & ExceptionLevel.Message) == 0 ? "" : ex.Message;
        var source = (ExceptionLevel & ExceptionLevel.Source) == 0 ? "" : ex.Source;
        var trace = (ExceptionLevel & ExceptionLevel.Trace) == 0 ? "" : ex.StackTrace;

        return new Tuple<ushort, string>((ushort)code, $"{source}: {msg}\n{trace}");
    }


    void EpRequestProcedureCall(uint callback, PlainTdu tdu)
    {
        var (offset, length, args) = DataDeserializer.LimitedCountListParser(tdu.Data, tdu.PayloadOffset,
                                                                     tdu.PayloadLength, Instance.Warehouse, 1);

        var procedureCall = (string)args[0];

        if (Server == null)
        {
            SendError(ErrorType.Management, callback, (ushort)ExceptionCode.NotSupported);
            return;
        }

        var call = Server.Calls[procedureCall];

        if (call == null)
        {
            SendError(ErrorType.Management, callback, (ushort)ExceptionCode.MethodNotFound);
            return;
        }

        Codec.ParseAsync(tdu.Data, offset, this, null).Then(pr =>
        {
            if (pr.Value is AsyncReply reply)
            {
                reply.Then(results =>
                {
                    //var arguments = (Map<byte, object>)results;

                    // un hold the socket to send data immediately
                    this.Socket.Unhold();

                    // @TODO: Make managers for procedure calls
                    //if (r.Instance.Applicable(session, ActionType.Execute, ft) == Ruling.Denied)
                    //{
                    //    SendError(ErrorType.Management, callback,
                    //        (ushort)ExceptionCode.InvokeDenied);
                    //    return;
                    //}

                    InvokeFunction(call.Value.Definition, callback, results, EpPacketRequest.ProcedureCall, call.Value.Delegate.Target);

                }).Error(x =>
                {
                    SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ParseError);
                });
            }
            else
            {
                //var arguments = (Map<byte, object>)parsed;

                // un hold the socket to send data immediately
                this.Socket.Unhold();

                // @TODO: Make managers for procedure calls
                InvokeFunction(call.Value.Definition, callback, pr.Value, EpPacketRequest.ProcedureCall, call.Value.Delegate.Target);
            }
        }).Error(x => SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ParseError));
    }

    void EpRequestStaticCall(uint callback, PlainTdu tdu)
    {
        var (offset, length, args) = DataDeserializer.LimitedCountListParser(tdu.Data, tdu.PayloadOffset,
                                                                     tdu.PayloadLength, Instance.Warehouse, 2);

        var typeId = Convert.ToUInt32(args[0]);
        var index = (byte)args[1];

        var typeDef = Instance.Warehouse.GetLocalTypeDefById(typeId);

        if (typeDef == null)
        {
            SendError(ErrorType.Management, callback, (ushort)ExceptionCode.TypeDefNotFound);
            return;
        }

        var fd = typeDef.GetFunctionDefByIndex(index);

        if (fd == null)
        {
            // no function at this index
            SendError(ErrorType.Management, callback, (ushort)ExceptionCode.MethodNotFound);
            return;
        }

        var fi = fd.MethodInfo;

        if (fi == null)
        {
            // ft found, fi not found, this should never happen
            SendError(ErrorType.Management, callback, (ushort)ExceptionCode.MethodNotFound);
            return;
        }

        Codec.ParseAsync(tdu.Data, offset, this, null).Then(pr =>
        {
            if (pr.Value is AsyncReply reply)
            {
                reply.Then(results =>
                {
                    //var arguments = (Map<byte, object>)results;

                    // un hold the socket to send data immediately
                    this.Socket.Unhold();


                    // @TODO: Make managers for static calls
                    //if (r.Instance.Applicable(session, ActionType.Execute, ft) == Ruling.Denied)
                    //{
                    //    SendError(ErrorType.Management, callback,
                    //        (ushort)ExceptionCode.InvokeDenied);
                    //    return;
                    //}

                    InvokeFunction(fd, callback, results, EpPacketRequest.StaticCall, null);

                }).Error(x =>
                {
                    SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ParseError);
                });
            }
            else
            {
                //var arguments = (Map<byte, object>)parsed;

                // un hold the socket to send data immediately
                this.Socket.Unhold();

                // @TODO: Make managers for static calls


                InvokeFunction(fd, callback, pr.Value, EpPacketRequest.StaticCall, null);
            }
        }).Error(x => SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ParseError));
    }

    void EpRequestInvokeFunction(uint callback, PlainTdu tdu)
    {
        var (offset, length, args) = DataDeserializer.LimitedCountListParser(tdu.Data, tdu.PayloadOffset,
                                                                             tdu.PayloadLength, Instance.Warehouse, 2);

        var resourceId = Convert.ToUInt32(args[0]);
        var index = (byte)args[1];

        Instance.Warehouse.GetById(resourceId).Then((r) =>
        {
            if (r == null)
            {
                // no resource with this id
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ResourceNotFound);
                return;
            }

            var ft = r.Instance.Definition.GetFunctionDefByIndex(index);

            if (ft == null)
            {
                // no function at this index
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.MethodNotFound);
                return;
            }

            Codec.ParseAsync(tdu.Data, offset, this, null).Then(pr =>
            {
                if (pr.Value is AsyncReply asyncReply)
                {
                    asyncReply.Then(result =>
                    {
                        // var arguments = result;

                        // un hold the socket to send data immediately
                        this.Socket.Unhold();

                        if (r is EpResource)
                        {
                            var rt = (r as EpResource)._Invoke(index, result);
                            if (rt != null)
                            {
                                rt.Then(res =>
                                {
                                    SendReply(EpPacketReply.Completed, callback, res);
                                });
                            }
                            else
                            {
                                // function not found on a distributed object
                                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.MethodNotFound);
                            }
                        }
                        else
                        {
                            if (r.Instance.Applicable(_session, ActionType.Execute, ft) == Ruling.Denied)
                            {
                                SendError(ErrorType.Management, callback,
                                    (ushort)ExceptionCode.InvokeDenied);
                                return;
                            }

                            InvokeFunction(ft, callback, result, EpPacketRequest.InvokeFunction, r);
                        }
                    });
                }
                else
                {
                    //var arguments = (Map<byte, object>)parsed;

                    // un hold the socket to send data immediately
                    this.Socket.Unhold();

                    if (r is EpResource)
                    {
                        var rt = (r as EpResource)._Invoke(index, pr.Value);
                        if (rt != null)
                        {
                            rt.Then(res =>
                            {
                                SendReply(EpPacketReply.Completed, callback, res);
                            });
                        }
                        else
                        {
                            // function not found on a distributed object
                            SendError(ErrorType.Management, callback, (ushort)ExceptionCode.MethodNotFound);
                        }
                    }
                    else
                    {
                        if (r.Instance.Applicable(_session, ActionType.Execute, ft) == Ruling.Denied)
                        {
                            SendError(ErrorType.Management, callback,
                                (ushort)ExceptionCode.InvokeDenied);
                            return;
                        }

                        InvokeFunction(ft, callback, pr.Value, EpPacketRequest.InvokeFunction, r);
                    }
                }
            }).Error(x => SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ParseError)); ;
        }).Error(x => SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ParseError)); ;
    }



    void InvokeFunction(FunctionDef ft, uint callback, object arguments, EpPacketRequest actionType, object target = null)
    {

        // cast arguments
        ParameterInfo[] pis = ft.MethodInfo.GetParameters();

        object[] args = new object[pis.Length];

        InvocationContext context = null;

        if (pis.Length > 0)
        {
            if (pis.Last().ParameterType == typeof(EpConnection))
            {
                if (arguments is Map<byte, object> indexedArguments)
                {
                    for (byte i = 0; i < pis.Length - 1; i++)
                    {
                        if (indexedArguments.ContainsKey(i))
                            args[i] = RuntimeCaster.Cast(indexedArguments[i], pis[i].ParameterType);
                        else if (ft.Arguments[i].Type.Nullable)
                            args[i] = null;
                        else
                            args[i] = Type.Missing;
                    }
                }
                else if (arguments is object[] arrayArguments)
                {
                    for (var i = 0; (i < arrayArguments.Length) && (i < pis.Length - 1); i++)
                    {
                        args[i] = RuntimeCaster.Cast(arrayArguments[i], pis[i].ParameterType);
                    }

                    for (var i = arrayArguments.Length; i < pis.Length - 1; i++)
                    {
                        args[i] = Type.Missing;
                    }
                }
                else
                {
                    // assume first argument
                    // Note: if object[] is intended, sender should send nest it withing object[] { object[] }
                    if (pis.Length > 1)
                        args[0] = RuntimeCaster.Cast(arguments, pis[0].ParameterType);
                }

                args[args.Length - 1] = this;
            }
            else if (pis.Last().ParameterType == typeof(InvocationContext))
            {
                context = new InvocationContext(this, callback);
                if (arguments is Map<byte, object> indexedArguments)
                {
                    for (byte i = 0; i < pis.Length - 1; i++)
                    {
                        if (indexedArguments.ContainsKey(i))
                            args[i] = RuntimeCaster.Cast(indexedArguments[i], pis[i].ParameterType);
                        else if (ft.Arguments[i].Type.Nullable)
                            args[i] = null;
                        else
                            args[i] = Type.Missing;

                    }
                }
                else if (arguments is object[] arrayArguments)
                {
                    for (var i = 0; (i < arrayArguments.Length) && (i < pis.Length - 1); i++)
                    {
                        args[i] = RuntimeCaster.Cast(arrayArguments[i], pis[i].ParameterType);
                    }

                    for (var i = arrayArguments.Length; i < pis.Length - 1; i++)
                    {
                        args[i] = Type.Missing;
                    }
                }
                else
                {
                    // assume first argument
                    // Note: if object[] is intended, sender should send nest it withing object[] { object[] }
                    if (pis.Length > 1)
                        args[0] = RuntimeCaster.Cast(arguments, pis[0].ParameterType);

                    //throw new NotImplementedException("Arguments type not supported.");
                }

                args[args.Length - 1] = context;

            }
            else
            {
                if (arguments is Map<byte, object> indexedArguments)
                {
                    for (byte i = 0; i < pis.Length; i++)
                    {
                        if (indexedArguments.ContainsKey(i))
                            args[i] = RuntimeCaster.Cast(indexedArguments[i], pis[i].ParameterType);
                        else if (ft.Arguments[i].Type.Nullable) //Nullable.GetUnderlyingType(pis[i].ParameterType) != null)
                            args[i] = null;
                        else
                            args[i] = Type.Missing;
                    }
                }
                else if (arguments is object[] arrayArguments)
                {
                    for (var i = 0; (i < arrayArguments.Length) && (i < pis.Length); i++)
                    {
                        args[i] = RuntimeCaster.Cast(arrayArguments[i], pis[i].ParameterType);
                    }

                    for (var i = arrayArguments.Length; i < pis.Length; i++)
                    {
                        args[i] = Type.Missing;
                    }
                }
                else
                {
                    // assume first argument
                    // Note: if object[] is intended, sender should send nest it withing object[] { object[] }
                    if (pis.Length > 0)
                        args[0] = RuntimeCaster.Cast(arguments, pis[0].ParameterType);
                }

            }
        }

        object rt;

        try
        {
            rt = ft.MethodInfo.Invoke(target, args);
        }
        catch (Exception ex)
        {
            var (code, msg) = SummerizeException(ex);
            msg = "Arguments: " + string.Join(", ", args.Select(x => x?.ToString() ?? "[Null]").ToArray()) + "\r\n" + msg;

            SendError(ErrorType.Exception, callback, code, msg);
            return;
        }

        if (rt is IAsyncEnumerable<object>)
        {
            var enu = rt as IAsyncEnumerable<object>;
            var enumerator = enu.GetAsyncEnumerator();
            Task.Run(async () =>
            {
                try
                {
                    while (await enumerator.MoveNextAsync())
                    {
                        var v = enumerator.Current;
                        SendChunk(callback, v);
                    }

                    SendReply(EpPacketReply.Completed, callback);

                    if (context != null)
                        context.Ended = true;
                }
                catch (Exception ex)
                {
                    if (context != null)
                        context.Ended = true;

                    var (code, msg) = SummerizeException(ex);
                    SendError(ErrorType.Exception, callback, code, msg);
                }
            });
        }
        else if (rt is System.Collections.IEnumerable && !(rt is Array || rt is Map<string, object> || rt is string))
        {
            var enu = rt as System.Collections.IEnumerable;

            try
            {
                foreach (var v in enu)
                    SendChunk(callback, v);

                SendReply(EpPacketReply.Completed, callback);

                if (context != null)
                    context.Ended = true;

            }
            catch (Exception ex)
            {
                if (context != null)
                    context.Ended = true;

                var (code, msg) = SummerizeException(ex);
                SendError(ErrorType.Exception, callback, code, msg);
            }

        }
        else if (rt is Task)
        {
            (rt as Task).ContinueWith(t =>
            {
                if (context != null)
                    context.Ended = true;

#if NETSTANDARD
                var res = t.GetType().GetTypeInfo().GetProperty("Result").GetValue(t);
#else
                var res = t.GetType().GetProperty("Result").GetValue(t);
#endif

                SendReply(EpPacketReply.Completed, callback, res);
            });

        }
        else if (rt is AsyncReply)
        {
            (rt as AsyncReply).Then(res =>
            {
                if (context != null)
                    context.Ended = true;

                SendReply(EpPacketReply.Completed, callback, res);

            }).Error(ex =>
            {
                var (code, msg) = SummerizeException(ex);
                SendError(ErrorType.Exception, callback, code, msg);
            }).Progress((pt, pv, pm) =>
            {
                SendProgress(callback, pv, pm);
            }).Chunk(v =>
            {
                SendChunk(callback, v);
            }).Warning((level, message) =>
            {
                SendError(ErrorType.Warning, callback, level, message);
            });
        }
        else
        {
            if (context != null)
                context.Ended = true;

            SendReply(EpPacketReply.Completed, callback, rt);
        }
    }

    void EpRequestSubscribe(uint callback, PlainTdu tdu)
    {

        var (offset, length, args) = DataDeserializer.LimitedCountListParser(tdu.Data, tdu.PayloadOffset,
                                                                     tdu.PayloadLength, Instance.Warehouse);

        var resourceId = Convert.ToUInt32(args[0]);
        var index = (byte)args[1];

        Instance.Warehouse.GetById(resourceId).Then((r) =>
        {
            if (r == null)
            {
                // resource not found
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ResourceNotFound);
                return;
            }

            var et = r.Instance.Definition.GetEventDefByIndex(index);

            if (et != null)
            {
                // et not found
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.MethodNotFound);
                return;
            }

            if (r is EpResource)
            {
                (r as EpResource).Subscribe(et).Then(x =>
               {
                   SendReply(EpPacketReply.Completed, callback);
               }).Error(x => SendError(ErrorType.Exception, callback, (ushort)ExceptionCode.GeneralFailure));
            }
            else
            {
                lock (_subscriptionsLock)
                {
                    if (!_subscriptions.ContainsKey(r))
                    {
                        SendError(ErrorType.Management, callback, (ushort)ExceptionCode.NotAttached);
                        return;
                    }

                    if (_subscriptions[r].Contains(index))
                    {
                        SendError(ErrorType.Management, callback, (ushort)ExceptionCode.AlreadyListened);
                        return;
                    }

                    _subscriptions[r].Add(index);

                    SendReply(EpPacketReply.Completed, callback);
                }
            }
        });

    }

    void EpRequestUnsubscribe(uint callback, PlainTdu tdu)
    {

        var (offset, length, args) = DataDeserializer.LimitedCountListParser(tdu.Data, tdu.PayloadOffset,
                                                                     tdu.PayloadLength, Instance.Warehouse);

        var resourceId = Convert.ToUInt32(args[0]);
        var index = (byte)args[1];

        Instance.Warehouse.GetById(resourceId).Then((r) =>
        {
            if (r == null)
            {
                // resource not found
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ResourceNotFound);
                return;
            }

            var et = r.Instance.Definition.GetEventDefByIndex(index);

            if (et == null)
            {
                // pt not found
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.MethodNotFound);
                return;
            }

            if (r is EpResource)
            {
                (r as EpResource).Unsubscribe(et).Then(x =>
                {
                    SendReply(EpPacketReply.Completed, callback);
                }).Error(x => SendError(ErrorType.Exception, callback, (ushort)ExceptionCode.GeneralFailure));
            }
            else
            {
                lock (_subscriptionsLock)
                {
                    if (!_subscriptions.ContainsKey(r))
                    {
                        SendError(ErrorType.Management, callback, (ushort)ExceptionCode.NotAttached);
                        return;
                    }

                    if (!_subscriptions[r].Contains(index))
                    {
                        SendError(ErrorType.Management, callback, (ushort)ExceptionCode.AlreadyUnsubscribed);
                        return;
                    }

                    _subscriptions[r].Remove(index);

                    SendReply(EpPacketReply.Completed, callback);
                }
            }
        });
    }




    void EpRequestSetProperty(uint callback, PlainTdu tdu)
    {

        var (offset, length, args) = DataDeserializer.LimitedCountListParser(tdu.Data, tdu.PayloadOffset,
                                                                     tdu.PayloadLength, Instance.Warehouse, 2);

        var rid = (uint)args[0];
        var index = (byte)args[1];

        // un hold the socket to send data immediately
        this.Socket.Unhold();

        Instance.Warehouse.GetById(rid).Then((r) =>
        {
            if (r == null)
            {
                // resource not found
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ResourceNotFound);
                return;
            }

            var pt = r.Instance.Definition.GetPropertyDefByIndex(index);

            if (pt != null)
            {
                // property not found
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.PropertyNotFound);
                return;
            }


            if (r is IDynamicResource dynamicResource)
            {
                Codec.ParseAsync(tdu.Data, offset, this, null).Then(pr =>
                {
                    if (pr.Value is AsyncReply asyncReply)
                    {
                        asyncReply.Then((value) =>
                        {
                            // propagation
                            dynamicResource.SetResourcePropertyAsync(index, value).Then((x) =>
                            {
                                SendReply(EpPacketReply.Completed, callback);
                            }).Error(x =>
                            {
                                SendError(x.Type, callback, (ushort)x.Code, x.Message);
                            });
                        });
                    }
                    else
                    {
                        // propagation
                        dynamicResource.SetResourcePropertyAsync(index, pr.Value).Then((x) =>
                        {
                            SendReply(EpPacketReply.Completed, callback);
                        }).Error(x =>
                        {
                            SendError(x.Type, callback, (ushort)x.Code, x.Message);
                        });
                    }
                }).Error(x => SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ParseError)); ;

            }
            else
            {
                var pi = pt.PropertyInfo;
                if (pi == null)
                {
                    // pt found, pi not found, this should never happen
                    SendError(ErrorType.Management, callback, (ushort)ExceptionCode.PropertyNotFound);
                    return;
                }

                if (r.Instance.Applicable(_session, ActionType.SetProperty, pt, this) == Ruling.Denied)
                {
                    SendError(ErrorType.Exception, callback, (ushort)ExceptionCode.SetPropertyDenied);
                    return;
                }

                if (!pi.CanWrite)
                {
                    SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ReadOnlyProperty);
                    return;
                }

                Codec.ParseAsync(tdu.Data, offset, this, null).Then(pr =>
                {



                    if (pr.Value is AsyncReply asyncReply)
                    {
                        asyncReply.Then((value) =>
                        {
                            if (pi.PropertyType.IsGenericType && pi.PropertyType.GetGenericTypeDefinition()
                                    == typeof(PropertyContext<>))
                            {
                                value = Activator.CreateInstance(pi.PropertyType, this, value);
                            }
                            else
                            {
                                // cast new value type to property type
                                value = RuntimeCaster.Cast(value, pi.PropertyType);
                            }

                            try
                            {
                                pi.SetValue(r, value);
                                SendReply(EpPacketReply.Completed, callback);
                            }
                            catch (Exception ex)
                            {
                                SendError(ErrorType.Exception, callback, 0, ex.Message);
                            }
                        });
                    }
                    else
                    {
                        var value = pr.Value;

                        if (pi.PropertyType.IsGenericType && pi.PropertyType.GetGenericTypeDefinition()
                                == typeof(PropertyContext<>))
                        {
                            value = Activator.CreateInstance(pi.PropertyType, this, value);
                            //value = new DistributedPropertyContext(this, value);
                        }
                        else
                        {
                            // cast new value type to property type
                            value = RuntimeCaster.Cast(value, pi.PropertyType);
                        }

                        try
                        {
                            pi.SetValue(r, value);
                            SendReply(EpPacketReply.Completed, callback);
                        }
                        catch (Exception ex)
                        {
                            SendError(ErrorType.Exception, callback, 0, ex.Message);
                        }
                    }
                }).Error(x => SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ParseError));
            }
        });
    }


    /// <summary>
    /// Get the TypeDef for a given type Id. 
    /// </summary>
    /// <param name="typeId">Type UUID.</param>
    /// <returns>TypeDef.</returns>
    //public AsyncReply<RemoteTypeDef> GetTypeDefById(ulong typeId)
    //{
    //    lock (_typeDefsLock)
    //    {
    //        if (_remoteTypeDefs.ContainsKey(typeId))
    //            return new AsyncReply<RemoteTypeDef>(_remoteTypeDefs[typeId]);
    //        else if (_typeDefsByIdRequests.ContainsKey(typeId))
    //            return _typeDefsByIdRequests[typeId];

    //        var reply = new AsyncReply<RemoteTypeDef>();
    //        _typeDefsByIdRequests.Add(typeId, reply);

    //        SendRequest(EpPacketRequest.TypeDefById, typeId)
    //                    .Then((result) =>
    //                    {
    //                        // @TODO: Solve for dependency deadlock
    //                        RemoteTypeDef.Parse(_remoteDomain, (byte[])result, this).Then(td =>
    //                        {
    //                            _typeDefsByIdRequests.Remove(typeId);
    //                            _remoteTypeDefs.Add(td.Id, td);
    //                            // register all remote TypeDefs to warehouse to be used in future parsing before the actual request for them arrives.
    //                            Instance.Warehouse.TryRegisterRemoteTypeDef(_remoteDomain, td);
    //                            reply.Trigger(td);
    //                        });
    //                    }).Error((ex) =>
    //                    {
    //                        reply.TriggerError(ex);
    //                    });

    //        return reply;
    //    }
    //}


    //public AsyncReply<RemoteTypeDef> GetTypeDefByName(string typeName)
    //{
    //    lock (_typeDefsLock)
    //    {
    //        var typeDef = _remoteTypeDefs.Values.FirstOrDefault(x => x.Name == typeName);
    //        if (typeDef != null)
    //            return new AsyncReply<RemoteTypeDef>(typeDef);

    //        if (_typeDefsByNameRequests.ContainsKey(typeName))
    //            return _typeDefsByNameRequests[typeName];

    //        var reply = new AsyncReply<RemoteTypeDef>();
    //        _typeDefsByNameRequests.Add(typeName, reply);

    //        SendRequest(EpPacketRequest.TypeDefByName, typeName)
    //                    .Then((result) =>
    //                    {
    //                        RemoteTypeDef.Parse(_remoteDomain, (byte[])result, this).Then(td =>
    //                        {
    //                            _typeDefsByNameRequests.Remove(typeName);
    //                            _remoteTypeDefs.Add(td.Id, td);
    //                            Instance.Warehouse.TryRegisterRemoteTypeDef(_remoteDomain, td);
    //                            reply.Trigger(td);
    //                        });

    //                    }).Error((ex) =>
    //                    {
    //                        reply.TriggerError(ex);
    //                    });

    //        return reply;
    //    }
    //}

    // IStore interface 
    /// <summary>
    /// Get a resource by its path.
    /// </summary>
    /// <param name="path">Path to the resource.</param>
    /// <returns>Resource</returns>
    public AsyncReply<IResource> Get(string path)
    {

        var rt = new AsyncReply<IResource>();

        var req = SendRequest(EpPacketRequest.GetResourceIdByLink, path);


        req.Then(result =>
            {
                // The resource is being handed to the application: remember it and publish its graph
                // once all reachable dependencies have attached.
                if (result is EpResource resource)
                    TrackDeliveredRoot(resource);

                rt.Trigger(result);
            }).Error(ex => rt.TriggerError(ex));


        //Query(path).Then(ar =>
        //{

        //    //if (filter != null)
        //    //  ar = ar?.Where(filter).ToArray();

        //    // MISSING: should dispatch the unused resources. 
        //    if (ar?.Length > 0)
        //        rt.Trigger(ar[0]);
        //    else
        //        rt.Trigger(null);
        //}).Error(ex => rt.TriggerError(ex));


        return rt;
    }


    public AsyncReply<RemoteTypeDef[]> GetLinkDefinitions(string link)
    {
        //throw new NotImplementedException();

        var reply = new AsyncReply<RemoteTypeDef[]>();


        SendRequest(EpPacketRequest.LinkTypeDefs, link)
        .Then(async (result) =>
        {

            var defs = new List<RemoteTypeDef>();

            foreach (var def in (byte[][])result)
            {
                var od = new RemoteTypeDef();
                await RemoteTypeDef.Parse(od, _remoteDomain, def, this, null);
                defs.Add(od);
            }

            reply.Trigger(defs.ToArray());

        }).Error((ex) =>
        {
            reply.TriggerError(ex);
        });

        return reply;
    }

    /// <summary>
    /// Fetch a resource from the other end
    /// </summary>
    /// <param name="id">Resource Id</param>
    /// <returns>DistributedResource</returns>
    /// 
    //object fetchResourceLock = new object();
    // Records that the fetch of `parent` is now blocked waiting on in-flight child `child`.
    static void AddFetchBlock<TId>(Dictionary<TId, HashSet<TId>> blockedOn, TId parent, TId child)
    {
        if (!blockedOn.TryGetValue(parent, out var set))
            blockedOn[parent] = set = new HashSet<TId>();
        set.Add(child);
    }

    void AddFetchBlock(uint parent, uint child) => AddFetchBlock(_fetchBlockedOn, parent, child);

    void AddTypeDefFetchBlock(ulong parent, ulong child) => AddFetchBlock(_typeDefFetchBlockedOn, parent, child);

    // Removes a resource from the wait-for graph once it is attached or its fetch failed: it is
    // no longer blocked on anything and no longer a pending child of anyone.
    static void ClearFetchNode<TId>(Dictionary<TId, HashSet<TId>> blockedOn, TId id)
    {
        blockedOn.Remove(id);
        foreach (var set in blockedOn.Values)
            set.Remove(id);
    }

    void ClearFetchNode(uint id) => ClearFetchNode(_fetchBlockedOn, id);

    void ClearTypeDefFetchNode(ulong id) => ClearFetchNode(_typeDefFetchBlockedOn, id);

    /// <summary>
    /// Returns true if completing the fetch of <paramref name="id"/> by waiting for its in-flight
    /// request would deadlock, i.e. the resource is (transitively) blocked on a resource that the
    /// current request chain is itself building. In that case the caller should hand back the
    /// placeholder to break the cycle instead of waiting.
    /// </summary>
    internal static bool HasWaitForCycle(uint id, uint[] requestSequence, IReadOnlyDictionary<uint, HashSet<uint>> blockedOn)
        => HasWaitForCycleCore(id, requestSequence, blockedOn);

    internal static bool HasWaitForCycle(ulong id, ulong[] requestSequence, IReadOnlyDictionary<ulong, HashSet<ulong>> blockedOn)
        => HasWaitForCycleCore(id, requestSequence, blockedOn);

    static bool HasWaitForCycleCore<TId>(TId id, TId[] requestSequence, IReadOnlyDictionary<TId, HashSet<TId>> blockedOn)
    {
        if (requestSequence == null || requestSequence.Length == 0)
            return false;

        var chain = new HashSet<TId>(requestSequence);
        var visited = new HashSet<TId>();
        var stack = new Stack<TId>();
        stack.Push(id);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!visited.Add(current))
                continue;

            if (!blockedOn.TryGetValue(current, out var children))
                continue;

            foreach (var child in children)
            {
                // Reaching a node that the current chain is attaching closes the cycle.
                if (chain.Contains(child))
                    return true;
                stack.Push(child);
            }
        }

        return false;
    }

    /// <summary>
    /// Publishes a fully-attached object graph to the application: every resource reachable from
    /// <paramref name="root"/> is marked <see cref="ResourceStatus.Published"/>, but only if the
    /// entire reachable graph is already attached. If any reachable resource is still being
    /// attached (e.g. a placeholder handed out to break a cycle), the graph is left unpublished —
    /// exactly the partially-attached delivery that the wait-by-default resolver prevents and the
    /// legacy resolver does not.
    /// </summary>
    internal bool PublishGraph(EpResource root)
    {
        if (root == null)
            return true;

        var seen = new HashSet<uint>();
        var reachable = new List<EpResource>();
        var queue = new Queue<EpResource>();
        queue.Enqueue(root);

        var fullyAttached = true;

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            if (node == null || !seen.Add(node.ResourceInstanceId))
                continue;

            reachable.Add(node);

            if (node.Status != ResourceStatus.Attached && node.Status != ResourceStatus.Published)
            {
                fullyAttached = false;
                continue; // do not traverse into a not-yet-attached node
            }

            foreach (var child in node.GetReferencedResources())
                queue.Enqueue(child);
        }

        if (fullyAttached)
            foreach (var node in reachable)
                node.Publish();

        return fullyAttached;
    }

    void TrackDeliveredRoot(EpResource root)
    {
        if (PublishGraph(root))
            return;

        lock (_deliveredRootsLock)
            _deliveredRoots[root.ResourceInstanceId] = new WeakReference<EpResource>(root);

        TryPublishDeliveredRoots();
    }

    void TryPublishDeliveredRoots()
    {
        lock (_deliveredRootsLock)
        {
            var stale = new List<uint>();
            foreach (var pair in _deliveredRoots)
            {
                if (!pair.Value.TryGetTarget(out var root) || PublishGraph(root))
                    stale.Add(pair.Key);
            }

            foreach (var key in stale)
                _deliveredRoots.Remove(key);
        }
    }

    public AsyncReply<EpResource> FetchResource(uint id, uint[] requestSequence)
    {
        //lock (fetchLock)
        //{
        EpResource resource = null;

        _attachedResources[id]?.TryGetTarget(out resource);

        if (resource != null)
        {
            Global.Counters["EpResourceAttachedCacheHit"]++;
            return new AsyncReply<EpResource>(resource);
        }

        resource = _neededResources[id];

        var requestInfo = _resourceRequests[id];

        // The resource that triggered this fetch (the tail of the chain), if any. Used to record
        // wait-for edges and to tell graph-internal references from app-facing fetches (no chain).
        uint? parent = requestSequence != null && requestSequence.Length > 0
            ? requestSequence[requestSequence.Length - 1]
            : (uint?)null;

        if (requestInfo != null)
        {
            // Same dependency chain (A->B->A): the placeholder is an internal node of the graph
            // currently being attached. The application only observes the chain's top-level reply,
            // which fires after full attachment, so returning the not-yet-attached placeholder here
            // is safe and breaks the reference cycle. NaiveWait skips this so that even same-chain
            // cycles deadlock (used to demonstrate the protection is necessary).
            if (DeadlockResolution != DeadlockResolutionMode.NaiveWait
                && resource != null && (requestSequence?.Contains(id) ?? false))
            {
                Global.Counters["EpResourceDeadLockSameChain"]++;
                CycleBreakCount++;
                return new AsyncReply<EpResource>(resource);
            }

            // Decide whether to break the wait by returning the placeholder:
            //  - Legacy: hand it to ANY cross-chain requester (over-eager; the bug under study).
            //  - WaitWithCycleDetection: only on a genuine wait-for cycle.
            //  - NaiveWait: never — always wait below (deadlocks on cycles).
            var breakCycle = resource != null && DeadlockResolution switch
            {
                DeadlockResolutionMode.LegacyCrossChainPlaceholder => requestInfo.RequestSequence.Contains(id),
                DeadlockResolutionMode.WaitWithCycleDetection => HasWaitForCycle(id, requestSequence, _fetchBlockedOn),
                _ => false,
            };

            if (breakCycle)
            {
                Global.Counters["EpResourceDeadLockCrossChain"]++;
                CycleBreakCount++;

                // Instrumentation: a placeholder handed out where there is no genuine wait-for cycle
                // is an unnecessary, partial delivery — the new resolver would have waited for full
                // attachment instead. This counts the legacy resolver's over-eager placeholders.
                if (DeadlockResolution == DeadlockResolutionMode.LegacyCrossChainPlaceholder
                    && !HasWaitForCycle(id, requestSequence, _fetchBlockedOn))
                {
                    Global.Counters["EpResourceUnnecessaryPlaceholder"]++;
                    UnnecessaryPlaceholderCount++;
                }

                return new AsyncReply<EpResource>(resource);
            }

            // Otherwise an independent or application-facing requester: wait for the in-flight
            // attachment to complete fully rather than exposing a partially attached resource.
            Global.Counters["EpResourcePendingCacheHit"]++;
            if (parent != null)
                AddFetchBlock(parent.Value, id);
            return requestInfo.Reply;
        }
        else if (resource != null && resource.Status != ResourceStatus.Suspended)
        {
            // @REVIEW: this should never happen
            Global.Log("DCON", LogType.Error, "Resource not moved to attached.");
            return new AsyncReply<EpResource>(resource);

        }

        var newSequence = requestSequence != null ? requestSequence.Concat(new uint[] { id }).ToArray() : new uint[] { id };

        var reply = new AsyncReply<EpResource>();
        _resourceRequests.Add(id, new FetchRequestInfo<EpResource, uint>(reply, newSequence));

        // This fetch's parent now waits on `id` until it attaches.
        if (parent != null)
            AddFetchBlock(parent.Value, id);

        SendRequest(EpPacketRequest.AttachResource, id)
                    .Then((result) =>
                    {
                        if (result == null)
                        {
                            reply.TriggerError(new AsyncException(ErrorType.Management,
                                    (ushort)ExceptionCode.ResourceNotFound, "Null response"));
                            return;
                        }

                        // TypeId, Age, Link, Hops, PropertyValue[]
                        var args = (object[])result;
                        var typeId = Convert.ToUInt32(args[0]);
                        var age = Convert.ToUInt64(args[1]);
                        var link = (string)args[2];
                        var hops = (byte)args[3];
                        var pvData = (byte[])args[4];


                        var typeDef = resource != null ?
                                      resource.Instance.Definition as RemoteTypeDef
                                      : Instance.Warehouse.GetRemoteTypeDefById(
                                                        _remoteDomain,
                                                        typeId
                                                        );


                        var initResource = (EpResource dr) =>
                        {
                            var parsedReply = DataDeserializer.PropertyValueArrayParserAsync(pvData, 0, (uint)pvData.Length, this, newSequence);

                            parsedReply.Then(results =>
                            {
                                var pvs = results as PropertyValue[];

                                dr._Attach(pvs);
                                // Progress signal: a resource has fully attached. Used by tests to
                                // distinguish a true deadlock (no progress while requests pend) from
                                // merely slow processing (these counters keep advancing).
                                Global.Counters["EpResourceAttached"]++;
                                AttachedResourceCount++;
                                _resourceRequests.Remove(id);
                                // move from needed to attached.
                                _neededResources.Remove(id);
                                _attachedResources[id] = new WeakReference<EpResource>(dr);
                                // attached: no longer part of the in-flight wait-for graph.
                                ClearFetchNode(id);
                                TryPublishDeliveredRoots();
                                reply.Trigger(dr);
                            }).Error(ex => { _resourceRequests.Remove(id); ClearFetchNode(id); reply.TriggerError(ex); });
                        };

                        if (typeDef == null)
                        {
                            FetchTypeDef(typeId, null).Then((td) =>
                            {
                                // typeId, ResourceAge, ResourceLink, Content
                                if (resource == null)
                                {
                                    if (td.ProxyType != null)
                                        resource = Activator.CreateInstance(td.ProxyType, this, id, Convert.ToUInt64(args[1]), (string)args[2]) as EpResource;
                                    else
                                        resource = new EpResource(this, id, Convert.ToUInt64(args[1]), (string)args[2]);

                                    resource.ResourceDefinition = td;
                                    typeDef = td;
                                    // Register the placeholder before parsing properties so cyclic
                                    // references in the graph can resolve back to this instance.
                                    _neededResources[id] = resource;
                                    Instance.Warehouse.Put(Instance.Link + "/" + id.ToString(), resource)
                                        .Then(initResource)
                                        .Error(ex => reply.TriggerError(ex));

                                }
                                else
                                {
                                    initResource(resource);
                                }
                            }).Error((ex) =>
                            {
                                reply.TriggerError(ex);
                            });
                        }
                        else
                        {
                            if (resource == null)
                            {
                                if (typeDef.ProxyType != null)
                                    resource = Activator.CreateInstance(typeDef.ProxyType, this, id, Convert.ToUInt64(args[1]), (string)args[2]) as EpResource;
                                else
                                    resource = new EpResource(this, id, Convert.ToUInt64(args[1]), (string)args[2]);

                                resource.ResourceDefinition = typeDef;

                                // Register the placeholder before parsing properties so cyclic
                                // references in the graph can resolve back to this instance.
                                _neededResources[id] = resource;
                                Instance.Warehouse.Put(this.Instance.Link + "/" + id.ToString(), resource)
                                    .Then(initResource).Error((ex) => reply.TriggerError(ex));
                            }
                            else
                            {
                                initResource(resource);
                            }
                        }

                    }).Error((ex) =>
                    {
                        // Failed to attach: drop the in-flight request and wait-for edges so a
                        // later retry is not blocked by a stale entry.
                        _resourceRequests.Remove(id);
                        ClearFetchNode(id);
                        reply.TriggerError(ex);
                    });


        return reply;
        //}
    }



    /// <summary>
    /// Fetch a resource from the other end
    /// </summary>
    /// <param name="id">Resource Id</param>
    /// <returns>DistributedResource</returns>
    /// 
    /// <summary>
    /// Re-attaches an already-known resource after reconnection using its last-known age. The peer
    /// returns only the properties modified after <paramref name="age"/> (the delta), which are
    /// merged into the existing instance instead of re-fetching everything. Falls back to a full
    /// <see cref="FetchResource"/> if there is no prior state to merge into.
    /// </summary>
    public AsyncReply<EpResource> Reattach(uint id, ulong age, EpResource resource)
    {
        EpResource attachedResource = null;
        _attachedResources[id]?.TryGetTarget(out attachedResource);
        if (attachedResource != null)
            return new AsyncReply<EpResource>(attachedResource);

        var existing = _resourceRequests[id];
        if (existing != null)
            return existing.Reply;

        var reply = new AsyncReply<EpResource>();
        var sequence = new uint[] { id };
        _resourceRequests.Add(id, new FetchRequestInfo<EpResource, uint>(reply, sequence));

        SendRequest(EpPacketRequest.ReattachResource, id, age).Then(result =>
        {
            if (result == null)
            {
                _resourceRequests.Remove(id);
                reply.TriggerError(new AsyncException(ErrorType.Management,
                        (ushort)ExceptionCode.ResourceNotFound, "Null response"));
                return;
            }

            // typeId, age, link, hops, delta(index -> PropertyValue)
            var args = (object[])result;
            var deltaData = (byte[])args[4];

            DataDeserializer.PropertyValueMapParserAsync(deltaData, 0, (uint)deltaData.Length, this, sequence)
                .Then(delta =>
                {
                    if (!resource._Reattach(delta))
                    {
                        // No prior state to merge into — perform a full attach instead.
                        _resourceRequests.Remove(id);
                        FetchResource(id, null).Then(r => reply.Trigger(r)).Error(ex => reply.TriggerError(ex));
                        return;
                    }

                    _resourceRequests.Remove(id);
                    _neededResources.Remove(id);
                    _attachedResources[id] = new WeakReference<EpResource>(resource);
                    ClearFetchNode(id);
                    TryPublishDeliveredRoots();
                    reply.Trigger(resource);
                })
                .Error(ex => { _resourceRequests.Remove(id); ClearFetchNode(id); reply.TriggerError(ex); });
        }).Error(ex =>
        {
            _resourceRequests.Remove(id);
            ClearFetchNode(id);
            reply.TriggerError(ex);
        });

        return reply;
    }

    //object fetchResourceLock = new object();
    public AsyncReply<RemoteTypeDef> FetchTypeDef(ulong id, ulong[] requestSequence)
    {
        //Console.WriteLine($"Fetching typedef {id} {Instance.Warehouse.GetHashCode()}");

        RemoteTypeDef typeDef = _cachedTypeDefs[id];

        if (typeDef != null)
            return new AsyncReply<RemoteTypeDef>(typeDef);

        typeDef = _neededTypeDefs[id];

        var requestInfo = _typeDefRequests[id];

        // The type definition that triggered this fetch (the tail of the chain), if any. Used to
        // record wait-for edges and to distinguish graph-internal typedef parsing from
        // application-facing fetches.
        ulong? parent = requestSequence != null && requestSequence.Length > 0
            ? requestSequence[requestSequence.Length - 1]
            : (ulong?)null;

        if (requestInfo != null)
        {
            if (DeadlockResolution != DeadlockResolutionMode.NaiveWait
                && typeDef != null && (requestSequence?.Contains(id) ?? false))
            {
                // Same dependency chain (A->B->A): return the in-progress placeholder to break
                // the reference cycle. NaiveWait skips this for deadlock detection experiments.
                return new AsyncReply<RemoteTypeDef>(typeDef);
            }

            var breakCycle = typeDef != null && DeadlockResolution switch
            {
                DeadlockResolutionMode.LegacyCrossChainPlaceholder => requestInfo.RequestSequence.Contains(id),
                DeadlockResolutionMode.WaitWithCycleDetection => HasWaitForCycle(id, requestSequence, _typeDefFetchBlockedOn),
                _ => false,
            };

            if (breakCycle)
            {
                return new AsyncReply<RemoteTypeDef>(typeDef);
            }

            if (parent != null)
                AddTypeDefFetchBlock(parent.Value, id);
            return requestInfo.Reply;
        }

        //Console.WriteLine($"Sent typedef {id} {Instance.Warehouse.GetHashCode()}");

        var newSequence = requestSequence != null ? requestSequence.Concat(new ulong[] { id }).ToArray() : new ulong[] { id };

        var reply = new AsyncReply<RemoteTypeDef>();
        _typeDefRequests.Add(id, new FetchRequestInfo<RemoteTypeDef, ulong>(reply, newSequence));

        if (parent != null)
            AddTypeDefFetchBlock(parent.Value, id);

        SendRequest(EpPacketRequest.TypeDefById, id)
                    .Then((result) =>
                    {
                        if (result == null)
                        {
                            _typeDefRequests.Remove(id);
                            ClearTypeDefFetchNode(id);
                            reply.TriggerError(new AsyncException(ErrorType.Management,
                                    (ushort)ExceptionCode.ResourceNotFound, "Null response"));
                            return;
                        }

                        // TypeDef Data
                        //var args = (object[])result;
                        var typeDefData = (byte[])result;

                        var od = new RemoteTypeDef();
                        _neededTypeDefs[id] = od;

                        RemoteTypeDef.Parse(od, this.RemoteDomain, typeDefData, this, newSequence).Then(td =>
                        {
                            _typeDefRequests.Remove(id);
                            // move from needed to attached.
                            _neededTypeDefs.Remove(id);
                            _cachedTypeDefs[id] = td;
                            ClearTypeDefFetchNode(id);

                            reply.Trigger(td);

                        }).Error(ex =>
                        {
                            _typeDefRequests.Remove(id);
                            _neededTypeDefs.Remove(id);
                            ClearTypeDefFetchNode(id);
                            reply.TriggerError(ex);
                        });

                    }).Error(ex =>
                    {
                        _typeDefRequests.Remove(id);
                        ClearTypeDefFetchNode(id);
                        reply.TriggerError(ex);
                    });

        return reply;

    }

    /// <summary>
    /// Query resources at specific link.
    /// </summary>
    /// <param name="path">Link path.</param>
    /// <returns></returns>
    public AsyncReply<IResource[]> Query(string path)
    {
        var str = DC.ToBytes(path);
        var reply = new AsyncReply<IResource[]>();

        SendRequest(EpPacketRequest.Query, path)
                    .Then(result =>
                    {
                        reply.Trigger((IResource[])result);
                    }).Error(ex => reply.TriggerError(ex));

        return reply;
    }

    public AsyncReply<ulong[]> GetTypeDefIds(string[] fullNames)
    {
        var reply = new AsyncReply<ulong[]>();

        SendRequest(EpPacketRequest.TypeDefIdsByNames, fullNames)
                    .Then(result =>
                    {
                        reply.Trigger((ulong[])result);
                    }).Error(ex => reply.TriggerError(ex));

        return reply;
    }

    /// <summary>
    /// Create a new resource.
    /// </summary>
    /// <param name="path">Resource path.</param>
    /// <param name="type">Type definition.</param>
    /// <param name="properties">Values for the resource properties.</param>
    /// <param name="attributes">Resource attributes.</param>
    /// <returns>New resource instance</returns>
    public AsyncReply<EpResource> Create(string path, TypeDef type, Map<string, object> properties, Map<string, object> attributes)
    {
        var reply = new AsyncReply<EpResource>();

        SendRequest(EpPacketRequest.CreateResource, path, type.Id, type.CastProperties(properties), attributes)
            .Then(r => reply.Trigger((EpResource)r))
            .Warning((l, m) => reply.TriggerWarning(l, m))
            .Error(e => reply.TriggerError(e));

        return reply;
    }

    private void Subscribe(IResource resource)
    {
        lock (_subscriptionsLock)
        {
            resource.Instance.EventOccurred += Instance_EventOccurred;
            resource.Instance.CustomEventOccurred += Instance_CustomEventOccurred;
            resource.Instance.PropertyModified += Instance_PropertyModified;
            resource.Instance.Destroyed += Instance_ResourceDestroyed;

            _subscriptions.Add(resource, new List<byte>());
        }
    }

    private void Unsubscribe(IResource resource)
    {
        lock (_subscriptionsLock)
        {
            // do something with the list...
            resource.Instance.EventOccurred -= Instance_EventOccurred;
            resource.Instance.CustomEventOccurred -= Instance_CustomEventOccurred;
            resource.Instance.PropertyModified -= Instance_PropertyModified;
            resource.Instance.Destroyed -= Instance_ResourceDestroyed;

            _subscriptions.Remove(resource);
        }

    }

    private void UnsubscribeAll()
    {
        lock (_subscriptionsLock)
        {
            foreach (var resource in _subscriptions.Keys)
            {
                resource.Instance.EventOccurred -= Instance_EventOccurred;
                resource.Instance.CustomEventOccurred -= Instance_CustomEventOccurred;
                resource.Instance.PropertyModified -= Instance_PropertyModified;
                resource.Instance.Destroyed -= Instance_ResourceDestroyed;
            }

            _subscriptions.Clear();
        }
    }

    private void Instance_ResourceDestroyed(IResource resource)
    {

        Unsubscribe(resource);

        // compose the packet
        SendNotification(EpPacketNotification.ResourceDestroyed, resource.Instance.Id);
    }

    private void Instance_PropertyModified(PropertyModificationInfo info)
    {
        SendNotification(EpPacketNotification.PropertyModified,
                         info.Resource.Instance.Id,
                         info.PropertyDef.Index,
                         info.Value);
    }

    private void Instance_CustomEventOccurred(CustomEventOccurredInfo info)
    {
        if (info.EventDef.Subscribable)
        {
            lock (_subscriptionsLock)
            {
                // check the client requested listen
                if (!_subscriptions.ContainsKey(info.Resource))
                    return;

                if (!_subscriptions[info.Resource].Contains(info.EventDef.Index))
                    return;
            }
        }

        if (!info.Receivers(_session))
            return;

        if (info.Resource.Instance.Applicable(_session, ActionType.ReceiveEvent, info.EventDef, info.Issuer) == Ruling.Denied)
            return;


        // compose the packet
        SendNotification(EpPacketNotification.EventOccurred,
                          info.Resource.Instance.Id,
                          info.EventDef.Index,
                          info.Value);
    }

    private void Instance_EventOccurred(EventOccurredInfo info)
    {
        if (info.Definition.Subscribable)
        {
            lock (_subscriptionsLock)
            {
                // check the client requested listen
                if (!_subscriptions.ContainsKey(info.Resource))
                    return;

                if (!_subscriptions[info.Resource].Contains(info.Definition.Index))
                    return;
            }
        }

        if (info.Resource.Instance.Applicable(_session, ActionType.ReceiveEvent, info.Definition, null) == Ruling.Denied)
            return;

        // compose the packet
        SendNotification(EpPacketNotification.EventOccurred,
            info.Resource.Instance.Id,
            info.Definition.Index,
            info.Value);
    }



    void EpRequestKeepAlive(uint callback, PlainTdu tdu)
    {

        var (offset, length, args) = DataDeserializer.LimitedCountListParser(tdu.Data, tdu.PayloadOffset,
                                                                     tdu.PayloadLength, Instance.Warehouse);

        var peerTime = (DateTime)args[0];
        var interval = Convert.ToUInt32(args[1]);

        uint jitter = 0;

        var now = DateTime.UtcNow;

        if (_lastKeepAliveReceived != null)
        {
            var diff = (uint)(now - (DateTime)_lastKeepAliveReceived).TotalMilliseconds;
            jitter = (uint)Math.Abs((int)diff - (int)interval);
        }

        SendReply(EpPacketReply.Completed, callback, now, jitter);

        _lastKeepAliveReceived = now;
    }
}
