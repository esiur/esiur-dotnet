using Esiur.Data;
using Esiur.Engine;
using Esiur.Net.Packets;
using Esiur.Resource;
using Esiur.Resource.Template;
using System;
using System.Collections.Generic;

using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Net.IIP
{
    partial class DistributedConnection
    {
        KeyList<uint, DistributedResource> resources = new KeyList<uint, DistributedResource>();
        KeyList<uint, AsyncReply<DistributedResource>> resourceRequests = new KeyList<uint, AsyncReply<DistributedResource>>();
        KeyList<Guid, AsyncReply<ResourceTemplate>> templateRequests = new KeyList<Guid, AsyncReply<ResourceTemplate>>();


        KeyList<string, AsyncReply<IResource>> pathRequests = new KeyList<string, AsyncReply<IResource>>();

        Dictionary<Guid, ResourceTemplate> templates = new Dictionary<Guid, ResourceTemplate>();

        KeyList<uint, AsyncReply<object[]>> requests = new KeyList<uint, AsyncReply<object[]>>();

        uint callbackCounter = 0;

        AsyncQueue<DistributedResourceQueueItem> queue = new AsyncQueue<DistributedResourceQueueItem>();

        /// <summary>
        /// Send IIP request.
        /// </summary>
        /// <param name="action">Packet action.</param>
        /// <param name="args">Arguments to send.</param>
        /// <returns></returns>
        internal AsyncReply<object[]> SendRequest(IIPPacket.IIPPacketAction action, params object[] args)
        {
            var reply = new AsyncReply<object[]>();
            callbackCounter++;
            var bl = new BinaryList((byte)(0x40 | (byte)action), callbackCounter);
            bl.AddRange(args);
            Send(bl.ToArray());
            requests.Add(callbackCounter, reply);
            return reply;
        }

        void IIPReply(uint callbackId, params object[] results)
        {
            var req = requests.Take(callbackId);
            req?.Trigger(results);
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
            if (resources.Contains(resourceId))
            {
                // push to the queue to gaurantee serialization
                var reply = new AsyncReply<DistributedResourceQueueItem>();
                queue.Add(reply);

                var r = resources[resourceId];
                Codec.Parse(content, 0, this).Then((arguments) =>
                {
                    var pt = r.Template.GetPropertyTemplate(index);
                    if (pt != null)
                    {
                        reply.Trigger(new DistributedResourceQueueItem((DistributedResource)r, DistributedResourceQueueItem.DistributedResourceQueueItemType.Propery, arguments, index));
                    }
                    else
                    {    // ft found, fi not found, this should never happen
                            queue.Remove(reply);
                    }
                });
            }
        }


        void IIPEventEventOccured(uint resourceId, byte index, byte[] content)
        {
            if (resources.Contains(resourceId))
            {
                // push to the queue to gaurantee serialization
                var reply = new AsyncReply<DistributedResourceQueueItem>();
                var r = resources[resourceId];

                queue.Add(reply);

                Codec.ParseVarArray(content, this).Then((arguments) =>
                {
                    var et = r.Template.GetEventTemplate(index);
                    if (et != null)
                    {
                        reply.Trigger(new DistributedResourceQueueItem((DistributedResource)r, DistributedResourceQueueItem.DistributedResourceQueueItemType.Event, arguments, index));
                    }
                    else
                    {    // ft found, fi not found, this should never happen
                                queue.Remove(reply);
                    }
                });
            }
        }

        void IIPRequestAttachResource(uint callback, uint resourceId)
        {
            Warehouse.Get(resourceId).Then((res) =>
            {
                if (res != null)
                {
                    var r = res as IResource;
                    r.Instance.ResourceEventOccured += Instance_EventOccured;
                    r.Instance.ResourceModified += Instance_PropertyModified;
                    r.Instance.ResourceDestroyed += Instance_ResourceDestroyed;

                    var link = DC.ToBytes(r.Instance.Link);

                    // reply ok
                    SendParams((byte)0x80, callback, r.Instance.Template.ClassId, r.Instance.Age, (ushort)link.Length, link, Codec.ComposeVarArray(r.Instance.Serialize(), this, true));
                }
                else
                {
                    // reply failed
                    //SendParams(0x80, r.Instance.Id, r.Instance.Age, r.Instance.Serialize(false, this));

                }
            });
        }

        void IIPRequestReattachResource(uint callback, uint resourceId, uint resourceAge)
        {
            Warehouse.Get(resourceId).Then((res) =>
            {
                if (res != null)
                {
                    var r = res as IResource;
                    r.Instance.ResourceEventOccured += Instance_EventOccured;
                    r.Instance.ResourceModified += Instance_PropertyModified;
                    r.Instance.ResourceDestroyed += Instance_ResourceDestroyed;
                    // reply ok
                    SendParams((byte)0x81, callback, r.Instance.Age, Codec.ComposeVarArray(r.Instance.Serialize(), this, true));
                }
                else
                {
                    // reply failed
                }
            });
        }

        void IIPRequestDetachResource(uint callback, uint resourceId)
        {
            Warehouse.Get(resourceId).Then((res) =>
            {
                if (res != null)
                {
                    var r = res as IResource;
                    r.Instance.ResourceEventOccured -= Instance_EventOccured;
                    r.Instance.ResourceModified -= Instance_PropertyModified;
                    r.Instance.ResourceDestroyed -= Instance_ResourceDestroyed;
                    // reply ok
                    SendParams((byte)0x82, callback);
                }
                else
                {
                    // reply failed
                }
            });
        }

        void IIPRequestCreateResource(uint callback, string className)
        {
            // not implemented
        }

        void IIPRequestDeleteResource(uint callback, uint resourceId)
        {
            // not implemented

        }

        void IIPRequestTemplateFromClassName(uint callback, string className)
        {
            Warehouse.GetTemplate(className).Then((t) =>
            {
                if (t != null)
                    SendParams((byte)0x88, callback, t.Content);
                else
                {
                    // reply failed
                }
            });
        }

        void IIPRequestTemplateFromClassId(uint callback, Guid classId)
        {
            Warehouse.GetTemplate(classId).Then((t) =>
            {
                if (t != null)
                    SendParams((byte)0x89, callback, (uint)t.Content.Length, t.Content);
                else
                {
                    // reply failed
                }
            });
        }

        void IIPRequestTemplateFromResourceLink(uint callback, string resourceLink)
        {
            Warehouse.GetTemplate(resourceLink).Then((t) =>
            {
                if (t != null)
                    SendParams((byte)0x8A, callback, t.Content);
                else
                {
                    // reply failed
                }
            });
        }

        void IIPRequestTemplateFromResourceId(uint callback, uint resourceId)
        {
            Warehouse.Get(resourceId).Then((r) =>
            {
                if (r != null)
                    SendParams((byte)0x8B, callback, r.Instance.Template.Content);
                else
                {
                    // reply failed
                }
            });
        }

        void IIPRequestResourceIdFromResourceLink(uint callback, string resourceLink)
        {
            Warehouse.Get(resourceLink).Then((r) =>
            {
                if (r != null)
                    SendParams((byte)0x8C, callback, r.Instance.Template.ClassId, r.Instance.Id, r.Instance.Age);
                else
                {
                    // reply failed
                }
            });
        }

        void IIPRequestInvokeFunction(uint callback, uint resourceId, byte index, byte[] content)
        {
            Warehouse.Get(resourceId).Then((r) =>
            {
                if (r != null)
                {
                    Codec.ParseVarArray(content, this).Then(async (arguments) =>
                    {
                        var ft = r.Instance.Template.GetFunctionTemplate(index);
                        if (ft != null)
                        {
                            if (r is DistributedResource)
                            {
                                var rt = (r as DistributedResource)._Invoke(index, arguments);
                                if (rt != null)
                                {
                                    rt.Then(res =>
                                    {
                                        SendParams((byte)0x90, callback, Codec.Compose(res, this));
                                    });
                                }
                                else
                                {
                                    // function not found on a distributed object
                                }
                            }
                            else
                            {
#if NETSTANDARD1_5
                                var fi = r.GetType().GetTypeInfo().GetMethod(ft.Name);
#else
                                var fi = r.GetType().GetMethod(ft.Name);
#endif

                                if (fi != null)
                                {
                                    // cast shit
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

                                    var rt = fi.Invoke(r, args);

                                    if (rt is Task)
                                    {
                                        var t = (Task)rt;
                                        //Console.WriteLine(t.IsCompleted);
                                        await t;
#if NETSTANDARD1_5
                                        var res = t.GetType().GetTypeInfo().GetProperty("Result").GetValue(t);
#else
                                        var res = t.GetType().GetProperty("Result").GetValue(t);
#endif
                                        SendParams((byte)0x90, callback, Codec.Compose(res, this));
                                    }
                                    else if (rt is AsyncReply) //(rt.GetType().IsGenericType && (rt.GetType().GetGenericTypeDefinition() == typeof(AsyncReply<>)))
                                    {
                                        (rt as AsyncReply).Then(res =>
                                        {
                                            SendParams((byte)0x90, callback, Codec.Compose(res, this));
                                        });
                                    }
                                    else
                                    {
                                        SendParams((byte)0x90, callback, Codec.Compose(rt, this));
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
            Warehouse.Get(resourceId).Then((r) =>
            {
                if (r != null)
                {
                    var pt = r.Instance.Template.GetFunctionTemplate(index);
                    if (pt != null)
                    {
                        if (r is DistributedResource)
                        {
                            SendParams((byte)0x91, callback, Codec.Compose((r as DistributedResource)._Get(pt.Index), this));
                        }
                        else
                        {
#if NETSTANDARD1_5
                            var pi = r.GetType().GetTypeInfo().GetProperty(pt.Name);
#else
                            var pi = r.GetType().GetProperty(pt.Name);
#endif

                            if (pi != null)
                            {
                                SendParams((byte)0x91, callback, Codec.Compose(pi.GetValue(r), this));
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

        void IIPRequestGetPropertyIfModifiedSince(uint callback, uint resourceId, byte index, uint age)
        {
            Warehouse.Get(resourceId).Then((r) =>
            {
                if (r != null)
                {
                    var pt = r.Instance.Template.GetFunctionTemplate(index);
                    if (pt != null)
                    {
                        if (r.Instance.GetAge(index) > age)
                        {
#if NETSTANDARD1_5
                            var pi = r.GetType().GetTypeInfo().GetProperty(pt.Name);
#else
                            var pi = r.GetType().GetProperty(pt.Name);
#endif
                            if (pi != null)
                            {
                                SendParams((byte)0x92, callback, Codec.Compose(pi.GetValue(r), this));
                            }
                            else
                            {
                                // pt found, pi not found, this should never happen
                            }
                        }
                        else
                        {
                            SendParams((byte)0x92, callback, (byte)DataType.NotModified);
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
            Warehouse.Get(resourceId).Then((r) =>
            {
                if (r != null)
                {


                    var pt = r.Instance.Template.GetPropertyTemplate(index);
                    if (pt != null)
                    {
                        Codec.Parse(content, 0, this).Then((value) =>
                        {
                            if (r is DistributedResource)
                            {
                                // propagation
                                (r as DistributedResource)._Set(index, value).Then((x) =>
                                {
                                    SendParams((byte)0x93, callback);
                                });
                            }
                            else
                            {
#if NETSTANDARD1_5
                                var pi = r.GetType().GetTypeInfo().GetProperty(pt.Name);
#else
                                var pi = r.GetType().GetProperty(pt.Name);
#endif
                                if (pi != null)
                                {
                                    // cast new value type to property type

                                    var v = DC.CastConvert(value, pi.PropertyType);
                                    pi.SetValue(r, v);

                                    SendParams((byte)0x93, callback);
                                }
                                else
                                {
                                    // pt found, pi not found, this should never happen
                                }
                            }

                        });
                    }
                    else
                    {
                        // property not found
                    }
                }
                else
                {
                    // resource not found
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

            SendRequest(IIPPacket.IIPPacketAction.TemplateFromClassId, classId).Then((rt) =>
            {
                templateRequests.Remove(classId);
                templates.Add(((ResourceTemplate)rt[0]).ClassId, (ResourceTemplate)rt[0]);
                reply.Trigger(rt[0]);
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
            if (pathRequests.ContainsKey(path))
                return pathRequests[path];

            var reply = new AsyncReply<IResource>();
            pathRequests.Add(path, reply);

            var bl = new BinaryList(path);
            bl.Insert(0, (ushort)bl.Length);

            SendRequest(IIPPacket.IIPPacketAction.ResourceIdFromResourceLink, bl.ToArray()).Then((rt) =>
            {
                pathRequests.Remove(path);
//(Guid)rt[0],
                Fetch( (uint)rt[1]).Then((r) =>
                {
                    reply.Trigger(r);
                });
            });


            return reply;
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
        public AsyncReply<DistributedResource> Fetch( uint id)
        {
            if (resourceRequests.ContainsKey(id) && resources.ContainsKey(id))
            {
                // dig for dead locks
                return resourceRequests[id];
            }
            else if (resourceRequests.ContainsKey(id))
                return resourceRequests[id];
            else if (resources.ContainsKey(id))
                return new AsyncReply<DistributedResource>(resources[id]);

            var reply = new AsyncReply<DistributedResource>();

                SendRequest(IIPPacket.IIPPacketAction.AttachResource, id).Then((rt) =>
                {
                    GetTemplate((Guid)rt[0]).Then((tmp) =>
                    {

                    // ClassId, ResourceAge, ResourceLink, Content

                    //var dr = Warehouse.New<DistributedResource>(id.ToString(), this);
                    //var dr = nInitialize(this, tmp, id, (uint)rt[0]);
                    var dr = new DistributedResource(this, tmp, id, (uint)rt[1], (string)rt[2]);
                    Warehouse.Put(dr, id.ToString(), this);

                    Codec.ParseVarArray((byte[])rt[3], this).Then((ar) =>
                    {
                        dr._Attached(ar);
                        resourceRequests.Remove(id);
                        reply.Trigger(dr);
                    });
                });
            });

            return reply;
        }

        private void Instance_ResourceDestroyed(IResource resource)
        {
            // compose the packet
            SendParams((byte)0x1, resource.Instance.Id);
        }

        private void Instance_PropertyModified(IResource resource, string name, object newValue, object oldValue)
        {
            var pt = resource.Instance.Template.GetPropertyTemplate(name);

            if (pt == null)
                return;

            // compose the packet
            if (newValue is Func<DistributedConnection, object>)
                SendParams((byte)0x10, resource.Instance.Id, pt.Index, Codec.Compose((newValue as Func<DistributedConnection, object>)(this), this));
            else
                SendParams((byte)0x10, resource.Instance.Id, pt.Index, Codec.Compose(newValue, this));

        }

        private void Instance_EventOccured(IResource resource, string name, string[] receivers, object[] args)
        {
            var et = resource.Instance.Template.GetEventTemplate(name);

            if (et == null)
                return;

            if (receivers != null)
                if (!receivers.Contains(RemoteUsername))
                    return;

            var clientArgs = new object[args.Length];
            for (var i = 0; i < args.Length; i++)
                if (args[i] is Func<DistributedConnection, object>)
                    clientArgs[i] = (args[i] as Func<DistributedConnection, object>)(this);
                else
                    clientArgs[i] = args[i];


            // compose the packet
            SendParams((byte)0x11, resource.Instance.Id, (byte)et.Index, Codec.ComposeVarArray(args, this, true));

        }
    }
}
