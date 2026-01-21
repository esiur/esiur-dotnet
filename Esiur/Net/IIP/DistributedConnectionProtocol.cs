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
using Esiur.Misc;
using Esiur.Net.Packets;
using Esiur.Resource;
using Esiur.Resource.Template;
using Esiur.Security.Authority;
using Esiur.Security.Permissions;
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

namespace Esiur.Net.IIP;

partial class DistributedConnection
{
    KeyList<uint, DistributedResource> neededResources = new KeyList<uint, DistributedResource>();
    KeyList<uint, WeakReference<DistributedResource>> attachedResources = new KeyList<uint, WeakReference<DistributedResource>>();
    KeyList<uint, WeakReference<DistributedResource>> suspendedResources = new KeyList<uint, WeakReference<DistributedResource>>();

    KeyList<uint, DistributedResourceAttachRequestInfo> resourceRequests = new KeyList<uint, DistributedResourceAttachRequestInfo>();
    KeyList<UUID, AsyncReply<TypeTemplate>> templateRequests = new KeyList<UUID, AsyncReply<TypeTemplate>>();

    KeyList<string, AsyncReply<TypeTemplate>> templateByNameRequests = new KeyList<string, AsyncReply<TypeTemplate>>();


    Dictionary<UUID, TypeTemplate> templates = new Dictionary<UUID, TypeTemplate>();

    KeyList<uint, AsyncReply> requests = new KeyList<uint, AsyncReply>();

    volatile uint callbackCounter = 0;

    Dictionary<IResource, List<byte>> subscriptions = new Dictionary<IResource, List<byte>>();

    // resources might get attached by the client
    internal KeyList<IResource, DateTime> cache = new();

    object subscriptionsLock = new object();

    AsyncQueue<DistributedResourceQueueItem> queue = new();



    /// <summary>
    /// Send IIP request.
    /// </summary>
    /// <param name="action">Packet action.</param>
    /// <param name="args">Arguments to send.</param>
    /// <returns></returns>
    AsyncReply SendRequest(IIPPacketRequest action, params object[] args)
    {
        var reply = new AsyncReply();
        var c = callbackCounter++; // avoid thread racing
        requests.Add(c, reply);

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
              .AddUInt8Array(Codec.Compose(args[0], this.Instance.Warehouse, this));
            Send(bl.ToArray());
        }
        else
        {
            var bl = new BinaryList();
            bl.AddUInt8((byte)(0x60 | (byte)action))
              .AddUInt32(c)
              .AddUInt8Array(Codec.Compose(args, this.Instance.Warehouse, this));
            Send(bl.ToArray());
        }

        return reply;
    }

    /// <summary>
    /// Send IIP notification.
    /// </summary>
    /// <param name="action">Packet action.</param>
    /// <param name="args">Arguments to send.</param>
    /// <returns></returns>
    AsyncReply SendNotification(IIPPacketNotification action, params object[] args)
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
              .AddUInt8Array(Codec.Compose(args[0], this.Instance.Warehouse, this));
            Send(bl.ToArray());
        }
        else
        {
            var bl = new BinaryList();
            bl.AddUInt8((byte)(0x20 | (byte)action))
              .AddUInt8Array(Codec.Compose(args, this.Instance.Warehouse, this));
            Send(bl.ToArray());
        }

        return reply;
    }

    void SendReply(IIPPacketReply action, uint callbackId, params object[] args)
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
              .AddUInt8Array(Codec.Compose(args[0], this.Instance.Warehouse, this));
            Send(bl.ToArray());
        }
        else
        {
            var bl = new BinaryList();
            bl.AddUInt8((byte)(0xA0 | (byte)action))
              .AddUInt32(callbackId)
              .AddUInt8Array(Codec.Compose(args, this.Instance.Warehouse, this));
            Send(bl.ToArray());
        }
    }


    internal AsyncReply SendSubscribeRequest(uint instanceId, byte index)
    {
        return SendRequest(IIPPacketRequest.Subscribe, instanceId, index);
    }

    internal AsyncReply SendUnsubscribeRequest(uint instanceId, byte index)
    {
        return SendRequest(IIPPacketRequest.Unsubscribe, instanceId, index);
    }


    public AsyncReply StaticCall(UUID classId, byte index, object parameters)
    {
        return SendRequest(IIPPacketRequest.StaticCall, classId, index, parameters);
    }

    public AsyncReply Call(string procedureCall, params object[] parameters)
    {
        //var args = new Map<byte, object>();
        //for (byte i = 0; i < parameters.Length; i++)
        //    args.Add(i, parameters[i]);
        //        return Call(procedureCall, parameters);

        return SendRequest(IIPPacketRequest.ProcedureCall, procedureCall, parameters);
    }

    public AsyncReply Call(string procedureCall, Map<byte, object> parameters)
    {
        return SendRequest(IIPPacketRequest.ProcedureCall, procedureCall, parameters);
    }

    internal AsyncReply SendInvoke(uint instanceId, byte index, object parameters)
    {
        return SendRequest(IIPPacketRequest.InvokeFunction, instanceId, index, parameters);
    }

    internal AsyncReply SendSetProperty(uint instanceId, byte index, object value)
    {
        return SendRequest(IIPPacketRequest.SetProperty, instanceId, index, value);
    }

    internal AsyncReply SendDetachRequest(uint instanceId)
    {
        try
        {
            var sendDetach = false;

            if (attachedResources.ContainsKey(instanceId))
            {
                attachedResources.Remove(instanceId);
                sendDetach = true;
            }

            if (suspendedResources.ContainsKey(instanceId))
            {
                suspendedResources.Remove(instanceId);
                sendDetach = true;
            }

            if (sendDetach)
                return SendRequest(IIPPacketRequest.DetachResource, instanceId);

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
            SendReply(IIPPacketReply.PermissionError, callbackId, errorCodeOrWarningLevel, message);
        else if (type == ErrorType.Exception)
            SendReply(IIPPacketReply.ExecutionError, callbackId, errorCodeOrWarningLevel, message);
        else if (type == ErrorType.Warning)
            SendReply(IIPPacketReply.Warning, callbackId, (byte)errorCodeOrWarningLevel, message);
    }

    internal void SendProgress(uint callbackId, uint value, uint max)
    {
        SendReply(IIPPacketReply.Progress, callbackId, value, max);
    }

    internal void SendWarning(uint callbackId, byte level, string message)
    {
        SendReply(IIPPacketReply.Warning, callbackId, level, message);
    }

    internal void SendChunk(uint callbackId, object chunk)
    {
        SendReply(IIPPacketReply.Chunk, callbackId, chunk);
    }

    void IIPReplyCompleted(uint callbackId, ParsedTDU dataType)
    {
        var req = requests.Take(callbackId);

        //Console.WriteLine("Completed " + callbackId);

        if (req == null)
        {
            // @TODO: Send general failure
            return;
        }

        var (_, parsed) = Codec.ParseAsync(dataType, this, null);
        if (parsed is AsyncReply reply)
        {
            reply.Then(result =>
            {
                req.Trigger(result);
            })
            .Error(e =>
            {
                //Console.WriteLine(callbackId + ": failed");
                req.TriggerError(e);
            });
        }
        else
        {
            req.Trigger(parsed);
        }
    }

    void IIPExtensionAction(byte actionId, ParsedTDU? dataType, byte[] data)
    {
        // nothing is supported now
    }

    void IIPReplyPropagated(uint callbackId, ParsedTDU dataType, byte[] data)
    {
        var req = requests[callbackId];

        if (req == null)
        {
            // @TODO: Send general failure
            return;
        }

        var (_, parsed) = Codec.ParseAsync(dataType, this, null);
        if (parsed is AsyncReply reply)
        {
            reply.Then(result =>
            {
                req.TriggerPropagation(result);
            });
        }
        else
        {
            req.TriggerPropagation(parsed);
        }
    }

    void IIPReplyError(uint callbackId, ParsedTDU dataType, byte[] data, ErrorType type)
    {
        var req = requests.Take(callbackId);

        if (req == null)
        {
            // @TODO: Send general failure
            return;
        }

        var args = DataDeserializer.ListParser(dataType, Instance.Warehouse)
                                                as object[];

        var errorCode = (ushort)args[0];
        var errorMsg = (string)args[1];

        req.TriggerError(new AsyncException(type, errorCode, errorMsg));
    }

    void IIPReplyProgress(uint callbackId, ParsedTDU dataType, byte[] data)
    {
        var req = requests[callbackId];

        if (req == null)
        {
            // @TODO: Send general failure
            return;
        }

        var args = DataDeserializer.ListParser(dataType, Instance.Warehouse)
                                                as object[];

        var current = (uint)args[0];
        var total = (uint)args[1];

        req.TriggerProgress(ProgressType.Execution, current, total);
    }

    void IIPReplyWarning(uint callbackId, ParsedTDU dataType, byte[] data)
    {
        var req = requests[callbackId];

        if (req == null)
        {
            // @TODO: Send general failure
            return;
        }

        var args = DataDeserializer.ListParser(dataType, Instance.Warehouse)
                                                as object[];

        var level = (byte)args[0];
        var message = (string)args[1];

        req.TriggerWarning(level, message);
    }



    void IIPReplyChunk(uint callbackId, ParsedTDU dataType)
    {
        var req = requests[callbackId];

        if (req == null)
            return;

        var (_, parsed) = Codec.ParseAsync(dataType, this, null);

        if (parsed is AsyncReply reply)
            reply.Then(result => req.TriggerChunk(result));
        else
            req.TriggerChunk(parsed);
    }

    void IIPNotificationResourceReassigned(ParsedTDU dataType)
    {
        // uint resourceId, uint newResourceId
    }

    void IIPNotificationResourceMoved(ParsedTDU dataType, byte[] data) { }

    void IIPNotificationSystemFailure(ParsedTDU dataType, byte[] data) { }

    void IIPNotificationResourceDestroyed(ParsedTDU dataType, byte[] data)
    {
        var (size, rt) = Codec.ParseSync(dataType, Instance.Warehouse);

        var resourceId = Convert.ToUInt32(rt);

        if (attachedResources.Contains(resourceId))
        {
            DistributedResource r;

            if (attachedResources[resourceId].TryGetTarget(out r))
            {
                // remove from attached to avoid sending unnecessary detach request when Destroy() is called
                attachedResources.Remove(resourceId);
                r.Destroy();
            }
            else
            {
                attachedResources.Remove(resourceId);
            }


        }
        else if (neededResources.Contains(resourceId))
        {
            // @TODO: handle this mess
            neededResources.Remove(resourceId);
        }

    }

    void IIPNotificationPropertyModified(ParsedTDU dataType)
    {
        // resourceId, index, value
        var (valueOffset, valueSize, args) =
            DataDeserializer.LimitedCountListParser(dataType.Data, dataType.Offset, dataType.ContentLength, Instance.Warehouse, 2);

        var rid = Convert.ToUInt32(args[0]);
        var index = (byte)args[1];

        Fetch(rid, null).Then(r =>
        {
            var pt = r.Instance.Template.GetPropertyTemplateByIndex(index);
            if (pt == null)
                return;


            var (_, parsed) = Codec.ParseAsync(dataType.Data, valueOffset, this, null);

            if (parsed is AsyncReply)
            {
                var item = new AsyncReply<DistributedResourceQueueItem>();
                queue.Add(item);

                (parsed as AsyncReply).Then((result) =>
                {
                    item.Trigger(new DistributedResourceQueueItem((DistributedResource)r,
                                                    DistributedResourceQueueItem.DistributedResourceQueueItemType.Propery,
                                                    result, index));
                });
            }
            else
            {
                queue.Add(new AsyncReply<DistributedResourceQueueItem>(new DistributedResourceQueueItem((DistributedResource)r,
                                                DistributedResourceQueueItem.DistributedResourceQueueItemType.Propery,
                                                parsed, index)));

                //item.Trigger(new DistributedResourceQueueItem((DistributedResource)r,
                //                                DistributedResourceQueueItem.DistributedResourceQueueItemType.Propery,
                //                                parsed, index));
            }
        });
    }


    void IIPNotificationEventOccurred(ParsedTDU dataType, byte[] data)
    {
        // resourceId, index, value
        var (valueOffset, valueSize, args) =
            DataDeserializer.LimitedCountListParser(data, dataType.Offset,
                                                    dataType.ContentLength, Instance.Warehouse, 2);

        var resourceId = Convert.ToUInt32(args[0]);
        var index = (byte)args[1];

        Fetch(resourceId, null).Then(r =>
        {
            var et = r.Instance.Template.GetEventTemplateByIndex(index);

            if (et == null) // this should never happen
                return;

            // push to the queue to guarantee serialization
            var item = new AsyncReply<DistributedResourceQueueItem>();
            queue.Add(item);


            var (_, parsed) = Codec.ParseAsync(data, valueOffset, this, null);

            if (parsed is AsyncReply)
            {
                (parsed as AsyncReply).Then((result) =>
                {
                    item.Trigger(new DistributedResourceQueueItem((DistributedResource)r,
                                 DistributedResourceQueueItem.DistributedResourceQueueItemType.Event, result, index));
                });
            }
            else
            {
                item.Trigger(new DistributedResourceQueueItem((DistributedResource)r,
                              DistributedResourceQueueItem.DistributedResourceQueueItemType.Event, parsed, index));
            }
        });

    }

    void IIPEventRenamed(uint resourceId, string name)
    {
        Fetch(resourceId, null).Then(resource =>
        {
            resource.Instance.Variables["name"] = name;
        });
    }

    void IIPRequestAttachResource(uint callback, ParsedTDU dataType, byte[] data)
    {

        var (_, value) = Codec.ParseSync(dataType, Instance.Warehouse);

        var resourceId = Convert.ToUInt32(value);

        Instance.Warehouse.GetById(resourceId).Then((res) =>
        {
            if (res != null)
            {
                if (res.Instance.Applicable(session, ActionType.Attach, null) == Ruling.Denied)
                {
                    SendError(ErrorType.Management, callback, 6);
                    return;
                }

                var r = res as IResource;

                // unsubscribe
                Unsubscribe(r);

                // reply ok
                SendReply(IIPPacketReply.Completed, callback,
                    r.Instance.Template.ClassId,
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
                Global.Log("DistributedConnection", LogType.Debug, "Not found " + resourceId);
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ResourceNotFound);
            }
        });
    }

    void IIPRequestReattachResource(uint callback, ParsedTDU dataType, byte[] data)
    {
        // resourceId, index, value
        var (valueOffset, valueSize, args) =
            DataDeserializer.LimitedCountListParser(data, dataType.Offset,
                                                    dataType.ContentLength, Instance.Warehouse, 2);

        var resourceId = Convert.ToUInt32(args[0]);

        var age = (ulong)args[1];

        Instance.Warehouse.GetById(resourceId).Then((res) =>
        {
            if (res != null)
            {
                if (res.Instance.Applicable(session, ActionType.Attach, null) == Ruling.Denied)
                {
                    SendError(ErrorType.Management, callback, 6);
                    return;
                }

                var r = res as IResource;

                // unsubscribe
                Unsubscribe(r);


                // reply ok
                SendReply(IIPPacketReply.Completed, callback,
                    r.Instance.Template.ClassId,
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
                Global.Log("DistributedConnection", LogType.Debug, "Not found " + resourceId);
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ResourceNotFound);
            }
        });
    }

    void IIPRequestDetachResource(uint callback, ParsedTDU dataType, byte[] data)
    {

        var (_, value) = Codec.ParseSync(dataType, Instance.Warehouse);

        var resourceId = Convert.ToUInt32(value);

        Instance.Warehouse.GetById(resourceId).Then((res) =>
        {
            if (res != null)
            {

                // unsubscribe
                Unsubscribe(res);
                // remove from cache
                cache.Remove(res);

                // remove from attached resources
                //attachedResources.Remove(res);

                // reply ok
                SendReply(IIPPacketReply.Completed, callback);
            }
            else
            {
                // reply failed
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ResourceNotFound);
            }
        });
    }

    void IIPRequestCreateResource(uint callback, ParsedTDU dataType, byte[] data)
    {
        var (_, parsed) = Codec.ParseAsync(dataType, this, null);

        var args = (object[])parsed;

        var path = (string)args[0];

        TypeTemplate type = null;

        if (args[1] is UUID)
            type = Instance.Warehouse.GetTemplateByClassId((UUID)args[1]);
        else if (args[1] is string)
            type = Instance.Warehouse.GetTemplateByClassName((string)args[1]);

        if (type == null)
        {
            SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ClassNotFound);
            return;
        }

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
                 if (store.Instance.Applicable(session, ActionType.CreateResource, null) != Ruling.Allowed)
                 {
                     SendError(ErrorType.Management, callback, (ushort)ExceptionCode.CreateDenied);
                     return;
                 }

                 Instance.Warehouse.New(type.DefinedType, path, null, attrs, props).Then(resource =>
                 {
                     SendReply(IIPPacketReply.Completed, callback, resource.Instance.Id);

                 }).Error(e =>
                 {
                     SendError(e.Type, callback, (ushort)e.Code, e.Message);
                 });

             }).Error(e =>
             {
                 SendError(e.Type, callback, (ushort)e.Code, e.Message);
             });
    }


    void IIPRequestDeleteResource(uint callback, ParsedTDU dataType, byte[] data)
    {

        var (_, value) = Codec.ParseSync(dataType, Instance.Warehouse);

        var resourceId = Convert.ToUInt32(value);

        Instance.Warehouse.GetById(resourceId).Then(r =>
        {
            if (r == null)
            {
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ResourceNotFound);
                return;
            }

            if (r.Instance.Store.Instance.Applicable(session, ActionType.Delete, null) != Ruling.Allowed)
            {
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.DeleteDenied);
                return;
            }

            if (Instance.Warehouse.Remove(r))
                SendReply(IIPPacketReply.Completed, callback);

            else
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.DeleteFailed);
        });
    }

    void IIPRequestMoveResource(uint callback, ParsedTDU dataType, byte[] data)
    {

        var (offset, length, args) = DataDeserializer.LimitedCountListParser(data, dataType.Offset,
                                                                     dataType.ContentLength, Instance.Warehouse);


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

            if (resource.Instance.Applicable(this.session, ActionType.Rename, null) != Ruling.Allowed)
            {
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.RenameDenied);
                return;
            }


            resource.Instance.Name = name;
            SendReply(IIPPacketReply.Completed, callback);
        });
    }





    void IIPRequestToken(uint callback, ParsedTDU dataType, byte[] data)
    {
        // @TODO: To be implemented
    }

    void IIPRequestLinkTemplates(uint callback, ParsedTDU dataType, byte[] data)
    {
        var (_, value) = Codec.ParseSync(dataType, Instance.Warehouse);

        var resourceLink = (string)value;

        Action<IResource> queryCallback = (r) =>
        {
            if (r == null)
            {
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ResourceNotFound);
                return;
            }

            if (r.Instance.Applicable(session, ActionType.ViewTemplate, null) == Ruling.Denied)
            {
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.NotAllowed);
                return;
            }

            var templates = TypeTemplate.GetDependencies(r.Instance.Template, Instance.Warehouse);

            // Send
            SendReply(IIPPacketReply.Completed, callback, templates.Select(x => x.Content).ToArray());

        };

        if (Server?.EntryPoint != null)
            Server.EntryPoint.Query(resourceLink, this).Then(queryCallback);
        else
            Instance.Warehouse.Query(resourceLink).Then(queryCallback);
    }

    void IIPRequestTemplateFromClassName(uint callback, ParsedTDU dataType, byte[] data)
    {
        var (_, value) = Codec.ParseSync(dataType, Instance.Warehouse);

        var className = (string)value;

        var t = Instance.Warehouse.GetTemplateByClassName(className);

        if (t != null)
        {
            SendReply(IIPPacketReply.Completed, callback, t.Content);
        }
        else
        {
            // reply failed
            SendError(ErrorType.Management, callback, (ushort)ExceptionCode.TemplateNotFound);
        }
    }

    void IIPRequestTemplateFromClassId(uint callback, ParsedTDU dataType, byte[] data)
    {

        var (_, value) = Codec.ParseSync(dataType, Instance.Warehouse);

        var classId = (UUID)value;

        var t = Instance.Warehouse.GetTemplateByClassId(classId);

        if (t != null)
        {
            SendReply(IIPPacketReply.Completed, callback, t.Content);
        }
        else
        {
            // reply failed
            SendError(ErrorType.Management, callback, (ushort)ExceptionCode.TemplateNotFound);
        }
    }



    void IIPRequestTemplateFromResourceId(uint callback, ParsedTDU dataType, byte[] data)
    {

        var (_, value) = Codec.ParseSync(dataType, Instance.Warehouse);

        var resourceId = Convert.ToUInt32(value);

        Instance.Warehouse.GetById(resourceId).Then((r) =>
        {
            if (r != null)
            {
                SendReply(IIPPacketReply.Completed, callback, r.Instance.Template.Content);
            }
            else
            {
                // reply failed
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ResourceNotFound);
            }
        });
    }



    void IIPRequestGetResourceIdByLink(uint callback, ParsedTDU dataType, byte[] data)
    {
        var (_, parsed) = Codec.ParseSync(dataType, Instance.Warehouse);
        var resourceLink = (string)parsed;

        Action<IResource> queryCallback = (r) =>
        {
            if (r == null)
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ResourceNotFound);
            else
            {
                if (r.Instance.Applicable(session, ActionType.Attach, null) == Ruling.Denied)
                {
                    SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ResourceNotFound);
                    return;
                }

                SendReply(IIPPacketReply.Completed, callback, r);
            }
        };

        if (Server?.EntryPoint != null)
            Server.EntryPoint.Query(resourceLink, this).Then(queryCallback);
        else
            Instance.Warehouse.Query(resourceLink).Then(queryCallback);

    }

    void IIPRequestQueryResources(uint callback, ParsedTDU dataType, byte[] data)
    {
        var (_, parsed) = Codec.ParseSync(dataType, Instance.Warehouse);

        var resourceLink = (string)parsed;

        Action<IResource> queryCallback = (r) =>
        {
            if (r == null)
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ResourceNotFound);
            else
            {
                if (r.Instance.Applicable(session, ActionType.Attach, null) == Ruling.Denied)
                {
                    SendError(ErrorType.Management, callback, (ushort)ExceptionCode.NotAllowed);
                    return;
                }

                r.Instance.Children<IResource>().Then(children =>
                {
                    var list = children.Where(x => x.Instance.Applicable(session, ActionType.Attach, null) != Ruling.Denied).ToArray();
                    SendReply(IIPPacketReply.Completed, callback, list);
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

    void IIPRequestResourceAttribute(uint callback, uint resourceId)
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


    void IIPRequestProcedureCall(uint callback, ParsedTDU dataType, byte[] data)
    {
        var (offset, length, args) = DataDeserializer.LimitedCountListParser(data, dataType.Offset,
                                                                     dataType.ContentLength, Instance.Warehouse, 1);

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

        var (_, parsed) = Codec.ParseAsync(data, offset, this, null);

        if (parsed is AsyncReply reply)
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

                InvokeFunction(call.Value.Template, callback, results, IIPPacketRequest.ProcedureCall, call.Value.Delegate.Target);

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
            InvokeFunction(call.Value.Template, callback, parsed, IIPPacketRequest.ProcedureCall, call.Value.Delegate.Target);
        }
    }

    void IIPRequestStaticCall(uint callback, ParsedTDU dataType, byte[] data)
    {
        var (offset, length, args) = DataDeserializer.LimitedCountListParser(data, dataType.Offset,
                                                                     dataType.ContentLength, Instance.Warehouse, 2);

        var classId = new UUID((byte[])args[0]);
        var index = (byte)args[1];


        var template = Instance.Warehouse.GetTemplateByClassId(classId);


        if (template == null)
        {
            SendError(ErrorType.Management, callback, (ushort)ExceptionCode.TemplateNotFound);
            return;
        }

        var ft = template.GetFunctionTemplateByIndex(index);

        if (ft == null)
        {
            // no function at this index
            SendError(ErrorType.Management, callback, (ushort)ExceptionCode.MethodNotFound);
            return;
        }

        var fi = ft.MethodInfo;

        if (fi == null)
        {
            // ft found, fi not found, this should never happen
            SendError(ErrorType.Management, callback, (ushort)ExceptionCode.MethodNotFound);
            return;
        }

        var (_, parsed) = Codec.ParseAsync(data, offset, this, null);

        if (parsed is AsyncReply reply)
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

                InvokeFunction(ft, callback, results, IIPPacketRequest.StaticCall, null);

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


            InvokeFunction(ft, callback, parsed, IIPPacketRequest.StaticCall, null);
        }
    }

    void IIPRequestInvokeFunction(uint callback, ParsedTDU dataType, byte[] data)
    {
        var (offset, length, args) = DataDeserializer.LimitedCountListParser(data, dataType.Offset,
                                                                             dataType.ContentLength, Instance.Warehouse, 2);

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

            var ft = r.Instance.Template.GetFunctionTemplateByIndex(index);

            if (ft == null)
            {
                // no function at this index
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.MethodNotFound);
                return;
            }

            var (_, parsed) = Codec.ParseAsync(data, offset, this, null);

            if (parsed is AsyncReply)
            {
                (parsed as AsyncReply).Then(result =>
                {
                    // var arguments = result;

                    // un hold the socket to send data immediately
                    this.Socket.Unhold();

                    if (r is DistributedResource)
                    {
                        var rt = (r as DistributedResource)._Invoke(index, result);
                        if (rt != null)
                        {
                            rt.Then(res =>
                            {
                                SendReply(IIPPacketReply.Completed, callback, res);
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
                        if (r.Instance.Applicable(session, ActionType.Execute, ft) == Ruling.Denied)
                        {
                            SendError(ErrorType.Management, callback,
                                (ushort)ExceptionCode.InvokeDenied);
                            return;
                        }

                        InvokeFunction(ft, callback, result, IIPPacketRequest.InvokeFunction, r);
                    }
                });
            }
            else
            {
                //var arguments = (Map<byte, object>)parsed;

                // un hold the socket to send data immediately
                this.Socket.Unhold();

                if (r is DistributedResource)
                {
                    var rt = (r as DistributedResource)._Invoke(index, parsed);
                    if (rt != null)
                    {
                        rt.Then(res =>
                        {
                            SendReply(IIPPacketReply.Completed, callback, res);
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
                    if (r.Instance.Applicable(session, ActionType.Execute, ft) == Ruling.Denied)
                    {
                        SendError(ErrorType.Management, callback,
                            (ushort)ExceptionCode.InvokeDenied);
                        return;
                    }

                    InvokeFunction(ft, callback, parsed, IIPPacketRequest.InvokeFunction, r);
                }

            }
        });
    }



    void InvokeFunction(FunctionTemplate ft, uint callback, object arguments, IIPPacketRequest actionType, object target = null)
    {

        // cast arguments
        ParameterInfo[] pis = ft.MethodInfo.GetParameters();

        object[] args = new object[pis.Length];

        InvocationContext context = null;

        if (pis.Length > 0)
        {
            if (pis.Last().ParameterType == typeof(DistributedConnection))
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

        if (rt is System.Collections.IEnumerable && !(rt is Array || rt is Map<string, object> || rt is string))
        {
            var enu = rt as System.Collections.IEnumerable;

            try
            {
                foreach (var v in enu)
                    SendChunk(callback, v);

                SendReply(IIPPacketReply.Completed, callback);

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

                SendReply(IIPPacketReply.Completed, callback, res);
            });

        }
        else if (rt is AsyncReply)
        {
            (rt as AsyncReply).Then(res =>
            {
                if (context != null)
                    context.Ended = true;

                SendReply(IIPPacketReply.Completed, callback, res);

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

            SendReply(IIPPacketReply.Completed, callback, rt);
        }
    }

    void IIPRequestSubscribe(uint callback, ParsedTDU dataType, byte[] data)
    {

        var (offset, length, args) = DataDeserializer.LimitedCountListParser(data, dataType.Offset,
                                                                     dataType.ContentLength, Instance.Warehouse);

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

            var et = r.Instance.Template.GetEventTemplateByIndex(index);

            if (et != null)
            {
                // et not found
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.MethodNotFound);
                return;
            }

            if (r is DistributedResource)
            {
                (r as DistributedResource).Subscribe(et).Then(x =>
               {
                   SendReply(IIPPacketReply.Completed, callback);
               }).Error(x => SendError(ErrorType.Exception, callback, (ushort)ExceptionCode.GeneralFailure));
            }
            else
            {
                lock (subscriptionsLock)
                {
                    if (!subscriptions.ContainsKey(r))
                    {
                        SendError(ErrorType.Management, callback, (ushort)ExceptionCode.NotAttached);
                        return;
                    }

                    if (subscriptions[r].Contains(index))
                    {
                        SendError(ErrorType.Management, callback, (ushort)ExceptionCode.AlreadyListened);
                        return;
                    }

                    subscriptions[r].Add(index);

                    SendReply(IIPPacketReply.Completed, callback);
                }
            }
        });

    }

    void IIPRequestUnsubscribe(uint callback, ParsedTDU dataType, byte[] data)
    {

        var (offset, length, args) = DataDeserializer.LimitedCountListParser(data, dataType.Offset,
                                                                     dataType.ContentLength, Instance.Warehouse);

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

            var et = r.Instance.Template.GetEventTemplateByIndex(index);

            if (et == null)
            {
                // pt not found
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.MethodNotFound);
                return;
            }

            if (r is DistributedResource)
            {
                (r as DistributedResource).Unsubscribe(et).Then(x =>
                {
                    SendReply(IIPPacketReply.Completed, callback);
                }).Error(x => SendError(ErrorType.Exception, callback, (ushort)ExceptionCode.GeneralFailure));
            }
            else
            {
                lock (subscriptionsLock)
                {
                    if (!subscriptions.ContainsKey(r))
                    {
                        SendError(ErrorType.Management, callback, (ushort)ExceptionCode.NotAttached);
                        return;
                    }

                    if (!subscriptions[r].Contains(index))
                    {
                        SendError(ErrorType.Management, callback, (ushort)ExceptionCode.AlreadyUnsubscribed);
                        return;
                    }

                    subscriptions[r].Remove(index);

                    SendReply(IIPPacketReply.Completed, callback);
                }
            }
        });
    }




    void IIPRequestSetProperty(uint callback, ParsedTDU dataType, byte[] data)
    {

        var (offset, length, args) = DataDeserializer.LimitedCountListParser(data, dataType.Offset,
                                                                     dataType.ContentLength, Instance.Warehouse, 2);

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

            var pt = r.Instance.Template.GetPropertyTemplateByIndex(index);

            if (pt != null)
            {
                // property not found
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.PropertyNotFound);
                return;
            }


            if (r is IDynamicResource)
            {
                var (_, parsed) = Codec.ParseAsync(data, offset, this, null);
                if (parsed is AsyncReply)
                {
                    (parsed as AsyncReply).Then((value) =>
                    {
                        // propagation
                        (r as IDynamicResource).SetResourcePropertyAsync(index, value).Then((x) =>
                        {
                            SendReply(IIPPacketReply.Completed, callback);
                        }).Error(x =>
                        {
                            SendError(x.Type, callback, (ushort)x.Code, x.Message);
                        });
                    });
                }
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

                if (r.Instance.Applicable(session, ActionType.SetProperty, pt, this) == Ruling.Denied)
                {
                    SendError(ErrorType.Exception, callback, (ushort)ExceptionCode.SetPropertyDenied);
                    return;
                }

                if (!pi.CanWrite)
                {
                    SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ReadOnlyProperty);
                    return;
                }

                var (_, parsed) = Codec.ParseAsync(data, offset, this, null);

                if (parsed is AsyncReply)
                {
                    (parsed as AsyncReply).Then((value) =>
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
                            SendReply(IIPPacketReply.Completed, callback);
                        }
                        catch (Exception ex)
                        {
                            SendError(ErrorType.Exception, callback, 0, ex.Message);
                        }
                    });
                }
                else
                {
                    if (pi.PropertyType.IsGenericType && pi.PropertyType.GetGenericTypeDefinition()
                            == typeof(PropertyContext<>))
                    {
                        parsed = Activator.CreateInstance(pi.PropertyType, this, parsed);
                        //value = new DistributedPropertyContext(this, value);
                    }
                    else
                    {
                        // cast new value type to property type
                        parsed = RuntimeCaster.Cast(parsed, pi.PropertyType);
                    }

                    try
                    {
                        pi.SetValue(r, parsed);
                        SendReply(IIPPacketReply.Completed, callback);
                    }
                    catch (Exception ex)
                    {
                        SendError(ErrorType.Exception, callback, 0, ex.Message);
                    }
                }
            }
        });
    }


    /// <summary>
    /// Get the ResourceTemplate for a given class Id. 
    /// </summary>
    /// <param name="classId">Class GUID.</param>
    /// <returns>ResourceTemplate.</returns>
    public AsyncReply<TypeTemplate> GetTemplate(UUID classId)
    {
        if (templates.ContainsKey(classId))
            return new AsyncReply<TypeTemplate>(templates[classId]);
        else if (templateRequests.ContainsKey(classId))
            return templateRequests[classId];

        var reply = new AsyncReply<TypeTemplate>();
        templateRequests.Add(classId, reply);

        SendRequest(IIPPacketRequest.TemplateFromClassId, classId)
                    .Then((result) =>
                    {
                        var tt = TypeTemplate.Parse((byte[])result);
                        templateRequests.Remove(classId);
                        templates.Add(tt.ClassId, tt);
                        Instance.Warehouse.PutTemplate(tt);
                        reply.Trigger(tt);

                    }).Error((ex) =>
                    {
                        reply.TriggerError(ex);
                    });

        return reply;
    }


    public AsyncReply<TypeTemplate> GetTemplateByClassName(string className)
    {
        var template = templates.Values.FirstOrDefault(x => x.ClassName == className);
        if (template != null)
            return new AsyncReply<TypeTemplate>(template);

        if (templateByNameRequests.ContainsKey(className))
            return templateByNameRequests[className];

        var reply = new AsyncReply<TypeTemplate>();
        templateByNameRequests.Add(className, reply);


        SendRequest(IIPPacketRequest.TemplateFromClassName, className)
                    .Then((result) =>
                    {
                        var tt = TypeTemplate.Parse((byte[])result);

                        templateByNameRequests.Remove(className);
                        templates.Add(tt.ClassId, tt);
                        Instance.Warehouse.PutTemplate(tt);
                        reply.Trigger(tt);
                    }).Error((ex) =>
                    {
                        reply.TriggerError(ex);
                    });

        return reply;
    }

    // IStore interface 
    /// <summary>
    /// Get a resource by its path.
    /// </summary>
    /// <param name="path">Path to the resource.</param>
    /// <returns>Resource</returns>
    public AsyncReply<IResource> Get(string path)
    {

        var rt = new AsyncReply<IResource>();

        var req = SendRequest(IIPPacketRequest.GetResourceIdByLink, path);


        req.Then(result =>
            {
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


    public AsyncReply<TypeTemplate[]> GetLinkTemplates(string link)
    {
        var reply = new AsyncReply<TypeTemplate[]>();


        SendRequest(IIPPacketRequest.LinkTemplates, link)
        .Then((result) =>
        {

            var templates = new List<TypeTemplate>();

            foreach (var template in (byte[][])result)
            {
                templates.Add(TypeTemplate.Parse(template));
            }

            reply.Trigger(templates.ToArray());

        }).Error((ex) =>
        {
            reply.TriggerError(ex);
        });

        return reply;
    }

    /// <summary>
    /// Fetch a resource from the other end
    /// </summary>
    /// <param name="classId">Class GUID</param>
    /// <param name="id">Resource Id</param>Guid classId
    /// <returns>DistributedResource</returns>
    public AsyncReply<DistributedResource> Fetch(uint id, uint[] requestSequence)
    {

        DistributedResource resource = null;

        attachedResources[id]?.TryGetTarget(out resource);

        if (resource != null)
            return new AsyncReply<DistributedResource>(resource);

        resource = neededResources[id];

        var requestInfo = resourceRequests[id];

        if (requestInfo != null)
        {
            if (resource != null && (requestSequence?.Contains(id) ?? false))
            {
                // dead lock avoidance for loop reference.
                return new AsyncReply<DistributedResource>(resource);
            }
            else if (resource != null && requestInfo.RequestSequence.Contains(id))
            {
                // dead lock avoidance for dependent reference.
                return new AsyncReply<DistributedResource>(resource);
            }
            else
            {
                return requestInfo.Reply;
            }
        }
        else if (resource != null && !resource.DistributedResourceSuspended)
        {
            // @REVIEW: this should never happen
            Global.Log("DCON", LogType.Error, "Resource not moved to attached.");
            return new AsyncReply<DistributedResource>(resource);

        }

        var newSequence = requestSequence != null ? requestSequence.Concat(new uint[] { id }).ToArray() : new uint[] { id };

        var reply = new AsyncReply<DistributedResource>();
        resourceRequests.Add(id, new DistributedResourceAttachRequestInfo(reply, newSequence));

        SendRequest(IIPPacketRequest.AttachResource, id)
                    .Then((result) =>
                    {
                        if (result == null)
                        {
                            reply.TriggerError(new AsyncException(ErrorType.Management,
                                (ushort)ExceptionCode.ResourceNotFound, "Null response"));
                            return;
                        }

                        // ClassId, Age, Link, Hops, PropertyValue[]
                        var args = (object[])result;
                        var classId = (UUID)args[0];
                        var age = Convert.ToUInt64(args[1]);
                        var link = (string)args[2];
                        var hops = (byte)args[3];
                        var pvData = (byte[])args[4];


                        DistributedResource dr;
                        TypeTemplate template = null;

                        if (resource == null)
                        {
                            template = Instance.Warehouse.GetTemplateByClassId(classId, TemplateType.Resource);
                            if (template?.DefinedType != null && template.IsWrapper)
                                dr = Activator.CreateInstance(template.DefinedType, this, id, Convert.ToUInt64(args[1]), (string)args[2]) as DistributedResource;
                            else
                                dr = new DistributedResource(this, id, Convert.ToUInt64(args[1]), (string)args[2]);
                        }
                        else
                        {
                            dr = resource;
                            template = resource.Instance.Template;
                        }


                        var initResource = (DistributedResource ok) =>
                        {
                            var parsedReply = DataDeserializer.PropertyValueArrayParserAsync(pvData, 0, (uint)pvData.Length, this, newSequence);// Codec.proper (content, 0, this, newSequence, transmissionType);


                            parsedReply.Then(results =>
                            {
                                var pvs = results as PropertyValue[];

                                //var pvs = new List<PropertyValue>();

                                //for (var i = 0; i < ar.Length; i += 3)
                                //    pvs.Add(new PropertyValue(ar[i + 2], Convert.ToUInt64(ar[i]), (DateTime)ar[i + 1]));

                                dr._Attach(pvs);
                                resourceRequests.Remove(id);
                                // move from needed to attached.
                                neededResources.Remove(id);
                                attachedResources[id] = new WeakReference<DistributedResource>(dr);
                                reply.Trigger(dr);
                            }).Error(ex => reply.TriggerError(ex));


                        };

                        if (template == null)
                        {
                            GetTemplate(classId).Then((tmp) =>
                            {
                                // ClassId, ResourceAge, ResourceLink, Content
                                if (resource == null)
                                {
                                    dr.ResourceTemplate = tmp;

                                    Instance.Warehouse.Put(this.Instance.Link + "/" + id.ToString(), dr)
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
                                Instance.Warehouse.Put(this.Instance.Link + "/" + id.ToString(), dr)
                                    .Then(initResource).Error((ex) => reply.TriggerError(ex));
                            }
                            else
                            {
                                initResource(resource);
                            }

                        }

                    }).Error((ex) =>
                    {
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

        SendRequest(IIPPacketRequest.Query, path)
                    .Then(result =>
                    {
                        reply.Trigger((IResource[])result);
                    }).Error(ex => reply.TriggerError(ex));

        return reply;
    }


    /// <summary>
    /// Create a new resource.
    /// </summary>
    /// <param name="path">Resource path.</param>
    /// <param name="type">Type template.</param>
    /// <param name="properties">Values for the resource properties.</param>
    /// <param name="attributes">Resource attributes.</param>
    /// <returns>New resource instance</returns>
    public AsyncReply<DistributedResource> Create(string path, TypeTemplate type, Map<string, object> properties, Map<string, object> attributes)
    {
        var reply = new AsyncReply<DistributedResource>();

        SendRequest(IIPPacketRequest.CreateResource, path, type.ClassId, type.CastProperties(properties), attributes)
            .Then(r => reply.Trigger((DistributedResource)r))
            .Error(e => reply.TriggerError(e))
            .Warning((l, m) => reply.TriggerWarning(l, m));

        return reply;
    }

    private void Subscribe(IResource resource)
    {
        lock (subscriptionsLock)
        {
            resource.Instance.EventOccurred += Instance_EventOccurred;
            resource.Instance.CustomEventOccurred += Instance_CustomEventOccurred;
            resource.Instance.PropertyModified += Instance_PropertyModified;
            resource.Instance.Destroyed += Instance_ResourceDestroyed;

            subscriptions.Add(resource, new List<byte>());
        }
    }

    private void Unsubscribe(IResource resource)
    {
        lock (subscriptionsLock)
        {
            // do something with the list...
            resource.Instance.EventOccurred -= Instance_EventOccurred;
            resource.Instance.CustomEventOccurred -= Instance_CustomEventOccurred;
            resource.Instance.PropertyModified -= Instance_PropertyModified;
            resource.Instance.Destroyed -= Instance_ResourceDestroyed;

            subscriptions.Remove(resource);
        }

    }

    private void UnsubscribeAll()
    {
        lock (subscriptionsLock)
        {
            foreach (var resource in subscriptions.Keys)
            {
                resource.Instance.EventOccurred -= Instance_EventOccurred;
                resource.Instance.CustomEventOccurred -= Instance_CustomEventOccurred;
                resource.Instance.PropertyModified -= Instance_PropertyModified;
                resource.Instance.Destroyed -= Instance_ResourceDestroyed;
            }

            subscriptions.Clear();
        }
    }

    private void Instance_ResourceDestroyed(IResource resource)
    {

        Unsubscribe(resource);

        // compose the packet
        SendNotification(IIPPacketNotification.ResourceDestroyed, resource.Instance.Id);
    }

    private void Instance_PropertyModified(PropertyModificationInfo info)
    {
        SendNotification(IIPPacketNotification.PropertyModified,
                         info.Resource.Instance.Id,
                         info.PropertyTemplate.Index,
                         info.Value);
    }

    private void Instance_CustomEventOccurred(CustomEventOccurredInfo info)
    {
        if (info.EventTemplate.Subscribable)
        {
            lock (subscriptionsLock)
            {
                // check the client requested listen
                if (!subscriptions.ContainsKey(info.Resource))
                    return;

                if (!subscriptions[info.Resource].Contains(info.EventTemplate.Index))
                    return;
            }
        }

        if (!info.Receivers(this.session))
            return;

        if (info.Resource.Instance.Applicable(this.session, ActionType.ReceiveEvent, info.EventTemplate, info.Issuer) == Ruling.Denied)
            return;


        // compose the packet
        SendNotification(IIPPacketNotification.EventOccurred,
                          info.Resource.Instance.Id,
                          info.EventTemplate.Index,
                          info.Value);
    }

    private void Instance_EventOccurred(EventOccurredInfo info)
    {
        if (info.EventTemplate.Subscribable)
        {
            lock (subscriptionsLock)
            {
                // check the client requested listen
                if (!subscriptions.ContainsKey(info.Resource))
                    return;

                if (!subscriptions[info.Resource].Contains(info.EventTemplate.Index))
                    return;
            }
        }

        if (info.Resource.Instance.Applicable(this.session, ActionType.ReceiveEvent, info.EventTemplate, null) == Ruling.Denied)
            return;

        // compose the packet
        SendNotification(IIPPacketNotification.EventOccurred,
            info.Resource.Instance.Id,
            info.EventTemplate.Index,
            info.Value);
    }



    void IIPRequestKeepAlive(uint callback, ParsedTDU dataType, byte[] data)
    {

        var (offset, length, args) = DataDeserializer.LimitedCountListParser(data, dataType.Offset,
                                                                     dataType.ContentLength, Instance.Warehouse);

        var peerTime = (DateTime)args[0];
        var interval = Convert.ToUInt32(args[1]);

        uint jitter = 0;

        var now = DateTime.UtcNow;

        if (lastKeepAliveReceived != null)
        {
            var diff = (uint)(now - (DateTime)lastKeepAliveReceived).TotalMilliseconds;
            jitter = (uint)Math.Abs((int)diff - (int)interval);
        }

        SendReply(IIPPacketReply.Completed, callback, now, jitter);

        lastKeepAliveReceived = now;
    }
}
