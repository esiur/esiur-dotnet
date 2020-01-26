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

using Esyur.Data;
using Esyur.Core;
using Esyur.Net.Packets;
using Esyur.Resource;
using Esyur.Resource.Template;
using Esyur.Security.Authority;
using Esyur.Security.Permissions;
using System;
using System.Collections.Generic;

using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Esyur.Net.IIP
{
    partial class DistributedConnection
    {
        KeyList<uint, DistributedResource> resources = new KeyList<uint, DistributedResource>();
        KeyList<uint, AsyncReply<DistributedResource>> resourceRequests = new KeyList<uint, AsyncReply<DistributedResource>>();
        KeyList<Guid, AsyncReply<ResourceTemplate>> templateRequests = new KeyList<Guid, AsyncReply<ResourceTemplate>>();


        KeyList<string, AsyncReply<IResource>> pathRequests = new KeyList<string, AsyncReply<IResource>>();

        Dictionary<Guid, ResourceTemplate> templates = new Dictionary<Guid, ResourceTemplate>();

        KeyList<uint, AsyncReply> requests = new KeyList<uint, AsyncReply>();

        volatile uint callbackCounter = 0;

        AsyncQueue<DistributedResourceQueueItem> queue = new AsyncQueue<DistributedResourceQueueItem>();

        /// <summary>
        /// Send IIP request.
        /// </summary>
        /// <param name="action">Packet action.</param>
        /// <param name="args">Arguments to send.</param>
        /// <returns></returns>
        internal SendList SendRequest(IIPPacket.IIPPacketAction action)
        {
            var reply = new AsyncReply<object[]>();
            var c = callbackCounter++; // avoid thread racing
            requests.Add(c, reply);

            return (SendList)SendParams(reply).AddUInt8((byte)(0x40 | (byte)action)).AddUInt32(c);
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

        internal SendList SendReply(IIPPacket.IIPPacketAction action, uint callbackId)
        {
            /*
            if (callbackId > maxcallerid)
            {
                maxcallerid = callbackId;
            }
            else
            {
                Console.Beep();

            }
            */

            return (SendList)SendParams().AddUInt8((byte)(0x80 | (byte)action)).AddUInt32(callbackId);
        }

        internal SendList SendEvent(IIPPacket.IIPPacketEvent evt)
        {
            //var bl = new BinaryList((byte)(evt));
            //bl.AddRange(args);
            //Send(bl.ToArray());

            return (SendList)SendParams().AddUInt8((byte)(evt));
        }

        internal AsyncReply<object> SendInvokeByArrayArguments(uint instanceId, byte index, object[] parameters)
        {
            var pb = Codec.ComposeVarArray(parameters, this, true);

            var reply = new AsyncReply<object>();
            var c = callbackCounter++;
            requests.Add(c, reply);

            SendParams().AddUInt8((byte)(0x40 | (byte)Packets.IIPPacket.IIPPacketAction.InvokeFunctionArrayArguments))
                        .AddUInt32(c)
                        .AddUInt32(instanceId)
                        .AddUInt8(index)
                        .AddUInt8Array(pb)
                        .Done();

            //var bl = new BinaryList((byte)(0x40 | (byte)Packets.IIPPacket.IIPPacketAction.InvokeFunctionArrayArguments),
            //                        callbackCounter, instanceId, index, pb);
            //Send(bl.ToArray());

            return reply;
        }

        internal AsyncReply<object> SendInvokeByNamedArguments(uint instanceId, byte index, Structure parameters)
        {
            var pb = Codec.ComposeStructure(parameters, this, true, true, true);

            /*
            var reply = new AsyncReply<object>();
            callbackCounter++;
            var bl = new BinaryList((byte)(0x40 | (byte)Packets.IIPPacket.IIPPacketAction.InvokeFunctionNamedArguments),
                                    callbackCounter, instanceId, index, pb);
            Send(bl.ToArray());
            requests.Add(callbackCounter, reply);

            return reply;
            */

            var reply = new AsyncReply<object>();
            var c = callbackCounter++;
            requests.Add(c, reply);

            SendParams().AddUInt8((byte)(0x40 | (byte)Packets.IIPPacket.IIPPacketAction.InvokeFunctionNamedArguments))
                        .AddUInt32(c)
                        .AddUInt32(instanceId)
                        .AddUInt8(index)
                        .AddUInt8Array(pb)
                        .Done();
            return reply;
        }


        void SendError(ErrorType type, uint callbackId, ushort errorCode, string errorMessage = "")
        {
            var msg = DC.ToBytes(errorMessage);
            if (type == ErrorType.Management)
                SendParams()
                            .AddUInt8((byte)(0xC0 | (byte)IIPPacket.IIPPacketReport.ManagementError))
                            .AddUInt32(callbackId)
                            .AddUInt16(errorCode)
                            .Done();
            else if (type == ErrorType.Exception)
                SendParams()
                            .AddUInt8((byte)(0xC0 | (byte)IIPPacket.IIPPacketReport.ExecutionError))
                            .AddUInt32(callbackId)
                            .AddUInt16(errorCode)
                            .AddUInt16((ushort)msg.Length)
                            .AddUInt8Array(msg)
                            .Done();
        }

        void SendProgress(uint callbackId, int value, int max)
        {
            SendParams()
                .AddUInt8((byte)(0xC0 | (byte)IIPPacket.IIPPacketReport.ProgressReport))
                .AddUInt32(callbackId)
                .AddInt32(value)
                .AddInt32(max)
                .Done();
            //SendParams(, callbackId, value, max);
        }

        void SendChunk(uint callbackId, object chunk)
        {
            var c = Codec.Compose(chunk, this, true);
            SendParams()
                .AddUInt8((byte)(0xC0 | (byte)IIPPacket.IIPPacketReport.ChunkStream))
                .AddUInt32(callbackId)
                .AddUInt8Array(c)
                .Done();
        }

        void IIPReply(uint callbackId, params object[] results)
        {
            var req = requests.Take(callbackId);
            req?.Trigger(results);
        }

        void IIPReplyInvoke(uint callbackId, byte[] result)
        {
            var req = requests.Take(callbackId);

            Codec.Parse(result, 0, this).Then((rt) =>
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

        void IIPReportChunk(uint callbackId, byte[] data)
        {
            if (requests.ContainsKey(callbackId))
            {
                var req = requests[callbackId];
                Codec.Parse(data, 0, this).Then((x) =>
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
            if (resources.Contains(resourceId))
            {
                var r = resources[resourceId];
                resources.Remove(resourceId);
                r.Destroy();
            }
        }

        void IIPEventPropertyUpdated(uint resourceId, byte index, byte[] content)
        {

            Fetch(resourceId).Then(r =>
            {
                var item = new AsyncReply<DistributedResourceQueueItem>();
                queue.Add(item);

                Codec.Parse(content, 0, this).Then((arguments) =>
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


        void IIPEventEventOccurred(uint resourceId, byte index, byte[] content)
        {
            Fetch(resourceId).Then(r =>
            {
                // push to the queue to gaurantee serialization
                var item = new AsyncReply<DistributedResourceQueueItem>();
                queue.Add(item);

                Codec.ParseVarArray(content, this).Then((arguments) =>
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
            Fetch(resourceId).Then(parent =>
            {
                Fetch(childId).Then(child =>
                {
                    parent.children.Add(child);
                    child.parents.Add(parent);

                    //parent.Instance.Children.Add(child);
                });
            });
        }

        void IIPEventChildRemoved(uint resourceId, uint childId)
        {
            Fetch(resourceId).Then(parent =>
            {
                Fetch(childId).Then(child =>
                {
                    parent.children.Remove(child);
                    child.parents.Remove(parent);

//                    parent.Instance.Children.Remove(child);
                });
            });
        }

        void IIPEventRenamed(uint resourceId, byte[] name)
        {
            Fetch(resourceId).Then(resource =>
            {
                resource.Instance.Attributes["name"] = name.GetString(0, (uint)name.Length);
            });
        }


        void IIPEventAttributesUpdated(uint resourceId, byte[] attributes)
        {
            Fetch(resourceId).Then(resource =>
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
                    r.Instance.ResourceEventOccurred -= Instance_EventOccurred;
                    r.Instance.ResourceModified -= Instance_PropertyModified;
                    r.Instance.ResourceDestroyed -= Instance_ResourceDestroyed;
                   // r.Instance.Children.OnAdd -= Children_OnAdd;
                   // r.Instance.Children.OnRemoved -= Children_OnRemoved;
                    r.Instance.Attributes.OnModified -= Attributes_OnModified;

                    // subscribe
                    r.Instance.ResourceEventOccurred += Instance_EventOccurred;
                    r.Instance.ResourceModified += Instance_PropertyModified;
                    r.Instance.ResourceDestroyed += Instance_ResourceDestroyed;
                    //r.Instance.Children.OnAdd += Children_OnAdd;
                    //r.Instance.Children.OnRemoved += Children_OnRemoved;
                    r.Instance.Attributes.OnModified += Attributes_OnModified;

                    // add it to attached resources so GC won't remove it from memory
                    attachedResources.Add(r);

                    var link = DC.ToBytes(r.Instance.Link);

                    if (r is DistributedResource)
                    {
                        // reply ok
                        SendReply(IIPPacket.IIPPacketAction.AttachResource, callback)
                                .AddGuid(r.Instance.Template.ClassId)
                                .AddUInt64(r.Instance.Age)
                                .AddUInt16((ushort)link.Length)
                                .AddUInt8Array(link)
                                .AddUInt8Array(Codec.ComposePropertyValueArray((r as DistributedResource)._Serialize(), this, true))
                                .Done();
                    }
                    else
                    {
                        // reply ok
                        SendReply(IIPPacket.IIPPacketAction.AttachResource, callback)
                                .AddGuid(r.Instance.Template.ClassId)
                                .AddUInt64(r.Instance.Age)
                                .AddUInt16((ushort)link.Length)
                                .AddUInt8Array(link)
                                .AddUInt8Array(Codec.ComposePropertyValueArray(r.Instance.Serialize(), this, true))
                                .Done();
                    }
                }
                else
                {
                    // reply failed
                    //SendParams(0x80, r.Instance.Id, r.Instance.Age, r.Instance.Serialize(false, this));
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
                SendEvent(IIPPacket.IIPPacketEvent.ChildRemoved)
                        .AddUInt32(instance.Id)
                        .AddUInt16((ushort)name.Length)
                        .AddUInt8Array(name)
                        .Done();
            }
        }

        private void Children_OnRemoved(Instance sender, IResource value)
        {
            SendEvent(IIPPacket.IIPPacketEvent.ChildRemoved)
                .AddUInt32(sender.Id)
                .AddUInt32(value.Instance.Id)
                .Done();
        }

        private void Children_OnAdd(Instance sender, IResource value)
        {
            //if (sender.Applicable(sender.Resource, this.session, ActionType.))
            SendEvent(IIPPacket.IIPPacketEvent.ChildAdded)
                .AddUInt32(sender.Id)
                .AddUInt32(value.Instance.Id)
                .Done();
        }


        public bool RemoveChild(IResource parent, IResource child)
        {
            SendEvent(IIPPacket.IIPPacketEvent.ChildRemoved)
                .AddUInt32((parent as DistributedResource).Id)
                .AddUInt32((child as DistributedResource).Id)
                .Done();

            return true;
        }

        public bool AddChild(IResource parent, IResource child)
        {
            SendEvent(IIPPacket.IIPPacketEvent.ChildAdded)
                .AddUInt32((parent as DistributedResource).Id)
                .AddUInt32((child as DistributedResource).Id)
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
                    r.Instance.ResourceEventOccurred -= Instance_EventOccurred;
                    r.Instance.ResourceModified -= Instance_PropertyModified;
                    r.Instance.ResourceDestroyed -= Instance_ResourceDestroyed;
                    //r.Instance.Children.OnAdd -= Children_OnAdd;
                    //r.Instance.Children.OnRemoved -= Children_OnRemoved;
                    r.Instance.Attributes.OnModified -= Attributes_OnModified;

                    // subscribe
                    r.Instance.ResourceEventOccurred += Instance_EventOccurred;
                    r.Instance.ResourceModified += Instance_PropertyModified;
                    r.Instance.ResourceDestroyed += Instance_ResourceDestroyed;
                    //r.Instance.Children.OnAdd += Children_OnAdd;
                    //r.Instance.Children.OnRemoved += Children_OnRemoved;
                    r.Instance.Attributes.OnModified += Attributes_OnModified;

                    // reply ok
                    SendReply(IIPPacket.IIPPacketAction.ReattachResource, callback)
                                .AddUInt64(r.Instance.Age)
                                .AddUInt8Array(Codec.ComposePropertyValueArray(r.Instance.Serialize(), this, true))
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
                    var r = res as IResource;
                    r.Instance.ResourceEventOccurred -= Instance_EventOccurred;
                    r.Instance.ResourceModified -= Instance_PropertyModified;
                    r.Instance.ResourceDestroyed -= Instance_ResourceDestroyed;

                    // remove from attached resources
                    attachedResources.Remove(res);

                    // reply ok
                    SendReply(IIPPacket.IIPPacketAction.DetachResource, callback).Done();
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

                    var nameLength = content.GetUInt16(offset);
                    offset += 2;
                    var name = content.GetString(offset, nameLength);

                    var cl = content.GetUInt32(offset);
                    offset += 4;

                    var type = Type.GetType(className);

                    if (type == null)
                    {
                        SendError(ErrorType.Management, callback, (ushort)ExceptionCode.ClassNotFound);
                        return;
                    }

                    Codec.ParseVarArray(content, offset, cl, this).Then(parameters =>
                    {
                        offset += cl;
                        cl = content.GetUInt32(offset);
                        Codec.ParseStructure(content, offset, cl, this).Then(attributes =>
                        {
                            offset += cl;
                            cl = (uint)content.Length - offset;

                            Codec.ParseStructure(content, offset, cl, this).Then(values =>
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

                                Warehouse.Put(resource, name, store as IStore, parent);

                                SendReply(IIPPacket.IIPPacketAction.CreateResource, callback)
                                           .AddUInt32(resource.Instance.Id)
                                           .Done();

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
                    SendReply(IIPPacket.IIPPacketAction.DeleteResource, callback).Done();
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
                    SendReply(all ? IIPPacket.IIPPacketAction.GetAllAttributes : IIPPacket.IIPPacketAction.GetAttributes, callback)
                              .AddUInt8Array(Codec.ComposeStructure(st, this, true, true, true))
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

                    SendReply(IIPPacket.IIPPacketAction.AddChild, callback).Done();
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

                    SendReply(IIPPacket.IIPPacketAction.RemoveChild, callback).Done();
                    //child.Instance.Parents
                });

            });
        }

        void IIPRequestRenameResource(uint callback, uint resourceId, byte[] name)
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


                resource.Instance.Name = name.GetString(0, (uint)name.Length);
                SendReply(IIPPacket.IIPPacketAction.RenameResource, callback).Done();
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
                    SendReply(IIPPacket.IIPPacketAction.ResourceChildren, callback)
                        .AddUInt8Array(Codec.ComposeResourceArray(children, this, true))
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
                    SendReply(IIPPacket.IIPPacketAction.ResourceParents, callback)
                    .AddUInt8Array(Codec.ComposeResourceArray(parents, this, true))
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
                    SendReply(all ? IIPPacket.IIPPacketAction.ClearAllAttributes : IIPPacket.IIPPacketAction.ClearAttributes, callback).Done();
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

                Codec.ParseStructure(attributes, 0, (uint)attributes.Length, this).Then(attrs =>
                {
                    if (r.Instance.SetAttributes(attrs, clearAttributes))
                        SendReply(clearAttributes ? IIPPacket.IIPPacketAction.ClearAllAttributes : IIPPacket.IIPPacketAction.ClearAttributes,
                                  callback).Done();
                    else
                        SendError(ErrorType.Management, callback, (ushort)ExceptionCode.UpdateAttributeFailed);
                });

            });

        }

        void IIPRequestTemplateFromClassName(uint callback, string className)
        {
            Warehouse.GetTemplate(className).Then((t) =>
            {
                if (t != null)
                    SendReply(IIPPacket.IIPPacketAction.TemplateFromClassName, callback)
                            .AddInt32(t.Content.Length)
                            .AddUInt8Array(t.Content)
                            .Done();
                else
                {
                    // reply failed
                    SendError(ErrorType.Management, callback, (ushort)ExceptionCode.TemplateNotFound);
                }
            });
        }

        void IIPRequestTemplateFromClassId(uint callback, Guid classId)
        {
            Warehouse.GetTemplate(classId).Then((t) =>
            {
                if (t != null)
                    SendReply(IIPPacket.IIPPacketAction.TemplateFromClassId, callback)
                            .AddInt32(t.Content.Length)
                            .AddUInt8Array(t.Content)
                            .Done();
                else
                {
                    // reply failed
                    SendError(ErrorType.Management, callback, (ushort)ExceptionCode.TemplateNotFound);
                }
            });
        }



        void IIPRequestTemplateFromResourceId(uint callback, uint resourceId)
        {
            Warehouse.GetById(resourceId).Then((r) =>
            {
                if (r != null)
                    SendReply(IIPPacket.IIPPacketAction.TemplateFromResourceId, callback)
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
                        SendReply(IIPPacket.IIPPacketAction.QueryLink, callback)
                                    .AddUInt8Array(Codec.ComposeResourceArray(list, this, true))
                                    .Done();
                }
            };

            if (Server?.EntryPoint != null)
                Server.EntryPoint.Query(resourceLink, this).Then(queryCallback);
            else
                Warehouse.Query(resourceLink).Then(x => queryCallback(x));
        }

        void IIPRequestResourceAttribute(uint callback, uint resourceId)
        {

        }

        void IIPRequestInvokeFunctionArrayArguments(uint callback, uint resourceId, byte index, byte[] content)
        {
            //Console.WriteLine("IIPRequestInvokeFunction " + callback + " " + resourceId + "  " + index);

            Warehouse.GetById(resourceId).Then((r) =>
            {
                if (r != null)
                {
                    Codec.ParseVarArray(content, this).Then((arguments) =>
                    {
                        var ft = r.Instance.Template.GetFunctionTemplateByIndex(index);
                        if (ft != null)
                        {

                            // un hold the socket to send data immediately
                            this.Socket.Unhold();

                            if (r is DistributedResource)
                            {
                                var rt = (r as DistributedResource)._InvokeByArrayArguments(index, arguments);
                                if (rt != null)
                                {
                                    rt.Then(res =>
                                    {
                                        SendReply(IIPPacket.IIPPacketAction.InvokeFunctionArrayArguments, callback)
                                                    .AddUInt8Array(Codec.Compose(res, this))
                                                    .Done();
                                    });
                                }
                                else
                                {

                                    // function not found on a distributed object
                                }
                            }
                            else
                            {
#if NETSTANDARD
                                var fi = r.GetType().GetTypeInfo().GetMethod(ft.Name);
#else
                                var fi = r.GetType().GetMethod(ft.Name);
#endif

                                if (fi != null)
                                {
                                    if (r.Instance.Applicable(session, ActionType.Execute, ft) == Ruling.Denied)
                                    {
                                        SendError(ErrorType.Management, callback,
                                            (ushort)ExceptionCode.InvokeDenied);
                                        return;
                                    }

                                    // cast arguments
                                    ParameterInfo[] pi = fi.GetParameters();
                                    object[] args = null;

                                    if (pi.Length > 0)
                                    {
                                        int argsCount = pi.Length;
                                        args = new object[pi.Length];

                                        if (pi[pi.Length - 1].ParameterType == typeof(DistributedConnection))
                                        {
                                            args[--argsCount] = this;
                                        }

                                        if (arguments != null)
                                        {
                                            for (int i = 0; i < argsCount && i < arguments.Length; i++)
                                            {
                                                args[i] = DC.CastConvert(arguments[i], pi[i].ParameterType);
                                            }
                                        }
                                    }

                                    object rt;

                                    try
                                    {
                                        rt = fi.Invoke(r, args);
                                    }
                                    catch (Exception ex)
                                    {
                                        SendError(ErrorType.Exception, callback, 0, 
                                            ex.InnerException != null ? ex.InnerException.ToString() : ex.ToString());
                                        return;
                                    }

                                    if (rt is System.Collections.IEnumerable && !(rt is Array || rt is Structure))
                                    {
                                        var enu = rt as System.Collections.IEnumerable;

                                        try
                                        {
                                            foreach (var v in enu)
                                                SendChunk(callback, v);
                                            SendReply(IIPPacket.IIPPacketAction.InvokeFunctionArrayArguments, callback)
                                            .AddUInt8((byte)DataType.Void)
                                            .Done();
                                        }
                                        catch(Exception ex)
                                        {
                                            SendError(ErrorType.Exception, callback, 0, ex.ToString());
                                        }

                                    }
                                    else if (rt is Task)
                                    {
                                        (rt as Task).ContinueWith(t =>
                                        {
#if NETSTANDARD
                                            var res = t.GetType().GetTypeInfo().GetProperty("Result").GetValue(t);
#else
                                            var res = t.GetType().GetProperty("Result").GetValue(t);
#endif
                                            SendReply(IIPPacket.IIPPacketAction.InvokeFunctionArrayArguments, callback)
                                                     .AddUInt8Array(Codec.Compose(res, this))
                                                     .Done();
                                        });

                                        //await t;
                                        //SendParams((byte)0x90, callback, Codec.Compose(res, this));
                                    }
                                    else if (rt is AsyncReply)// Codec.ImplementsInterface(rt.GetType(), typeof(IAsyncReply<>)))// rt.GetType().GetTypeInfo().IsGenericType
                                          //&& rt.GetType().GetGenericTypeDefinition() == typeof(IAsyncReply<>))
                                    {
                                        (rt as AsyncReply).Then(res =>
                                        {
                                            SendReply(IIPPacket.IIPPacketAction.InvokeFunctionArrayArguments, callback)
                                                        .AddUInt8Array(Codec.Compose(res, this))
                                                        .Done();
                                        }).Error(ex =>
                                        {
                                            SendError(ErrorType.Exception, callback, (ushort)ex.Code, ex.Message);
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
                                        SendReply(IIPPacket.IIPPacketAction.InvokeFunctionArrayArguments, callback)
                                                .AddUInt8Array(Codec.Compose(rt, this))
                                                .Done();
                                    }
                                }
                                else
                                {
                                    // ft found, fi not found, this should never happen
                                }
                            }
                        }
                        else
                        {
                            // no function at this index
                        }
                    });
                }
                else
                {
                    // no resource with this id
                }
            });
        }


        void IIPRequestInvokeFunctionNamedArguments(uint callback, uint resourceId, byte index, byte[] content)
        {

            Warehouse.GetById(resourceId).Then((r) =>
            {
                if (r != null)
                {
                    Codec.ParseStructure(content, 0, (uint)content.Length, this).Then((namedArgs) =>
                     {
                         var ft = r.Instance.Template.GetFunctionTemplateByIndex(index);
                         if (ft != null)
                         {
                             // un hold the socket to send data immediately
                             this.Socket.Unhold();

                             if (r is DistributedResource)
                             {
                                 var rt = (r as DistributedResource)._InvokeByNamedArguments(index, namedArgs);
                                 if (rt != null)
                                 {
                                     rt.Then(res =>
                                     {
                                         SendReply(IIPPacket.IIPPacketAction.InvokeFunctionNamedArguments, callback)
                                                 .AddUInt8Array(Codec.Compose(res, this))
                                                 .Done();
                                     });
                                 }
                                 else
                                 {

                                    // function not found on a distributed object
                                }
                             }
                             else
                             {
#if NETSTANDARD
                                var fi = r.GetType().GetTypeInfo().GetMethod(ft.Name);
#else
                                var fi = r.GetType().GetMethod(ft.Name);
#endif

                                if (fi != null)
                                 {
                                     if (r.Instance.Applicable(session, ActionType.Execute, ft) == Ruling.Denied)
                                     {
                                         SendError(ErrorType.Management, callback,
                                             (ushort)ExceptionCode.InvokeDenied);
                                         return;
                                     }

                                    // cast arguments
                                    ParameterInfo[] pi = fi.GetParameters();

                                     object[] args = new object[pi.Length];

                                     for (var i = 0; i < pi.Length; i++)
                                     {
                                         if (pi[i].ParameterType == typeof(DistributedConnection))
                                         {
                                             args[i] = this;
                                         }
                                         else if (namedArgs.ContainsKey(pi[i].Name))
                                         {
                                             args[i] = DC.CastConvert(namedArgs[pi[i].Name], pi[i].ParameterType);
                                         }
                                     }


                                     object rt;

                                     try
                                     {
                                         rt = fi.Invoke(r, args);
                                     }
                                     catch (Exception ex)
                                     {
                                         SendError(ErrorType.Exception, callback, 0, ex.ToString());
                                         return;
                                     }

                                     if (rt is System.Collections.IEnumerable && !(rt is Array))
                                     {
                                         var enu = rt as System.Collections.IEnumerable;

                                         try
                                         {
                                             foreach (var v in enu)
                                                 SendChunk(callback, v);

                                             SendReply(IIPPacket.IIPPacketAction.InvokeFunctionNamedArguments, callback)
                                                     .AddUInt8((byte)DataType.Void)
                                                     .Done();
                                         }
                                         catch (Exception ex)
                                         {
                                             SendError(ErrorType.Exception, callback, 0, ex.ToString());
                                         }
                                     }
                                     else if (rt is Task)
                                     {
                                         (rt as Task).ContinueWith(t =>
                                         {
#if NETSTANDARD
                                            var res = t.GetType().GetTypeInfo().GetProperty("Result").GetValue(t);
#else
                                            var res = t.GetType().GetProperty("Result").GetValue(t);
#endif
                                            SendReply(IIPPacket.IIPPacketAction.InvokeFunctionNamedArguments, callback)
                                                 .AddUInt8Array(Codec.Compose(res, this))
                                                 .Done();
                                         });

                                     }
                                    else if (rt is AsyncReply)
                                     {
                                         (rt as AsyncReply).Then(res =>
                                {
                                             SendReply(IIPPacket.IIPPacketAction.InvokeFunctionNamedArguments, callback)
                                                         .AddUInt8Array(Codec.Compose(res, this))
                                                         .Done();

                                         }).Error(ex =>
                                         {
                                             SendError(ErrorType.Exception, callback, (ushort)ex.Code, ex.Message);
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
                                         SendReply(IIPPacket.IIPPacketAction.InvokeFunctionNamedArguments, callback)
                                                 .AddUInt8Array(Codec.Compose(rt, this))
                                                 .Done();
                                     }
                                 }
                                 else
                                 {
                                    // ft found, fi not found, this should never happen
                                }
                             }
                         }
                         else
                         {
                            // no function at this index
                        }
                     });
                }
                else
                {
                    // no resource with this id
                }
            });
        }

        void IIPRequestGetProperty(uint callback, uint resourceId, byte index)
        {
            Warehouse.GetById(resourceId).Then((r) =>
            {
                if (r != null)
                {
                    var pt = r.Instance.Template.GetFunctionTemplateByIndex(index);
                    if (pt != null)
                    {
                        if (r is DistributedResource)
                        {
                            SendReply(IIPPacket.IIPPacketAction.GetProperty, callback)
                                        .AddUInt8Array(Codec.Compose((r as DistributedResource)._Get(pt.Index), this))
                                        .Done();
                        }
                        else
                        {
#if NETSTANDARD
                            var pi = r.GetType().GetTypeInfo().GetProperty(pt.Name);
#else
                            var pi = r.GetType().GetProperty(pt.Name);
#endif

                            if (pi != null)
                            {
                                SendReply(IIPPacket.IIPPacketAction.GetProperty, callback)
                                            .AddUInt8Array(Codec.Compose(pi.GetValue(r), this))
                                            .Done();
                            }
                            else
                            {
                                // pt found, pi not found, this should never happen
                            }
                        }
                    }
                    else
                    {
                        // pt not found
                    }
                }
                else
                {
                    // resource not found
                }
            });
        }

        void IIPRequestInquireResourceHistory(uint callback, uint resourceId, DateTime fromDate, DateTime toDate)
        {
            Warehouse.GetById(resourceId).Then((r) =>
            {
                if (r != null)
                {
                    r.Instance.Store.GetRecord(r, fromDate, toDate).Then((results) =>
                    {
                        var history = Codec.ComposeHistory(results, this, true);

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

                        SendReply(IIPPacket.IIPPacketAction.ResourceHistory, callback)
                                .AddUInt8Array(history)
                                .Done();

                    });
                }
            });
        }

        void IIPRequestGetPropertyIfModifiedSince(uint callback, uint resourceId, byte index, ulong age)
        {
            Warehouse.GetById(resourceId).Then((r) =>
            {
                if (r != null)
                {
                    var pt = r.Instance.Template.GetFunctionTemplateByIndex(index);
                    if (pt != null)
                    {
                        if (r.Instance.GetAge(index) > age)
                        {
#if NETSTANDARD
                            var pi = r.GetType().GetTypeInfo().GetProperty(pt.Name);
#else
                            var pi = r.GetType().GetProperty(pt.Name);
#endif
                            if (pi != null)
                            {
                                SendReply(IIPPacket.IIPPacketAction.GetPropertyIfModified, callback)
                                            .AddUInt8Array(Codec.Compose(pi.GetValue(r), this))
                                            .Done();
                            }
                            else
                            {
                                // pt found, pi not found, this should never happen
                            }
                        }
                        else
                        {
                            SendReply(IIPPacket.IIPPacketAction.GetPropertyIfModified, callback)
                                    .AddUInt8((byte)DataType.NotModified)
                                    .Done();
                        }
                    }
                    else
                    {
                        // pt not found
                    }
                }
                else
                {
                    // resource not found
                }
            });
        }

        void IIPRequestSetProperty(uint callback, uint resourceId, byte index, byte[] content)
        {
            Warehouse.GetById(resourceId).Then((r) =>
            {
                if (r != null)
                {


                    var pt = r.Instance.Template.GetPropertyTemplateByIndex(index);
                    if (pt != null)
                    {
                        Codec.Parse(content, 0, this).Then((value) =>
                        {
                            if (r is DistributedResource)
                            {
                                // propagation
                                (r as DistributedResource)._Set(index, value).Then((x) =>
                                {
                                    SendReply(IIPPacket.IIPPacketAction.SetProperty, callback).Done();
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

                                var pi = pt.Info;

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


                                    if (pi.PropertyType == typeof(DistributedPropertyContext))
                                    {
                                        value = new DistributedPropertyContext(this, value);
                                    }
                                    else
                                    {
                                        // cast new value type to property type
                                        value = DC.CastConvert(value, pi.PropertyType);
                                    }


                                    try
                                    {
                                        pi.SetValue(r, value);
                                        SendReply(IIPPacket.IIPPacketAction.SetProperty, callback).Done();
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
        public AsyncReply<ResourceTemplate> GetTemplate(Guid classId)
        {
            if (templates.ContainsKey(classId))
                return new AsyncReply<ResourceTemplate>(templates[classId]);
            else if (templateRequests.ContainsKey(classId))
                return templateRequests[classId];

            var reply = new AsyncReply<ResourceTemplate>();
            templateRequests.Add(classId, reply);

            SendRequest(IIPPacket.IIPPacketAction.TemplateFromClassId)
                        .AddGuid(classId)
                        .Done()
                        .Then((rt) =>
                        {
                            templateRequests.Remove(classId);
                            templates.Add(((ResourceTemplate)rt[0]).ClassId, (ResourceTemplate)rt[0]);
                            Warehouse.PutTemplate(rt[0] as ResourceTemplate);
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

        /// <summary>
        /// Retrive a resource by its instance Id.
        /// </summary>
        /// <param name="iid">Instance Id</param>
        /// <returns>Resource</returns>
        public AsyncReply<IResource> Retrieve(uint iid)
        {
            foreach (var r in resources.Values)
                if (r.Instance.Id == iid)
                    return new AsyncReply<IResource>(r);
            return new AsyncReply<IResource>(null);
        }

        /// <summary>
        /// Fetch a resource from the other end
        /// </summary>
        /// <param name="classId">Class GUID</param>
        /// <param name="id">Resource Id</param>Guid classId
        /// <returns>DistributedResource</returns>
        public AsyncReply<DistributedResource> Fetch(uint id)
        {
            if (resourceRequests.ContainsKey(id) && resources.ContainsKey(id))
            {
                Console.WriteLine("DEAD LOCK " + id);

                return new AsyncReply<DistributedResource>(resources[id]);
                // dig for dead locks
                return resourceRequests[id];
            }
            else if (resourceRequests.ContainsKey(id))
                return resourceRequests[id];
            else if (resources.ContainsKey(id))
                return new AsyncReply<DistributedResource>(resources[id]);

            var reply = new AsyncReply<DistributedResource>();
            resourceRequests.Add(id, reply);

            SendRequest(IIPPacket.IIPPacketAction.AttachResource)
                        .AddUInt32(id)
                        .Done()
                        .Then((rt) =>
                        {
                            var dr = new DistributedResource(this, id, (ulong)rt[1], (string)rt[2]);

                            GetTemplate((Guid)rt[0]).Then((tmp) =>
                            {
                                // ClassId, ResourceAge, ResourceLink, Content
                                Warehouse.Put(dr, id.ToString(), this, null, tmp);

                                Codec.ParsePropertyValueArray((byte[])rt[3], this).Then((ar) =>
                                {
                                    dr._Attached(ar);
                                    resourceRequests.Remove(id);
                                    reply.Trigger(dr);
                                });
                            }).Error((ex) =>
                            {
                                reply.TriggerError(ex);
                            });
                        }).Error((ex) =>
                        {
                            reply.TriggerError(ex);
                        });

            return reply;
        }


        public AsyncReply<IResource[]> GetChildren(IResource resource)
        {
            var rt = new AsyncReply<IResource[]>();

            SendRequest(IIPPacket.IIPPacketAction.ResourceChildren)
                        .AddUInt32(resource.Instance.Id)
                        .Done()
                        .Then(ar =>
                        {
                            var d = (byte[])ar[0];
                            Codec.ParseResourceArray(d, 0, (uint)d.Length, this).Then(resources =>
                            {
                                rt.Trigger(resources);
                            }).Error(ex => rt.TriggerError(ex));
                        });

            return rt;
        }

        public AsyncReply<IResource[]> GetParents(IResource resource)
        {
            var rt = new AsyncReply<IResource[]>();

            SendRequest(IIPPacket.IIPPacketAction.ResourceParents)
                .AddUInt32(resource.Instance.Id)
                .Done()
                .Then(ar =>
                {
                    var d = (byte[])ar[0];
                    Codec.ParseResourceArray(d, 0, (uint)d.Length, this).Then(resources =>
                    {
                        rt.Trigger(resources);
                    }).Error(ex => rt.TriggerError(ex));
                });

            return rt;
        }

        public AsyncReply<bool> RemoveAttributes(IResource resource, string[] attributes = null)
        {
            var rt = new AsyncReply<bool>();

            if (attributes == null)
                SendRequest(IIPPacket.IIPPacketAction.ClearAllAttributes)
                    .AddUInt32(resource.Instance.Id)
                    .Done()
                    .Then(ar => rt.Trigger(true))
                    .Error(ex => rt.TriggerError(ex));
            else
            {
                var attrs = DC.ToBytes(attributes);
                SendRequest(IIPPacket.IIPPacketAction.ClearAttributes)
                    .AddUInt32(resource.Instance.Id)
                    .AddInt32(attrs.Length)
                    .AddUInt8Array(attrs)
                    .Done()
                    .Then(ar => rt.Trigger(true))
                    .Error(ex => rt.TriggerError(ex));
            }

            return rt;
        }

        public AsyncReply<bool> SetAttributes(IResource resource, Structure attributes, bool clearAttributes = false)
        {
            var rt = new AsyncReply<bool>();

            SendRequest(clearAttributes ? IIPPacket.IIPPacketAction.UpdateAllAttributes : IIPPacket.IIPPacketAction.UpdateAttributes)
                .AddUInt32(resource.Instance.Id)
                .AddUInt8Array(Codec.ComposeStructure(attributes, this, true, true, true))
                .Done()
                .Then(ar => rt.Trigger(true))
                .Error(ex => rt.TriggerError(ex));

            return rt;
        }

        public AsyncReply<Structure> GetAttributes(IResource resource, string[] attributes = null)
        {
            var rt = new AsyncReply<Structure>();

            if (attributes == null)
            {
                SendRequest(IIPPacket.IIPPacketAction.GetAllAttributes)
                    .AddUInt32(resource.Instance.Id)
                    .Done()
                    .Then(ar =>
                    {
                        var d = ar[0] as byte[];
                        Codec.ParseStructure(d, 0, (uint)d.Length, this).Then(st =>
                        {

                            resource.Instance.SetAttributes(st);

                            rt.Trigger(st);
                        }).Error(ex => rt.TriggerError(ex));
                    });
            }
            else
            {
                var attrs = DC.ToBytes(attributes);
                SendRequest(IIPPacket.IIPPacketAction.GetAttributes)
                    .AddUInt32(resource.Instance.Id)
                    .AddInt32(attrs.Length)
                    .AddUInt8Array(attrs)
                    .Done()
                    .Then(ar =>
                    {
                        var d = ar[0] as byte[];
                        Codec.ParseStructure(d, 0, (uint)d.Length, this).Then(st =>
                        {

                            resource.Instance.SetAttributes(st);

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

                if (dr.Connection != this)
                    return new AsyncReply<KeyList<PropertyTemplate, PropertyValue[]>>(null);

                var reply = new AsyncReply<KeyList<PropertyTemplate, PropertyValue[]>>();

                SendRequest(IIPPacket.IIPPacketAction.ResourceHistory)
                    .AddUInt32(dr.Id)
                    .AddDateTime(fromDate)
                    .AddDateTime(toDate)
                    .Done()
                    .Then(rt =>
                    {
                        var content = (byte[])rt[0];

                        Codec.ParseHistory(content, 0, (uint)content.Length, resource, this)
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

            SendRequest(IIPPacket.IIPPacketAction.QueryLink)
                        .AddUInt16((ushort)str.Length)
                        .AddUInt8Array(str)
                        .Done()
                        .Then(args =>
                        {
                            var content = args[0] as byte[];

                            Codec.ParseResourceArray(content, 0, (uint)content.Length, this)
                                                    .Then(resources => reply.Trigger(resources));

                        }).Error(ex=>reply.TriggerError(ex));

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
        public AsyncReply<DistributedResource> Create(IStore store, IResource parent, string className, object[] parameters, Structure attributes, Structure values)
        {
            var reply = new AsyncReply<DistributedResource>();
            var pkt = new BinaryList()
                                    .AddUInt32(store.Instance.Id)
                                    .AddUInt32(parent.Instance.Id)
                                    .AddUInt8((byte)className.Length)
                                    .AddString(className)
                                    .AddUInt8Array(Codec.ComposeVarArray(parameters, this, true))
                                    .AddUInt8Array(Codec.ComposeStructure(attributes, this, true, true, true))
                                    .AddUInt8Array(Codec.ComposeStructure(values, this));

            pkt.InsertInt32(8, pkt.Length);

            SendRequest(IIPPacket.IIPPacketAction.CreateResource)
                .AddUInt8Array(pkt.ToArray())
                .Done()
                .Then(args =>
                {
                    var rid = (uint)args[0];

                    Fetch(rid).Then((r) =>
                    {
                        reply.Trigger(r);
                    });

                });

            return reply;
        }

        private void Instance_ResourceDestroyed(IResource resource)
        {
            // compose the packet
            SendEvent(IIPPacket.IIPPacketEvent.ResourceDestroyed)
                        .AddUInt32(resource.Instance.Id)
                        .Done();
        }

        private void Instance_PropertyModified(IResource resource, string name, object newValue)
        {
            var pt = resource.Instance.Template.GetPropertyTemplateByName(name);

            if (pt == null)
                return;

            SendEvent(IIPPacket.IIPPacketEvent.PropertyUpdated)
                        .AddUInt32(resource.Instance.Id)
                        .AddUInt8(pt.Index)
                        .AddUInt8Array(Codec.Compose(newValue, this))
                        .Done();

        }

        //        private void Instance_EventOccurred(IResource resource, string name, string[] users, DistributedConnection[] connections, object[] args)

        private void Instance_EventOccurred(IResource resource, object issuer, Session[] receivers, string name, object[] args)
        {
            var et = resource.Instance.Template.GetEventTemplateByName(name);

            if (et == null)
                return;

            /*
            if (users != null)
                if (!users.Contains(RemoteUsername))
                    return;

            if (connections != null)
                if (!connections.Contains(this))
                    return;
            */

            if (receivers != null)
                if (!receivers.Contains(this.session))
                    return;

            if (resource.Instance.Applicable(this.session, ActionType.ReceiveEvent, et, issuer) == Ruling.Denied)
                return;

            // compose the packet
            SendEvent(IIPPacket.IIPPacketEvent.EventOccurred)
                        .AddUInt32(resource.Instance.Id)
                        .AddUInt8((byte)et.Index)
                        .AddUInt8Array(Codec.ComposeVarArray(args, this, true))
                        .Done();
        }
    }
}
