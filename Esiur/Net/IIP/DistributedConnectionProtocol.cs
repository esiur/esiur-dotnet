/*
 
Copyright (c) 2017 Ahmed Kh. Zamil

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

using Esiur.Data;
using Esiur.Core;
using Esiur.Resource;
using Esiur.Resource.Template;
using Esiur.Security.Authority;
using Esiur.Security.Permissions;
using System;
using System.Collections.Generic;

using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using Esiur.Misc;
using Esiur.Net.Packets;

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

    // resources might get attched by the client
    internal KeyList<IResource, DateTime> cache = new();

    object subscriptionsLock = new object();

    AsyncQueue<DistributedResourceQueueItem> queue = new AsyncQueue<DistributedResourceQueueItem>();



    /// <summary>
    /// Send IIP request.
    /// </summary>
    /// <param name="action">Packet action.</param>
    /// <param name="args">Arguments to send.</param>
    /// <returns></returns>
    SendList SendRequest(IIPPacketAction action)
    {
        var reply = new AsyncReply<object[]>();
        var c = callbackCounter++; // avoid thread racing
        requests.Add(c, reply);

        return (SendList)SendParams(reply)
            .AddUInt8((byte)(0x40 | (byte)action))
            .AddUInt32(c);
    }

    /*
    internal IAsyncReply<object[]> SendRequest(IIPPacket.IIPPacketAction action, params object[] args)
    {
        var reply = new AsyncReply<object[]>();
        callbackCounter++;
        var bl = new BinaryList((byte)(0x40 | (byte)action), callbackCounter);
        bl.AddRange(args);
        Send(bl.ToArray());
        requests.Add(callbackCounter, reply);
        return reply;
    }
    */

    //uint maxcallerid = 0;

    internal SendList SendReply(IIPPacketAction action, uint callbackId)
    {
        return (SendList)SendParams().AddUInt8((byte)(0x80 | (byte)action)).AddUInt32(callbackId);
    }

    internal SendList SendEvent(IIPPacketEvent evt)
    {
        return (SendList)SendParams().AddUInt8((byte)(evt));
    }

    internal AsyncReply SendListenRequest(uint instanceId, byte index)
    {
        var reply = new AsyncReply<object>();
        var c = callbackCounter++;
        requests.Add(c, reply);

        SendParams().AddUInt8((byte)(0x40 | (byte)IIPPacketAction.Listen))
                    .AddUInt32(c)
                    .AddUInt32(instanceId)
                    .AddUInt8(index)
                    .Done();

        return reply;
    }

    internal AsyncReply SendUnlistenRequest(uint instanceId, byte index)
    {
        var reply = new AsyncReply<object>();
        var c = callbackCounter++;
        requests.Add(c, reply);

        SendParams().AddUInt8((byte)(0x40 | (byte)IIPPacketAction.Unlisten))
                    .AddUInt32(c)
                    .AddUInt32(instanceId)
                    .AddUInt8(index)
                    .Done();

        return reply;
    }


    public AsyncReply<object> StaticCall(UUID classId, byte index, Map<byte, object> parameters)
    {
        var pb = Codec.Compose(parameters, this);// Codec.ComposeVarArray(parameters, this, true);

        var reply = new AsyncReply<object>();
        var c = callbackCounter++;
        requests.Add(c, reply);


        SendParams().AddUInt8((byte)(0x40 | (byte)IIPPacketAction.StaticCall))
        .AddUInt32(c)
        .AddUUID(classId)
        .AddUInt8(index)
        .AddUInt8Array(pb)
        .Done();

        return reply;
    }

    public AsyncReply<object> Call(string procedureCall, params object[] parameters)
    {
        var args = new Map<byte, object>();
        for (byte i = 0; i < parameters.Length; i++)
            args.Add(i, parameters[i]);
        return Call(procedureCall, args);
    }

    public AsyncReply<object> Call(string procedureCall, Map<byte, object> parameters)
    {
        var pb = Codec.Compose(parameters, this);

        var reply = new AsyncReply<object>();
        var c = callbackCounter++;
        requests.Add(c, reply);

        var callName = DC.ToBytes(procedureCall);

        SendParams().AddUInt8((byte)(0x40 | (byte)IIPPacketAction.ProcedureCall))
        .AddUInt32(c)
        .AddUInt16((ushort)callName.Length)
        .AddUInt8Array(callName)
        .AddUInt8Array(pb)
        .Done();

        return reply;
    }

    internal AsyncReply<object> SendInvoke(uint instanceId, byte index, Map<byte, object> parameters)
    {
        var pb = Codec.Compose(parameters, this);// Codec.ComposeVarArray(parameters, this, true);

        var reply = new AsyncReply<object>();
        var c = callbackCounter++;
        requests.Add(c, reply);

        SendParams().AddUInt8((byte)(0x40 | (byte)IIPPacketAction.InvokeFunction))
                    .AddUInt32(c)
                    .AddUInt32(instanceId)
                    .AddUInt8(index)
                    .AddUInt8Array(pb)
                    .Done();
        return reply;
    }

    internal AsyncReply<object[]> SendSetProperty(uint instanceId, byte index, object value)
    {
        var cv = Codec.Compose(value, this);

        return SendRequest(IIPPacketAction.SetProperty)
                .AddUInt32(instanceId)
                .AddUInt8(index)
                .AddUInt8Array(cv)
                .Done();
    }

    internal AsyncReply<object[]> SendDetachRequest(uint instanceId)
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
                return SendRequest(IIPPacketAction.DetachResource)
                    .AddUInt32(instanceId)
                    .Done();

            return null; // no one is waiting for this
        }
        catch
        {
            return null;
        }
    }

    void SendError(ErrorType type, uint callbackId, ushort errorCode, string errorMessage = "")
    {
        var msg = DC.ToBytes(errorMessage);
        if (type == ErrorType.Management)
            SendParams()
                        .AddUInt8((byte)(0xC0 | (byte)IIPPacketReport.ManagementError))
                        .AddUInt32(callbackId)
                        .AddUInt16(errorCode)
                        .Done();
        else if (type == ErrorType.Exception)
            SendParams()
                        .AddUInt8((byte)(0xC0 | (byte)IIPPacketReport.ExecutionError))
                        .AddUInt32(callbackId)
                        .AddUInt16(errorCode)
                        .AddUInt16((ushort)msg.Length)
                        .AddUInt8Array(msg)
                        .Done();
    }

    internal void SendProgress(uint callbackId, int value, int max)
    {
        SendParams()
            .AddUInt8((byte)(0xC0 | (byte)IIPPacketReport.ProgressReport))
            .AddUInt32(callbackId)
            .AddInt32(value)
            .AddInt32(max)
            .Done();
        //SendParams(, callbackId, value, max);
    }

    internal void SendChunk(uint callbackId, object chunk)
    {
        var c = Codec.Compose(chunk, this);
        SendParams()
            .AddUInt8((byte)(0xC0 | (byte)IIPPacketReport.ChunkStream))
            .AddUInt32(callbackId)
            .AddUInt8Array(c)
            .Done();
    }

    void IIPReply(uint callbackId, params object[] results)
    {
        var req = requests.Take(callbackId);
        req?.Trigger(results);
    }

    void IIPReplyInvoke(uint callbackId, TransmissionType transmissionType, byte[] content)
    {
        var req = requests.Take(callbackId);

        var (_, parsed) = Codec.Parse(content, 0, this, null, transmissionType);
        parsed.Then((rt) =>
        {
            req?.Trigger(rt);
        });
    }

    void IIPReportError(uint callbackId, ErrorType errorType, ushort errorCode, string errorMessage)
    {
        var req = requests.Take(callbackId);
        req?.TriggerError(new AsyncException(errorType, errorCode, errorMessage));
    }

    void IIPReportProgress(uint callbackId, ProgressType type, int value, int max)
    {
        var req = requests[callbackId];
        req?.TriggerProgress(type, value, max);
    }

    void IIPReportChunk(uint callbackId, TransmissionType dataType, byte[] data)
    {
        if (requests.ContainsKey(callbackId))
        {
            var req = requests[callbackId];
            var (_, parsed) = Codec.Parse(data, dataType.Offset, this, null, dataType);
            parsed.Then((x) =>
            {
                req.TriggerChunk(x);
            });
        }
    }

    void IIPEventResourceReassigned(uint resourceId, uint newResourceId)
    {

    }

    void IIPEventResourceDestroyed(uint resourceId)
    {
        if (attachedResources.Contains(resourceId))
        {
            DistributedResource r;

            if (attachedResources[resourceId].TryGetTarget(out r))
            {
                // remove from attached to avoid sending unnecessary deattach request when Destroy() is called
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

    void IIPEventPropertyUpdated(uint resourceId, byte index, TransmissionType dataType, byte[] data)
    {

        Fetch(resourceId, null).Then(r =>
        {
            var item = new AsyncReply<DistributedResourceQueueItem>();
            queue.Add(item);

            var (_, parsed) = Codec.Parse(data, dataType.Offset, this, null, dataType);// 0, this);
            parsed.Then((arguments) =>
            {
                var pt = r.Instance.Template.GetPropertyTemplateByIndex(index);
                if (pt != null)
                {
                    item.Trigger(new DistributedResourceQueueItem((DistributedResource)r,
                                                    DistributedResourceQueueItem.DistributedResourceQueueItemType.Propery,
                                                    arguments, index));
                }
                else
                {    // ft found, fi not found, this should never happen
                    queue.Remove(item);
                }
            });

        });

        /*
        if (resources.Contains(resourceId))
        {
            // push to the queue to gaurantee serialization
            var reply = new AsyncReply<DistributedResourceQueueItem>();
            queue.Add(reply);

            var r = resources[resourceId];
            Codec.Parse(content, 0, this).Then((arguments) =>
            {
                if (!r.IsAttached)
                {
                    // property updated before the template is received
                    r.AddAfterAttachement(reply, 
                                            new DistributedResourceQueueItem((DistributedResource)r, 
                                                             DistributedResourceQueueItem.DistributedResourceQueueItemType.Propery, 
                                                             arguments, index));
                }
                else
                {
                    var pt = r.Instance.Template.GetPropertyTemplate(index);
                    if (pt != null)
                    {
                        reply.Trigger(new DistributedResourceQueueItem((DistributedResource)r, 
                                                        DistributedResourceQueueItem.DistributedResourceQueueItemType.Propery, 
                                                        arguments, index));
                    }
                    else
                    {    // ft found, fi not found, this should never happen
                        queue.Remove(reply);
                    }
                }
            });
        }
        */
    }


    void IIPEventEventOccurred(uint resourceId, byte index, TransmissionType dataType, byte[] data)
    {
        Fetch(resourceId, null).Then(r =>
        {
            // push to the queue to gaurantee serialization
            var item = new AsyncReply<DistributedResourceQueueItem>();
            queue.Add(item);

            var (_, parsed) = Codec.Parse(data, dataType.Offset, this, null, dataType);//, 0, this);
            parsed.Then((arguments) =>
            {
                var et = r.Instance.Template.GetEventTemplateByIndex(index);
                if (et != null)
                {
                    item.Trigger(new DistributedResourceQueueItem((DistributedResource)r,
                                  DistributedResourceQueueItem.DistributedResourceQueueItemType.Event, arguments, index));
                }
                else
                {    // ft found, fi not found, this should never happen
                    queue.Remove(item);
                }

            });
        });

        /*
        if (resources.Contains(resourceId))
        {
            // push to the queue to gaurantee serialization
            var reply = new AsyncReply<DistributedResourceQueueItem>();
            var r = resources[resourceId];

            queue.Add(reply);

            Codec.ParseVarArray(content, this).Then((arguments) =>
            {
                if (!r.IsAttached)
                {
                    // event occurred before the template is received
                    r.AddAfterAttachement(reply,
                                            new DistributedResourceQueueItem((DistributedResource)r,
                                      DistributedResourceQueueItem.DistributedResourceQueueItemType.Event, arguments, index));
                }
                else
                {
                    var et = r.Instance.Template.GetEventTemplate(index);
                    if (et != null)
                    {
                        reply.Trigger(new DistributedResourceQueueItem((DistributedResource)r, 
                                      DistributedResourceQueueItem.DistributedResourceQueueItemType.Event, arguments, index));
                    }
                    else
                    {    // ft found, fi not found, this should never happen
                        queue.Remove(reply);
                    }
                }
            });
        }
        */
    }

    void IIPEventChildAdded(uint resourceId, uint childId)
    {
        Fetch(resourceId, null).Then(parent =>
        {
            Fetch(childId, null).Then(child =>
            {
                parent.children.Add(child);
                child.parents.Add(parent);

                //parent.Instance.Children.Add(child);
            });
        });
    }

    void IIPEventChildRemoved(uint resourceId, uint childId)
    {
        Fetch(resourceId, null).Then(parent =>
        {
            Fetch(childId, null).Then(child =>
            {
                parent.children.Remove(child);
                child.parents.Remove(parent);

                //                    parent.Instance.Children.Remove(child);
            });
        });
    }

    void IIPEventRenamed(uint resourceId, string name)
    {
        Fetch(resourceId, null).Then(resource =>
        {
            resource.Instance.Variables["name"] = name;
        });
    }


    void IIPEventAttributesUpdated(uint resourceId, byte[] attributes)
    {
        Fetch(resourceId, null).Then(resource =>
        {
            var attrs = attributes.GetStringArray(0, (uint)attributes.Length);

            GetAttributes(resource, attrs).Then(s =>
            {
                resource.Instance.SetAttributes(s);
            });
        });
    }

    void IIPRequestAttachResource(uint callback, uint resourceId)
    {

        Warehouse.GetById(resourceId).Then((res) =>
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

                //r.Instance.ResourceEventOccurred -= Instance_EventOccurred;
                //r.Instance.CustomResourceEventOccurred -= Instance_CustomEventOccurred;
                //r.Instance.ResourceModified -= Instance_PropertyModified;
                //r.Instance.ResourceDestroyed -= Instance_ResourceDestroyed;


                // r.Instance.Children.OnAdd -= Children_OnAdd;
                // r.Instance.Children.OnRemoved -= Children_OnRemoved;

                //r.Instance.Attributes.OnModified -= Attributes_OnModified;

                // Console.WriteLine("Attach {0} {1}", r.Instance.Link, r.Instance.Id);

                // add it to attached resources so GC won't remove it from memory
                ///attachedResources.Add(r);

                var link = DC.ToBytes(r.Instance.Link);

                if (r is DistributedResource)
                {
                    // reply ok
                    SendReply(IIPPacketAction.AttachResource, callback)
                        .AddUUID(r.Instance.Template.ClassId)
                        .AddUInt64(r.Instance.Age)
                        .AddUInt16((ushort)link.Length)
                        .AddUInt8Array(link)
                        //.AddUInt8Array(DataSerializer.PropertyValueArrayComposer((r as DistributedResource)._Serialize(), this, true))
                        .AddUInt8Array(Codec.Compose((r as DistributedResource)._Serialize(), this))
                        .Done();
                }
                else
                {
                    // reply ok
                    SendReply(IIPPacketAction.AttachResource, callback)
                        .AddUUID(r.Instance.Template.ClassId)
                        .AddUInt64(r.Instance.Age)
                        .AddUInt16((ushort)link.Length)
                        .AddUInt8Array(link)
                        .AddUInt8Array(Codec.Compose(r.Instance.Serialize(), this))
                        //.AddUInt8Array(DataSerializer.PropertyValueArrayComposer(r.Instance.Serialize(), this, true))
                        .Done();
                }



                // subscribe
                //r.Instance.ResourceEventOccurred += Instance_EventOccurred;
                //r.Instance.CustomResourceEventOccurred += Instance_CustomEventOccurred;
                //r.Instance.ResourceModified += Instance_PropertyModified;
                //r.Instance.ResourceDestroyed += Instance_ResourceDestroyed;

                Subscribe(r);

                //r.Instance.Children.OnAdd += Children_OnAdd;
                //r.Instance.Children.OnRemoved += Children_OnRemoved;

                //r.Instance.Attributes.OnModified += Attributes_OnModified;


            }
            else
            {
                // reply failed
                //SendParams(0x80, r.Instance.Id, r.Instance.Age, r.Instance.Serialize(false, this));

                Global.Log("DistributedConnection", LogType.Debug, "Not found " + resourceId);

                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ResourceNotFound);
            }
        });
    }

    private void Attributes_OnModified(string key, object oldValue, object newValue, KeyList<string, object> sender)
    {
        if (key == "name")
        {
            var instance = (sender.Owner as Instance);
            var name = DC.ToBytes(newValue.ToString());
            SendEvent(IIPPacketEvent.ChildRemoved)
                    .AddUInt32(instance.Id)
                    .AddUInt16((ushort)name.Length)
                    .AddUInt8Array(name)
                    .Done();
        }
    }

    private void Children_OnRemoved(Instance sender, IResource value)
    {
        SendEvent(IIPPacketEvent.ChildRemoved)
            .AddUInt32(sender.Id)
            .AddUInt32(value.Instance.Id)
            .Done();
    }

    private void Children_OnAdd(Instance sender, IResource value)
    {
        //if (sender.Applicable(sender.Resource, this.session, ActionType.))
        SendEvent(IIPPacketEvent.ChildAdded)
            .AddUInt32(sender.Id)
            .AddUInt32(value.Instance.Id)
            .Done();
    }


    public bool RemoveChild(IResource parent, IResource child)
    {
        SendEvent(IIPPacketEvent.ChildRemoved)
            .AddUInt32((parent as DistributedResource).DistributedResourceInstanceId)
            .AddUInt32((child as DistributedResource).DistributedResourceInstanceId)
            .Done();

        return true;
    }

    public bool AddChild(IResource parent, IResource child)
    {
        SendEvent(IIPPacketEvent.ChildAdded)
            .AddUInt32((parent as DistributedResource).DistributedResourceInstanceId)
            .AddUInt32((child as DistributedResource).DistributedResourceInstanceId)
            .Done();

        return true;
    }


    void IIPRequestReattachResource(uint callback, uint resourceId, ulong resourceAge)
    {
        Warehouse.GetById(resourceId).Then((res) =>
        {
            if (res != null)
            {
                var r = res as IResource;
                // unsubscribe
                Unsubscribe(r);
                Subscribe(r);

                //r.Instance.ResourceEventOccurred -= Instance_EventOccurred;
                //r.Instance.CustomResourceEventOccurred -= Instance_CustomEventOccurred;
                //r.Instance.ResourceModified -= Instance_PropertyModified;
                //r.Instance.ResourceDestroyed -= Instance_ResourceDestroyed;

                //r.Instance.Children.OnAdd -= Children_OnAdd;
                //r.Instance.Children.OnRemoved -= Children_OnRemoved;

                //r.Instance.Attributes.OnModified -= Attributes_OnModified;

                // subscribe
                //r.Instance.ResourceEventOccurred += Instance_EventOccurred;
                //r.Instance.CustomResourceEventOccurred += Instance_CustomEventOccurred;
                //r.Instance.ResourceModified += Instance_PropertyModified;
                //r.Instance.ResourceDestroyed += Instance_ResourceDestroyed;

                //r.Instance.Children.OnAdd += Children_OnAdd;
                //r.Instance.Children.OnRemoved += Children_OnRemoved;

                //r.Instance.Attributes.OnModified += Attributes_OnModified;

                // reply ok
                SendReply(IIPPacketAction.ReattachResource, callback)
                        .AddUInt64(r.Instance.Age)
                        .AddUInt8Array(Codec.Compose(r.Instance.Serialize(), this))
                            .Done();
            }
            else
            {
                // reply failed
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ResourceNotFound);
            }
        });
    }

    void IIPRequestDetachResource(uint callback, uint resourceId)
    {
        Warehouse.GetById(resourceId).Then((res) =>
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
                SendReply(IIPPacketAction.DetachResource, callback).Done();
            }
            else
            {
                // reply failed
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ResourceNotFound);
            }
        });
    }

    void IIPRequestCreateResource(uint callback, uint storeId, uint parentId, byte[] content)
    {

        Warehouse.GetById(storeId).Then(store =>
        {
            if (store == null)
            {
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.StoreNotFound);
                return;
            }

            if (!(store is IStore))
            {
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ResourceIsNotStore);
                return;
            }

            // check security
            if (store.Instance.Applicable(session, ActionType.CreateResource, null) != Ruling.Allowed)
            {
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.CreateDenied);
                return;
            }

            Warehouse.GetById(parentId).Then(parent =>
            {

                // check security

                if (parent != null)
                    if (parent.Instance.Applicable(session, ActionType.AddChild, null) != Ruling.Allowed)
                    {
                        SendError(ErrorType.Management, callback, (ushort)ExceptionCode.AddChildDenied);
                        return;
                    }

                uint offset = 0;

                var className = content.GetString(offset + 1, content[0]);
                offset += 1 + (uint)content[0];

                var nameLength = content.GetUInt16(offset, Endian.Little);
                offset += 2;
                var name = content.GetString(offset, nameLength);

                var cl = content.GetUInt32(offset, Endian.Little);
                offset += 4;

                var type = Type.GetType(className);

                if (type == null)
                {
                    SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ClassNotFound);
                    return;
                }

                DataDeserializer.ListParser(content, offset, cl, this, null).Then(parameters =>
                {
                    offset += cl;
                    cl = content.GetUInt32(offset, Endian.Little);

                    //Codec.ParseStructure(content, offset, cl, this).Then(attributes =>
                    DataDeserializer.TypedMapParser(content, offset, cl, this, null).Then(attributes =>
                    {
                        offset += cl;
                        cl = (uint)content.Length - offset;

                        //Codec.ParseStructure(content, offset, cl, this).Then(values =>
                        DataDeserializer.TypedMapParser(content, offset, cl, this, null).Then(values =>
                        {

#if NETSTANDARD
                            var constructors = Type.GetType(className).GetTypeInfo().GetConstructors();
#else
                                var constructors = Type.GetType(className).GetConstructors();
#endif

                            var matching = constructors.Where(x =>
                            {
                                var ps = x.GetParameters();
                                if (ps.Length > 0 && ps.Length == parameters.Length + 1)
                                    if (ps.Last().ParameterType == typeof(DistributedConnection))
                                        return true;

                                return ps.Length == parameters.Length;
                            }
                            ).ToArray();

                            var pi = matching[0].GetParameters();

                            // cast arguments
                            object[] args = null;

                            if (pi.Length > 0)
                            {
                                int argsCount = pi.Length;
                                args = new object[pi.Length];

                                if (pi[pi.Length - 1].ParameterType == typeof(DistributedConnection))
                                {
                                    args[--argsCount] = this;
                                }

                                if (parameters != null)
                                {
                                    for (int i = 0; i < argsCount && i < parameters.Length; i++)
                                    {
                                        args[i] = DC.CastConvert(parameters[i], pi[i].ParameterType);
                                    }
                                }
                            }

                            // create the resource
                            var resource = Activator.CreateInstance(type, args) as IResource;

                            Warehouse.Put(name, resource, store as IStore, parent).Then(ok =>
                           {
                               SendReply(IIPPacketAction.CreateResource, callback)
                              .AddUInt32(resource.Instance.Id)
                              .Done();

                           }).Error(x =>
                           {
                               SendError(ErrorType.Exception, callback, (ushort)ExceptionCode.AddToStoreFailed);
                           });

                        });
                    });
                });
            });
        });
    }

    void IIPRequestDeleteResource(uint callback, uint resourceId)
    {
        Warehouse.GetById(resourceId).Then(r =>
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

            if (Warehouse.Remove(r))
                SendReply(IIPPacketAction.DeleteResource, callback).Done();
            //SendParams((byte)0x84, callback);
            else
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.DeleteFailed);
        });
    }

    void IIPRequestGetAttributes(uint callback, uint resourceId, byte[] attributes, bool all = false)
    {
        Warehouse.GetById(resourceId).Then(r =>
        {
            if (r == null)
            {
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ResourceNotFound);
                return;
            }

            //                if (!r.Instance.Store.Instance.Applicable(r, session, ActionType.InquireAttributes, null))
            if (r.Instance.Applicable(session, ActionType.InquireAttributes, null) != Ruling.Allowed)
            {
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ViewAttributeDenied);
                return;
            }

            string[] attrs = null;

            if (!all)
                attrs = attributes.GetStringArray(0, (uint)attributes.Length);

            var st = r.Instance.GetAttributes(attrs);

            if (st != null)
                SendReply(all ? IIPPacketAction.GetAllAttributes : IIPPacketAction.GetAttributes, callback)
                          .AddUInt8Array(Codec.Compose(st, this))
                          .Done();
            else
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.GetAttributesFailed);

        });
    }

    void IIPRequestAddChild(uint callback, uint parentId, uint childId)
    {
        Warehouse.GetById(parentId).Then(parent =>
        {
            if (parent == null)
            {
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ResourceNotFound);
                return;
            }

            Warehouse.GetById(childId).Then(child =>
            {
                if (child == null)
                {
                    SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ResourceNotFound);
                    return;
                }

                if (parent.Instance.Applicable(this.session, ActionType.AddChild, null) != Ruling.Allowed)
                {
                    SendError(ErrorType.Management, callback, (ushort)ExceptionCode.AddChildDenied);
                    return;
                }

                if (child.Instance.Applicable(this.session, ActionType.AddParent, null) != Ruling.Allowed)
                {
                    SendError(ErrorType.Management, callback, (ushort)ExceptionCode.AddParentDenied);
                    return;
                }

                parent.Instance.Store.AddChild(parent, child);

                SendReply(IIPPacketAction.AddChild, callback).Done();
                //child.Instance.Parents
            });

        });
    }

    void IIPRequestRemoveChild(uint callback, uint parentId, uint childId)
    {
        Warehouse.GetById(parentId).Then(parent =>
        {
            if (parent == null)
            {
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ResourceNotFound);
                return;
            }

            Warehouse.GetById(childId).Then(child =>
            {
                if (child == null)
                {
                    SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ResourceNotFound);
                    return;
                }

                if (parent.Instance.Applicable(this.session, ActionType.RemoveChild, null) != Ruling.Allowed)
                {
                    SendError(ErrorType.Management, callback, (ushort)ExceptionCode.AddChildDenied);
                    return;
                }

                if (child.Instance.Applicable(this.session, ActionType.RemoveParent, null) != Ruling.Allowed)
                {
                    SendError(ErrorType.Management, callback, (ushort)ExceptionCode.AddParentDenied);
                    return;
                }

                parent.Instance.Store.RemoveChild(parent, child);// Children.Remove(child);

                SendReply(IIPPacketAction.RemoveChild, callback).Done();
                //child.Instance.Parents
            });

        });
    }

    void IIPRequestRenameResource(uint callback, uint resourceId, string name)
    {
        Warehouse.GetById(resourceId).Then(resource =>
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
            SendReply(IIPPacketAction.RenameResource, callback).Done();
        });
    }

    void IIPRequestResourceChildren(uint callback, uint resourceId)
    {
        Warehouse.GetById(resourceId).Then(resource =>
        {
            if (resource == null)
            {
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ResourceNotFound);
                return;
            }

            resource.Instance.Children<IResource>().Then(children =>
            {
                SendReply(IIPPacketAction.ResourceChildren, callback)
                .AddUInt8Array(Codec.Compose(children, this))// Codec.ComposeResourceArray(children, this, true))
                    .Done();

            });


        });
    }

    void IIPRequestResourceParents(uint callback, uint resourceId)
    {
        Warehouse.GetById(resourceId).Then(resource =>
        {
            if (resource == null)
            {
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ResourceNotFound);
                return;
            }

            resource.Instance.Parents<IResource>().Then(parents =>
            {
                SendReply(IIPPacketAction.ResourceParents, callback)
                .AddUInt8Array(Codec.Compose(parents, this))
                //.AddUInt8Array(Codec.ComposeResourceArray(parents, this, true))
                .Done();

            });

        });
    }

    void IIPRequestClearAttributes(uint callback, uint resourceId, byte[] attributes, bool all = false)
    {
        Warehouse.GetById(resourceId).Then(r =>
        {
            if (r == null)
            {
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ResourceNotFound);
                return;
            }

            if (r.Instance.Store.Instance.Applicable(session, ActionType.UpdateAttributes, null) != Ruling.Allowed)
            {
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.UpdateAttributeDenied);
                return;
            }

            string[] attrs = null;

            if (!all)
                attrs = attributes.GetStringArray(0, (uint)attributes.Length);

            if (r.Instance.RemoveAttributes(attrs))
                SendReply(all ? IIPPacketAction.ClearAllAttributes : IIPPacketAction.ClearAttributes, callback).Done();
            else
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.UpdateAttributeFailed);

        });
    }

    void IIPRequestUpdateAttributes(uint callback, uint resourceId, byte[] attributes, bool clearAttributes = false)
    {
        Warehouse.GetById(resourceId).Then(r =>
        {
            if (r == null)
            {
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ResourceNotFound);
                return;
            }

            if (r.Instance.Store.Instance.Applicable(session, ActionType.UpdateAttributes, null) != Ruling.Allowed)
            {
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.UpdateAttributeDenied);
                return;
            }


            DataDeserializer.TypedMapParser(attributes, 0, (uint)attributes.Length, this, null).Then(attrs =>
            {
                if (r.Instance.SetAttributes((Map<string, object>)attrs, clearAttributes))
                    SendReply(clearAttributes ? IIPPacketAction.ClearAllAttributes : IIPPacketAction.ClearAttributes,
                              callback).Done();
                else
                    SendError(ErrorType.Management, callback, (ushort)ExceptionCode.UpdateAttributeFailed);
            });

        });

    }

    void IIPRequestLinkTemplates(uint callback, string resourceLink)
    {
        Action<IResource[]> queryCallback = (r) =>
        {
            if (r == null)
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ResourceNotFound);
            else
            {
                var list = r.Where(x => x.Instance.Applicable(session, ActionType.ViewTemplate, null) != Ruling.Denied).ToArray();

                if (list.Length == 0)
                    SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ResourceNotFound);
                else
                {
                    // get all templates related to this resource

                    var msg = new BinaryList();

                    var templates = new List<TypeTemplate>();
                    foreach (var resource in list)
                        templates.AddRange(TypeTemplate.GetDependencies(resource.Instance.Template).Where(x => !templates.Contains(x)));

                    foreach (var t in templates)
                    {
                        msg.AddInt32(t.Content.Length)
                            .AddUInt8Array(t.Content);
                    }

                    // digggg
                    SendReply(IIPPacketAction.LinkTemplates, callback)
                            //.AddInt32(msg.Length)
                            //.AddUInt8Array(msg.ToArray())
                            .AddUInt8Array(TransmissionType.Compose(TransmissionTypeIdentifier.RawData, msg.ToArray()))
                            .Done();
                }
            }
        };

        if (Server?.EntryPoint != null)
            Server.EntryPoint.Query(resourceLink, this).Then(queryCallback);
        else
            Warehouse.Query(resourceLink).Then(queryCallback);
    }

    void IIPRequestTemplateFromClassName(uint callback, string className)
    {
        var t = Warehouse.GetTemplateByClassName(className);

        if (t != null)
            SendReply(IIPPacketAction.TemplateFromClassName, callback)
                    .AddUInt8Array(TransmissionType.Compose(TransmissionTypeIdentifier.RawData, t.Content))
                    //.AddInt32(t.Content.Length)
                    //.AddUInt8Array(t.Content)
                    .Done();
        else
        {
            // reply failed
            SendError(ErrorType.Management, callback, (ushort)ExceptionCode.TemplateNotFound);
        }
    }

    void IIPRequestTemplateFromClassId(uint callback, UUID classId)
    {
        var t = Warehouse.GetTemplateByClassId(classId);

        if (t != null)
            SendReply(IIPPacketAction.TemplateFromClassId, callback)
                    .AddUInt8Array(TransmissionType.Compose(TransmissionTypeIdentifier.RawData, t.Content))
                    //.AddInt32(t.Content.Length)
                    //.AddUInt8Array(t.Content)
                    .Done();
        else
        {
            // reply failed
            SendError(ErrorType.Management, callback, (ushort)ExceptionCode.TemplateNotFound);
        }
    }



    void IIPRequestTemplateFromResourceId(uint callback, uint resourceId)
    {
        Warehouse.GetById(resourceId).Then((r) =>
        {
            if (r != null)
                SendReply(IIPPacketAction.TemplateFromResourceId, callback)
                        .AddInt32(r.Instance.Template.Content.Length)
                        .AddUInt8Array(r.Instance.Template.Content)
                        .Done();
            else
            {
                // reply failed
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.TemplateNotFound);
            }
        });
    }




    void IIPRequestQueryResources(uint callback, string resourceLink)
    {

        Action<IResource[]> queryCallback = (r) =>
        {
            if (r == null)
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ResourceNotFound);
            else
            {
                var list = r.Where(x => x.Instance.Applicable(session, ActionType.Attach, null) != Ruling.Denied).ToArray();

                if (list.Length == 0)
                    SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ResourceNotFound);
                else
                    SendReply(IIPPacketAction.QueryLink, callback)
                                .AddUInt8Array(Codec.Compose(list, this))
                                //.AddUInt8Array(Codec.ComposeResourceArray(list, this, true))
                                .Done();
            }
        };

        if (Server?.EntryPoint != null)
            Server.EntryPoint.Query(resourceLink, this).Then(queryCallback);
        else
            Warehouse.Query(resourceLink).Then(queryCallback);
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


    void IIPRequestProcedureCall(uint callback, string procedureCall, TransmissionType transmissionType, byte[] content)
    {

        if (Server == null)
        {
            SendError(ErrorType.Management, callback, (ushort)ExceptionCode.GeneralFailure);
            return;
        }

        var call = Server.Calls[procedureCall];

        if (call == null)
        {
            SendError(ErrorType.Management, callback, (ushort)ExceptionCode.MethodNotFound);
            return;
        }

        var (_, parsed) = Codec.Parse(content, 0, this, null, transmissionType);

        parsed.Then(results =>
        {
            var arguments = (Map<byte, object>)results;// (object[])results;

            // un hold the socket to send data immediately
            this.Socket.Unhold();

            // @TODO: Make managers for procedure calls
            //if (r.Instance.Applicable(session, ActionType.Execute, ft) == Ruling.Denied)
            //{
            //    SendError(ErrorType.Management, callback,
            //        (ushort)ExceptionCode.InvokeDenied);
            //    return;
            //}

            InvokeFunction(call.Value.Template, callback, arguments, IIPPacketAction.ProcedureCall, call.Value.Delegate.Target);

        }).Error(x =>
        {
            SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ParseError);
        });
    }

    void IIPRequestStaticCall(uint callback, UUID classId, byte index, TransmissionType transmissionType, byte[] content)
    {
        var template = Warehouse.GetTemplateByClassId(classId);

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

        var (_, parsed) = Codec.Parse(content, 0, this, null, transmissionType);

        parsed.Then(results =>
        {
            var arguments = (Map<byte, object>)results;// (object[])results;

            // un hold the socket to send data immediately
            this.Socket.Unhold();

            var fi = ft.MethodInfo;

            if (fi == null)
            {
                // ft found, fi not found, this should never happen
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.MethodNotFound);
                return;
            }

            // @TODO: Make managers for static calls
            //if (r.Instance.Applicable(session, ActionType.Execute, ft) == Ruling.Denied)
            //{
            //    SendError(ErrorType.Management, callback,
            //        (ushort)ExceptionCode.InvokeDenied);
            //    return;
            //}

            InvokeFunction(ft, callback, arguments, IIPPacketAction.StaticCall, null);

        }).Error(x =>
        {
            SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ParseError);
        });
    }

    void IIPRequestInvokeFunction(uint callback, uint resourceId, byte index, TransmissionType transmissionType, byte[] content)
    {
        //Console.WriteLine("IIPRequestInvokeFunction " + callback + " " + resourceId + "  " + index);

        Warehouse.GetById(resourceId).Then((r) =>
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

            var (_, parsed) = Codec.Parse(content, 0, this, null, transmissionType);

            parsed.Then(results =>
            {
                var arguments = (Map<byte, object>)results;// (object[])results;

                // un hold the socket to send data immediately
                this.Socket.Unhold();

                if (r is DistributedResource)
                {
                    var rt = (r as DistributedResource)._Invoke(index, arguments);
                    if (rt != null)
                    {
                        rt.Then(res =>
                        {
                            SendReply(IIPPacketAction.InvokeFunction, callback)
                                        .AddUInt8Array(Codec.Compose(res, this))
                                        .Done();
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

                    //var fi = r.GetType().GetMethod(ft.Name);

                    //if (fi == null)
                    //{
                    //    // ft found, fi not found, this should never happen
                    //    SendError(ErrorType.Management, callback, (ushort)ExceptionCode.MethodNotFound);
                    //    return;
                    //}


                    if (r.Instance.Applicable(session, ActionType.Execute, ft) == Ruling.Denied)
                    {
                        SendError(ErrorType.Management, callback,
                            (ushort)ExceptionCode.InvokeDenied);
                        return;
                    }

                    InvokeFunction(ft, callback, arguments, IIPPacketAction.InvokeFunction, r);
                }
            });
        });
    }



    void InvokeFunction(FunctionTemplate ft, uint callback, Map<byte, object> arguments, IIPPacketAction actionType, object target = null)
    {

        // cast arguments
        ParameterInfo[] pis = ft.MethodInfo.GetParameters();

        object[] args = new object[pis.Length];

        InvocationContext context = null;

        if (pis.Length > 0)
        {
            if (pis.Last().ParameterType == typeof(DistributedConnection))
            {
                for (byte i = 0; i < pis.Length - 1; i++)
                {
                    if (arguments.ContainsKey(i))
                        args[i] = DC.CastConvert(arguments[i], pis[i].ParameterType);
                    else if (ft.Arguments[i].Type.Nullable)
                        args[i] = null;
                    else
                        args[i] = Type.Missing;

                }

                args[args.Length - 1] = this;
            }
            else if (pis.Last().ParameterType == typeof(InvocationContext))
            {
                context = new InvocationContext(this, callback);

                for (byte i = 0; i < pis.Length - 1; i++)
                {
                    if (arguments.ContainsKey(i))
                        args[i] = DC.CastConvert(arguments[i], pis[i].ParameterType);
                    else if (ft.Arguments[i].Type.Nullable)
                        args[i] = null;
                    else
                        args[i] = Type.Missing;

                }

                args[args.Length - 1] = context;

            }
            else
            {
                for (byte i = 0; i < pis.Length; i++)
                {
                    if (arguments.ContainsKey(i))
                        args[i] = DC.CastConvert(arguments[i], pis[i].ParameterType);
                    else if (ft.Arguments[i].Type.Nullable) //Nullable.GetUnderlyingType(pis[i].ParameterType) != null)
                        args[i] = null;
                    else
                        args[i] = Type.Missing;
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
                SendReply(actionType, callback)
                .AddUInt8((byte)TransmissionTypeIdentifier.Null)
                .Done();

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
                SendReply(actionType, callback)
                 .AddUInt8Array(Codec.Compose(res, this))
                 .Done();
            });

        }
        else if (rt is AsyncReply)
        {
            (rt as AsyncReply).Then(res =>
            {
                if (context != null)
                    context.Ended = true;

                SendReply(actionType, callback)
                            .AddUInt8Array(Codec.Compose(res, this))
                            .Done();
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
            });
        }
        else
        {
            if (context != null)
                context.Ended = true;

            SendReply(actionType, callback)
                    .AddUInt8Array(Codec.Compose(rt, this))
                    .Done();
        }
    }

    void IIPRequestListen(uint callback, uint resourceId, byte index)
    {
        Warehouse.GetById(resourceId).Then((r) =>
        {
            if (r != null)
            {
                var et = r.Instance.Template.GetEventTemplateByIndex(index);

                if (et != null)
                {
                    if (r is DistributedResource)
                    {
                        (r as DistributedResource).Listen(et).Then(x =>
                       {
                           SendReply(IIPPacketAction.Listen, callback).Done();
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

                            SendReply(IIPPacketAction.Listen, callback).Done();
                        }
                    }
                }
                else
                {
                    // pt not found
                    SendError(ErrorType.Management, callback, (ushort)ExceptionCode.MethodNotFound);
                }
            }
            else
            {
                // resource not found
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ResourceNotFound);
            }
        });

    }

    void IIPRequestUnlisten(uint callback, uint resourceId, byte index)
    {
        Warehouse.GetById(resourceId).Then((r) =>
        {
            if (r != null)
            {
                var et = r.Instance.Template.GetEventTemplateByIndex(index);

                if (et != null)
                {
                    if (r is DistributedResource)
                    {
                        (r as DistributedResource).Unlisten(et).Then(x =>
                        {
                            SendReply(IIPPacketAction.Unlisten, callback).Done();
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
                                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.AlreadyUnlistened);
                                return;
                            }

                            subscriptions[r].Remove(index);

                            SendReply(IIPPacketAction.Unlisten, callback).Done();
                        }
                    }
                }
                else
                {
                    // pt not found
                    SendError(ErrorType.Management, callback, (ushort)ExceptionCode.MethodNotFound);
                }
            }
            else
            {
                // resource not found
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ResourceNotFound);
            }
        });

    }

    //        void IIPRequestGetProperty(uint callback, uint resourceId, byte index)
    //        {
    //            Warehouse.GetById(resourceId).Then((r) =>
    //            {
    //                if (r != null)
    //                {
    //                    var pt = r.Instance.Template.GetPropertyTemplateByIndex(index);
    //                    if (pt != null)
    //                    {
    //                        if (r is DistributedResource)
    //                        {
    //                            SendReply(IIPPacket.IIPPacketAction.GetProperty, callback)
    //                                        .AddUInt8Array(Codec.Compose((r as DistributedResource)._Get(pt.Index), this))
    //                                        .Done();
    //                        }
    //                        else
    //                        {
    //#if NETSTANDARD
    //                            var pi = r.GetType().GetTypeInfo().GetProperty(pt.Name);
    //#else
    //                            var pi = r.GetType().GetProperty(pt.Name);
    //#endif

    //                            if (pi != null)
    //                            {
    //                                SendReply(IIPPacket.IIPPacketAction.GetProperty, callback)
    //                                            .AddUInt8Array(Codec.Compose(pi.GetValue(r), this))
    //                                            .Done();
    //                            }
    //                            else
    //                            {
    //                                // pt found, pi not found, this should never happen
    //                            }
    //                        }
    //                    }
    //                    else
    //                    {
    //                        // pt not found
    //                    }
    //                }
    //                else
    //                {
    //                    // resource not found
    //                }
    //            });
    //        }

    void IIPRequestInquireResourceHistory(uint callback, uint resourceId, DateTime fromDate, DateTime toDate)
    {
        Warehouse.GetById(resourceId).Then((r) =>
        {
            if (r != null)
            {
                r.Instance.Store.GetRecord(r, fromDate, toDate).Then((results) =>
                {
                    var history = DataSerializer.HistoryComposer(results, this, true);

                    /*
                    ulong fromAge = 0;
                    ulong toAge = 0;

                    if (results.Count > 0)
                    {
                        var firstProp = results.Values.First();
                        //var lastProp = results.Values.Last();

                        if (firstProp.Length > 0)
                        {
                            fromAge = firstProp[0].Age;
                            toAge = firstProp.Last().Age;
                        }

                    }*/

                    SendReply(IIPPacketAction.ResourceHistory, callback)
                            .AddUInt8Array(history)
                            .Done();

                });
            }
        });
    }

    //        void IIPRequestGetPropertyIfModifiedSince(uint callback, uint resourceId, byte index, ulong age)
    //        {
    //            Warehouse.GetById(resourceId).Then((r) =>
    //            {
    //                if (r != null)
    //                {
    //                    var pt = r.Instance.Template.GetFunctionTemplateByIndex(index);
    //                    if (pt != null)
    //                    {
    //                        if (r.Instance.GetAge(index) > age)
    //                        {
    //#if NETSTANDARD
    //                            var pi = r.GetType().GetTypeInfo().GetProperty(pt.Name);
    //#else
    //                            var pi = r.GetType().GetProperty(pt.Name);
    //#endif
    //                            if (pi != null)
    //                            {
    //                                SendReply(IIPPacket.IIPPacketAction.GetPropertyIfModified, callback)
    //                                            .AddUInt8Array(Codec.Compose(pi.GetValue(r), this))
    //                                            .Done();
    //                            }
    //                            else
    //                            {
    //                                // pt found, pi not found, this should never happen
    //                            }
    //                        }
    //                        else
    //                        {
    //                            SendReply(IIPPacket.IIPPacketAction.GetPropertyIfModified, callback)
    //                                    .AddUInt8((byte)DataType.NotModified)
    //                                    .Done();
    //                        }
    //                    }
    //                    else
    //                    {
    //                        // pt not found
    //                    }
    //                }
    //                else
    //                {
    //                    // resource not found
    //                }
    //            });
    //        }

    void IIPRequestSetProperty(uint callback, uint resourceId, byte index, TransmissionType transmissionType, byte[] content)
    {

        // un hold the socket to send data immediately
        this.Socket.Unhold();

        Warehouse.GetById(resourceId).Then((r) =>
        {
            if (r != null)
            {

                var pt = r.Instance.Template.GetPropertyTemplateByIndex(index);
                if (pt != null)
                {
                    var (_, parsed) = Codec.Parse(content, 0, this, null, transmissionType);
                    parsed.Then((value) =>
                    {
                        if (r is DistributedResource)
                        {
                            // propagation
                            (r as DistributedResource)._Set(index, value).Then((x) =>
                        {
                            SendReply(IIPPacketAction.SetProperty, callback).Done();
                        }).Error(x =>
                        {
                            SendError(x.Type, callback, (ushort)x.Code, x.Message);
                        });
                        }
                        else
                        {

                            /*
    #if NETSTANDARD
                            var pi = r.GetType().GetTypeInfo().GetProperty(pt.Name);
    #else
                            var pi = r.GetType().GetProperty(pt.Name);
    #endif*/

                            var pi = pt.PropertyInfo;

                            if (pi != null)
                            {

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


                                if (pi.PropertyType.IsGenericType && pi.PropertyType.GetGenericTypeDefinition()
                                    == typeof(DistributedPropertyContext<>))
                                {
                                    value = Activator.CreateInstance(pi.PropertyType, this, value);
                                    //value = new DistributedPropertyContext(this, value);
                                }
                                else
                                {
                                    // cast new value type to property type
                                    value = DC.CastConvert(value, pi.PropertyType);
                                }


                                try
                                {

                                    pi.SetValue(r, value);
                                    SendReply(IIPPacketAction.SetProperty, callback).Done();
                                }
                                catch (Exception ex)
                                {
                                    SendError(ErrorType.Exception, callback, 0, ex.Message);
                                }

                            }
                            else
                            {
                                // pt found, pi not found, this should never happen
                                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.PropertyNotFound);
                            }
                        }

                    });
                }
                else
                {
                    // property not found
                    SendError(ErrorType.Management, callback, (ushort)ExceptionCode.PropertyNotFound);
                }
            }
            else
            {
                // resource not found
                SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ResourceNotFound);
            }
        });
    }

    /*
    void IIPReplyAttachResource(uint callback, uint resourceAge, object[] properties)
    {
        if (requests.ContainsKey(callback))
        {
            var req = requests[callback];
            var r = resources[(uint)req.Arguments[0]];

            if (r == null)
            {
                r.Instance.Deserialize(properties);
                r.Instance.Age = resourceAge;
                r.Attached();

                // process stack
                foreach (var rr in resources.Values)
                    rr.Stack.ProcessStack();
            }
            else
            {
                // resource not found
            }
        }
    }

    void IIPReplyReattachResource(uint callback, uint resourceAge, object[] properties)
    {
        var req = requests.Take(callback);

        if (req != null)
        {
            var r = resources[(uint)req.Arguments[0]];

            if (r == null)
            {
                r.Instance.Deserialize(properties);
                r.Instance.Age = resourceAge;
                r.Attached();

                // process stack
                foreach (var rr in resources.Values)
                    rr.Stack.ProcessStack();
            }
            else
            {
                // resource not found
            }
        }
    }


    void IIPReplyDetachResource(uint callback)
    {
        var req = requests.Take(callback);
        // nothing to do
    }

    void IIPReplyCreateResource(uint callback, Guid classId, uint resourceId)
    {
        var req = requests.Take(callback);
        // nothing to do

    }
    void IIPReplyDeleteResource(uint callback)
    {
        var req = requests.Take(callback);
        // nothing to do

    }

    void IIPReplyTemplateFromClassName(uint callback, ResourceTemplate template)
    {
        // cache
        if (!templates.ContainsKey(template.ClassId))
            templates.Add(template.ClassId, template);

        var req = requests.Take(callback);
        req?.Trigger(template);
    }

    void IIPReplyTemplateFromClassId(uint callback, ResourceTemplate template)
    {
        // cache
        if (!templates.ContainsKey(template.ClassId))
            templates.Add(template.ClassId, template);

        var req = requests.Take(callback);
        req?.Trigger(template);

    }

    void IIPReplyTemplateFromResourceLink(uint callback, ResourceTemplate template)
    {
        // cache
        if (!templates.ContainsKey(template.ClassId))
            templates.Add(template.ClassId, template);

        var req = requests.Take(callback);
        req?.Trigger(template);
    }

    void IIPReplyTemplateFromResourceId(uint callback, ResourceTemplate template)
    {
        // cache
        if (!templates.ContainsKey(template.ClassId))
            templates.Add(template.ClassId, template);

        var req = requests.Take(callback);
        req?.Trigger(template);
    }

    void IIPReplyResourceIdFromResourceLink(uint callback, Guid classId, uint resourceId, uint resourceAge)
    {
        var req = requests.Take(callback);
        req?.Trigger(template);
    }

    void IIPReplyInvokeFunction(uint callback, object returnValue)
    {

    }

    void IIPReplyGetProperty(uint callback, object value)
    {

    }
    void IIPReplyGetPropertyIfModifiedSince(uint callback, object value)
    {

    }
    void IIPReplySetProperty(uint callback)
    {

    }
    */

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

        SendRequest(IIPPacketAction.TemplateFromClassId)
                    .AddUUID(classId)
                    .Done()
                    .Then((rt) =>
                    {
                        templateRequests.Remove(classId);
                        templates.Add(((TypeTemplate)rt[0]).ClassId, (TypeTemplate)rt[0]);
                        Warehouse.PutTemplate(rt[0] as TypeTemplate);
                        reply.Trigger(rt[0]);
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

        var classNameBytes = DC.ToBytes(className);

        SendRequest(IIPPacketAction.TemplateFromClassName)
            .AddUInt8((byte)classNameBytes.Length)
            .AddUInt8Array(classNameBytes)
                    .Done()
                    .Then((rt) =>
                    {
                        templateByNameRequests.Remove(className);
                        templates.Add(((TypeTemplate)rt[0]).ClassId, (TypeTemplate)rt[0]);
                        Warehouse.PutTemplate(rt[0] as TypeTemplate);
                        reply.Trigger(rt[0]);
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

        Query(path).Then(ar =>
        {

            //if (filter != null)
            //  ar = ar?.Where(filter).ToArray();

            // MISSING: should dispatch the unused resources. 
            if (ar?.Length > 0)
                rt.Trigger(ar[0]);
            else
                rt.Trigger(null);
        }).Error(ex => rt.TriggerError(ex));


        return rt;

        /*

        if (pathRequests.ContainsKey(path))
            return pathRequests[path];

        var reply = new AsyncReply<IResource>();
        pathRequests.Add(path, reply);

        var bl = new BinaryList(path);
        bl.Insert(0, (ushort)bl.Length);

        SendRequest(IIPPacket.IIPPacketAction.QueryLink, bl.ToArray()).Then((rt) =>
        {
            pathRequests.Remove(path);
            //(Guid)rt[0],
            Fetch((uint)rt[1]).Then((r) =>
           {
               reply.Trigger(r);
           });
        }).Error((ex) =>
        {
            reply.TriggerError(ex);
        }); ;


        return reply;
        */
    }

    ///// <summary>
    ///// Retrive a resource by its instance Id.
    ///// </summary>
    ///// <param name="iid">Instance Id</param>
    ///// <returns>Resource</returns>
    //public AsyncReply<IResource> Retrieve(uint iid)
    //{
    //    foreach (var r in resources.Values)
    //        if (r.Instance.Id == iid)
    //            return new AsyncReply<IResource>(r);
    //    return new AsyncReply<IResource>(null);
    //}


    public AsyncReply<TypeTemplate[]> GetLinkTemplates(string link)
    {
        var reply = new AsyncReply<TypeTemplate[]>();

        var l = DC.ToBytes(link);

        SendRequest(IIPPacketAction.LinkTemplates)
        .AddUInt16((ushort)l.Length)
        .AddUInt8Array(l)
        .Done()
        .Then((rt) =>
        {

            var templates = new List<TypeTemplate>();
            // parse templates
            var tt = (TransmissionType)rt[0];
            var data = (byte[])rt[1];
            //var offset = 0;
            for (var offset = tt.Offset; offset < tt.ContentLength;)
            {
                var cs = data.GetUInt32(offset, Endian.Little);
                offset += 4;
                templates.Add(TypeTemplate.Parse(data, offset, cs));
                offset += cs;
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

        SendRequest(IIPPacketAction.AttachResource)
                    .AddUInt32(id)
                    .Done()
                    .Then((rt) =>
                    {

                        if (rt == null)
                        {
                            reply.TriggerError(new AsyncException(ErrorType.Management,
                                (ushort)ExceptionCode.ResourceNotFound, "Null response"));
                            return;
                        }

                        DistributedResource dr;
                        TypeTemplate template = null;
                        UUID classId = (UUID)rt[0];

                        if (resource == null)
                        {
                            template = Warehouse.GetTemplateByClassId(classId, TemplateType.Resource);
                            if (template?.DefinedType != null && template.IsWrapper)
                                dr = Activator.CreateInstance(template.DefinedType, this, id, (ulong)rt[1], (string)rt[2]) as DistributedResource;
                            else
                                dr = new DistributedResource(this, id, (ulong)rt[1], (string)rt[2]);
                        }
                        else
                        {
                            dr = resource;
                            template = resource.Instance.Template;
                        }

                        var transmissionType = (TransmissionType)rt[3];
                        var content = (byte[])rt[4];

                        var initResource = (DistributedResource ok) =>
                        {
                            var (_, parsed) = Codec.Parse(content, 0, this, newSequence, transmissionType);
                            parsed.Then(results =>
                            {
                                var ar = results as object[];

                                var pvs = new List<PropertyValue>();

                                for (var i = 0; i < ar.Length; i += 3)
                                    pvs.Add(new PropertyValue(ar[i + 2], Convert.ToUInt64(ar[i]), (DateTime)ar[i + 1]));

                                dr._Attach(pvs.ToArray());// (PropertyValue[])pvs);
                                resourceRequests.Remove(id);
                                // move from needed to attached.
                                neededResources.Remove(id);
                                attachedResources[id] = new WeakReference<DistributedResource>(dr);
                                reply.Trigger(dr);
                            }).Error(ex => reply.TriggerError(ex));

                        };

                        if (template == null)
                        {
                            GetTemplate((UUID)rt[0]).Then((tmp) =>
                            {
                                // ClassId, ResourceAge, ResourceLink, Content
                                if (resource == null)
                                {
                                    Warehouse.Put(id.ToString(), dr, this, null, tmp).Then(initResource).Error(ex => reply.TriggerError(ex));
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
                                Warehouse.Put(id.ToString(), dr, this, null, template)
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


    public AsyncReply<IResource[]> GetChildren(IResource resource)
    {
        var rt = new AsyncReply<IResource[]>();

        SendRequest(IIPPacketAction.ResourceChildren)
                    .AddUInt32(resource.Instance.Id)
                    .Done()
                    .Then(ar =>
                    {
                        var dataType = (TransmissionType)ar[0];
                        var data = (byte[])ar[1];

                        var (_, parsed) = Codec.Parse(data, dataType.Offset, this, null, dataType);

                        parsed.Then(resources => rt.Trigger(resources))
                              .Error(ex => rt.TriggerError(ex));

                        //Codec.ParseResourceArray(d, 0, (uint)d.Length, this).Then(resources =>
                        //{
                        //  rt.Trigger(resources);
                        //}).Error(ex => rt.TriggerError(ex));
                    });

        return rt;
    }

    public AsyncReply<IResource[]> GetParents(IResource resource)
    {
        var rt = new AsyncReply<IResource[]>();

        SendRequest(IIPPacketAction.ResourceParents)
            .AddUInt32(resource.Instance.Id)
            .Done()
            .Then(ar =>
            {
                var dataType = (TransmissionType)ar[0];
                var data = (byte[])ar[1];
                var (_, parsed) = Codec.Parse(data, dataType.Offset, this, null, dataType);

                parsed.Then(resources => rt.Trigger(resources))
                      .Error(ex => rt.TriggerError(ex));

                //Codec.ParseResourceArray(d, 0, (uint)d.Length, this).Then(resources =>
                //{
                //    rt.Trigger(resources);
                //}).Error(ex => rt.TriggerError(ex));
            });

        return rt;
    }

    public AsyncReply<bool> RemoveAttributes(IResource resource, string[] attributes = null)
    {
        var rt = new AsyncReply<bool>();

        if (attributes == null)
            SendRequest(IIPPacketAction.ClearAllAttributes)
                .AddUInt32(resource.Instance.Id)
                .Done()
                .Then(ar => rt.Trigger(true))
                .Error(ex => rt.TriggerError(ex));
        else
        {
            var attrs = DC.ToBytes(attributes);
            SendRequest(IIPPacketAction.ClearAttributes)
                .AddUInt32(resource.Instance.Id)
                .AddInt32(attrs.Length)
                .AddUInt8Array(attrs)
                .Done()
                .Then(ar => rt.Trigger(true))
                .Error(ex => rt.TriggerError(ex));
        }

        return rt;
    }

    public AsyncReply<bool> SetAttributes(IResource resource, Map<string, object> attributes, bool clearAttributes = false)
    {
        var rt = new AsyncReply<bool>();

        SendRequest(clearAttributes ? IIPPacketAction.UpdateAllAttributes : IIPPacketAction.UpdateAttributes)
            .AddUInt32(resource.Instance.Id)
            //.AddUInt8Array(Codec.ComposeStructure(attributes, this, true, true, true))
            .AddUInt8Array(Codec.Compose(attributes, this))
            .Done()
            .Then(ar => rt.Trigger(true))
            .Error(ex => rt.TriggerError(ex));

        return rt;
    }

    public AsyncReply<Map<string, object>> GetAttributes(IResource resource, string[] attributes = null)
    {
        var rt = new AsyncReply<Map<string, object>>();

        if (attributes == null)
        {
            SendRequest(IIPPacketAction.GetAllAttributes)
                .AddUInt32(resource.Instance.Id)
                .Done()
                .Then(ar =>
                {
                    var dataType = (TransmissionType)ar[0];
                    var data = (byte[])ar[1];
                    //Codec.Parse(d, )
                    var (_, parsed) = Codec.Parse(data, 0, this, null, dataType);
                    parsed.Then(st =>
                    {

                        resource.Instance.SetAttributes(st as Map<string, object>);

                        rt.Trigger(st);
                    }).Error(ex => rt.TriggerError(ex));
                });
        }
        else
        {
            var attrs = DC.ToBytes(attributes);
            SendRequest(IIPPacketAction.GetAttributes)
                .AddUInt32(resource.Instance.Id)
                .AddInt32(attrs.Length)
                .AddUInt8Array(attrs)
                .Done()
                .Then(ar =>
                {
                    var dataType = (TransmissionType)ar[0];
                    var data = (byte[])ar[1];

                    var (_, parsed) = Codec.Parse(data, 0, this, null, dataType);
                    parsed.Then(st =>
                    {

                        resource.Instance.SetAttributes((Map<string, object>)st);

                        rt.Trigger(st);
                    }).Error(ex => rt.TriggerError(ex));
                });
        }

        return rt;
    }

    /// <summary>
    /// Get resource history.
    /// </summary>
    /// <param name="resource">IResource.</param>
    /// <param name="fromDate">From date.</param>
    /// <param name="toDate">To date.</param>
    /// <returns></returns>
    public AsyncReply<KeyList<PropertyTemplate, PropertyValue[]>> GetRecord(IResource resource, DateTime fromDate, DateTime toDate)
    {
        if (resource is DistributedResource)
        {
            var dr = resource as DistributedResource;

            if (dr.DistributedResourceConnection != this)
                return new AsyncReply<KeyList<PropertyTemplate, PropertyValue[]>>(null);

            var reply = new AsyncReply<KeyList<PropertyTemplate, PropertyValue[]>>();

            SendRequest(IIPPacketAction.ResourceHistory)
                .AddUInt32(dr.DistributedResourceInstanceId)
                .AddDateTime(fromDate)
                .AddDateTime(toDate)
                .Done()
                .Then(rt =>
                {
                    var content = (byte[])rt[0];

                    DataDeserializer.HistoryParser(content, 0, (uint)content.Length, resource, this, null)
                                      .Then((history) => reply.Trigger(history));

                }).Error((ex) => reply.TriggerError(ex));

            return reply;
        }
        else
            return new AsyncReply<KeyList<PropertyTemplate, PropertyValue[]>>(null);
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

        SendRequest(IIPPacketAction.QueryLink)
                    .AddUInt16((ushort)str.Length)
                    .AddUInt8Array(str)
                    .Done()
                    .Then(ar =>
                    {
                        var dataType = (TransmissionType)ar[0];
                        var data = ar[1] as byte[];

                        var (_, parsed) = Codec.Parse(data, dataType.Offset, this, null, dataType);

                        parsed.Then(resources => reply.Trigger(resources))
                              .Error(ex => reply.TriggerError(ex));

                        //Codec.ParseResourceArray(content, 0, (uint)content.Length, this)
                        //                      .Then(resources => reply.Trigger(resources));

                    }).Error(ex => reply.TriggerError(ex));

        return reply;
    }


    /// <summary>
    /// Create a new resource.
    /// </summary>
    /// <param name="store">The store in which the resource is saved.</param>
    /// <param name="className">Class full name.</param>
    /// <param name="parameters">Constructor parameters.</param>
    /// <param name="attributes">Resource attributeds.</param>
    /// <param name="values">Values for the resource properties.</param>
    /// <returns>New resource instance</returns>
    public AsyncReply<DistributedResource> Create(IStore store, IResource parent, string className, object[] parameters, Map<string, object> attributes, Map<string, object> values)
    {
        var reply = new AsyncReply<DistributedResource>();
        var pkt = new BinaryList()
                                .AddUInt32(store.Instance.Id)
                                .AddUInt32(parent.Instance.Id)
                                .AddUInt8((byte)className.Length)
                                .AddString(className)
                                .AddUInt8Array(Codec.Compose(parameters, this))
                                .AddUInt8Array(Codec.Compose(attributes, this))
                                .AddUInt8Array(Codec.Compose(values, this));

        pkt.InsertInt32(8, pkt.Length);

        SendRequest(IIPPacketAction.CreateResource)
            .AddUInt8Array(pkt.ToArray())
            .Done()
            .Then(args =>
            {
                var rid = (uint)args[0];

                Fetch(rid, null).Then((r) =>
                {
                    reply.Trigger(r);
                });

            });

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
        SendEvent(IIPPacketEvent.ResourceDestroyed)
                    .AddUInt32(resource.Instance.Id)
                    .Done();


    }

    private void Instance_PropertyModified(PropertyModificationInfo info)
    {
        //var pt = resource.Instance.Template.GetPropertyTemplateByName(name);
        // if (pt == null)
        //    return;

        SendEvent(IIPPacketEvent.PropertyUpdated)
                    .AddUInt32(info.Resource.Instance.Id)
                    .AddUInt8(info.PropertyTemplate.Index)
                    .AddUInt8Array(Codec.Compose(info.Value, this))
                    .Done();

    }

    //        private void Instance_EventOccurred(IResource resource, string name, string[] users, DistributedConnection[] connections, object[] args)

    private void Instance_CustomEventOccurred(CustomEventOccurredInfo info)
    {
        if (info.EventTemplate.Listenable)
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
        SendEvent(IIPPacketEvent.EventOccurred)
                    .AddUInt32(info.Resource.Instance.Id)
                    .AddUInt8((byte)info.EventTemplate.Index)
                    .AddUInt8Array(Codec.Compose(info.Value, this))
                    .Done();
    }

    private void Instance_EventOccurred(EventOccurredInfo info)
    {
        if (info.EventTemplate.Listenable)
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
        SendEvent(IIPPacketEvent.EventOccurred)
                    .AddUInt32(info.Resource.Instance.Id)
                    .AddUInt8((byte)info.EventTemplate.Index)
                    .AddUInt8Array(Codec.Compose(info.Value, this))
                    .Done();
    }



    void IIPRequestKeepAlive(uint callbackId, DateTime peerTime, uint interval)
    {

        uint jitter = 0;

        var now = DateTime.UtcNow;

        if (lastKeepAliveReceived != null)
        {
            var diff = (uint)(now - (DateTime)lastKeepAliveReceived).TotalMilliseconds;
            //Console.WriteLine("Diff " + diff + " " + interval);

            jitter = (uint)Math.Abs((int)diff - (int)interval);
        }

        SendParams()
            .AddUInt8((byte)(0x80 | (byte)IIPPacketAction.KeepAlive))
            .AddUInt32(callbackId)
            .AddDateTime(now)
            .AddUInt32(jitter)
            .Done();

        lastKeepAliveReceived = now;
    }
}
