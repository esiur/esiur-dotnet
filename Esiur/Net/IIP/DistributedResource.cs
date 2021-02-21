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

using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.IO;
using System.Collections;
using System.ComponentModel;
using Esiur.Misc;
using Esiur.Data;
using System.Dynamic;
using System.Security.Cryptography;
using Esiur.Core;
using System.Runtime.CompilerServices;
using System.Reflection.Emit;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Esiur.Resource;
using Esiur.Resource.Template;

namespace Esiur.Net.IIP
{
    
    //[System.Runtime.InteropServices.ComVisible(true)]
    public class DistributedResource : DynamicObject, IResource
    {

        /// <summary>
        /// Raised when the distributed resource is destroyed.
        /// </summary>
        public event DestroyedEvent OnDestroy;
        public event Instance.ResourceModifiedEvent OnModified;
        uint instanceId;
        DistributedConnection connection;


        bool attached = false;
        bool destroyed = false;
        bool suspended = false;

        //Structure properties = new Structure();

        string link;
        //ulong age;
        //ulong[] ages;
        object[] properties;
        internal List<DistributedResource> parents = new List<DistributedResource>();
        internal List<DistributedResource> children = new List<DistributedResource>();

        DistributedResourceEvent[] events;

        

        /// <summary>
        /// Resource template for the remotely located resource.
        /// </summary>
        //public ResourceTemplate Template
        //{
        //    get { return template; }
        //}


        /// <summary>
        /// Connection responsible for the distributed resource.
        /// </summary>
        public DistributedConnection Connection
        {
            get { return connection; }
        }

        /// <summary>
        /// Resource link
        /// </summary>
        public string Link
        {
            get { return link; }
        }

        /// <summary>
        /// Instance Id given by the other end.
        /// </summary>
        public uint Id
        {
            get {  return instanceId; }
        }

        /// <summary>
        /// IDestructible interface.
        /// </summary>
        public void Destroy()
        {
            destroyed = true;
            attached = false;
            connection.SendDetachRequest(instanceId);
            OnDestroy?.Invoke(this);
        }

        /// <summary>
        /// Suspend resource
        /// </summary>

        internal void Suspend()
        {
            suspended = true;
            attached = false;
        }


        /// <summary>
        /// Resource is attached when all its properties are received.
        /// </summary>
        internal bool Attached => attached;

        internal bool Suspended => suspended;
    

       // public DistributedResourceStack Stack
        //{
       //     get { return stack; }
        //}

            /// <summary>
            /// Create a new distributed resource.
            /// </summary>
            /// <param name="connection">Connection responsible for the distributed resource.</param>
            /// <param name="template">Resource template.</param>
            /// <param name="instanceId">Instance Id given by the other end.</param>
            /// <param name="age">Resource age.</param>
        public DistributedResource(DistributedConnection connection, uint instanceId, ulong age, string link)
        {
            this.link = link;
            this.connection = connection;
            this.instanceId = instanceId;

            //this.Instance.Template = template;
            //this.Instance.Age = age;
            //this.template = template;
            //this.age = age;

        }

        /// <summary>
        /// Export all properties with ResourceProperty attributed as bytes array.
        /// </summary>
        /// <returns></returns>
        internal PropertyValue[] _Serialize()
        {

            var props = new PropertyValue[properties.Length];


            for (byte i = 0; i < properties.Length; i++)
                props[i] = new PropertyValue(properties[i], Instance.GetAge(i), Instance.GetModificationDate(i));

            return props;
        }

        internal bool _Attach(PropertyValue[] properties)
        {
            if (attached)
                return false;
            else
            {
                suspended = false;

                this.properties = new object[properties.Length];

                this.events = new DistributedResourceEvent[Instance.Template.Events.Length];

                for (byte i = 0; i < properties.Length; i++)
                {
                    Instance.SetAge(i, properties[i].Age);
                    Instance.SetModificationDate(i, properties[i].Date);
                    this.properties[i] = properties[i].Value;
                }

                // trigger holded events/property updates.
                //foreach (var r in afterAttachmentTriggers)
                //    r.Key.Trigger(r.Value);

                //afterAttachmentTriggers.Clear();

                attached = true;

            }
           return true;
        }

        internal void _EmitEventByIndex(byte index, object args)
        {
            var et = Instance.Template.GetEventTemplateByIndex(index);
            events[index]?.Invoke(this, args);
            Instance.EmitResourceEvent(et.Name, args);
        }

        public AsyncReply<object> _InvokeByNamedArguments(byte index, Structure namedArgs)
        {
            if (destroyed)
                throw new Exception("Trying to access destroyed object");

            if (suspended)
                throw new Exception("Trying to access suspended object");

            if (index >= Instance.Template.Functions.Length)
                throw new Exception("Function index is incorrect");


            return connection.SendInvokeByNamedArguments(instanceId, index, namedArgs);
        }

        public AsyncReply<object> _InvokeByArrayArguments(byte index, object[] args)
        {
            if (destroyed)
                throw new Exception("Trying to access destroyed object");

            if (suspended)
                throw new Exception("Trying to access suspended object");

            if (index >= Instance.Template.Functions.Length)
                throw new Exception("Function index is incorrect");


            return connection.SendInvokeByArrayArguments(instanceId, index, args);
        }

 
        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        {
            var ft = Instance.Template.GetFunctionTemplateByName(binder.Name);

            var reply = new AsyncReply<object>();

            if (attached && ft!=null)
            {
                if (args.Length == 1)
                {
                    // Detect anonymous types
                    var type = args[0].GetType();
                    if (Codec.IsAnonymous(type))
                    {
                        var namedArgs = new Structure();

                        var pi = type.GetTypeInfo().GetProperties();
                        foreach (var p in pi)
                            namedArgs[p.Name] = p.GetValue(args[0]);
                        result = _InvokeByNamedArguments(ft.Index, namedArgs);
                    }
                    else
                    {
                        result = _InvokeByArrayArguments(ft.Index, args);
                    }
                    
                }
                else
                {
                    result = _InvokeByArrayArguments(ft.Index, args);
                }
                return true;
            }
            else
            {
                result = null;
                return false;
            }
        }

        /// <summary>
        /// Get a property value.
        /// </summary>
        /// <param name="index">Zero-based property index.</param>
        /// <returns>Value</returns>
        internal object _Get(byte index)
        {
            if (index >= properties.Length)
                return null;
            return properties[index];
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            if (destroyed)
                throw new Exception("Trying to access destroyed object");


            result = null;

            if (!attached)
                return false;

            var pt = Instance.Template.GetPropertyTemplateByName(binder.Name);

            if (pt != null)
            {
                result = properties[pt.Index];
                return true;
            }
            else
            {
                var et = Instance.Template.GetEventTemplateByName(binder.Name);
                if (et == null)
                    return false;

                result = events[et.Index];

                return true;
            }
        }


        internal void _UpdatePropertyByIndex(byte index, object value)
        {
            var pt = Instance.Template.GetPropertyTemplateByIndex(index);
            properties[index] = value;
            Instance.EmitModification(pt, value);
        }

        /// <summary>
        /// Set property value.
        /// </summary>
        /// <param name="index">Zero-based property index.</param>
        /// <param name="value">Value</param>
        /// <returns>Indicator when the property is set.</returns>
        internal AsyncReply<object> _Set(byte index, object value)
        {
            if (index >= properties.Length)
                return null;

            var reply = new AsyncReply<object>();

            var parameters = Codec.Compose(value, connection);
            connection.SendRequest(Packets.IIPPacket.IIPPacketAction.SetProperty)
                        .AddUInt32(instanceId)
                        .AddUInt8(index)
                        .AddUInt8Array(parameters)
                        .Done()
                        .Then((res) =>
                        {
                            // not really needed, server will always send property modified, 
                            // this only happens if the programmer forgot to emit in property setter
                            properties[index] = value;
                            reply.Trigger(null);
                        });

            return reply;
        }

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            if (destroyed)
                throw new Exception("Trying to access destroyed object");

            if (suspended)
                throw new Exception("Trying to access suspended object");

            if (!attached)
                return false;

            var pt = Instance.Template.GetPropertyTemplateByName(binder.Name);
 
            if (pt != null)
            {
                _Set(pt.Index, value);
                return true;
            }
            else
            {
                var et = Instance.Template.GetEventTemplateByName(binder.Name);
                if (et == null)
                    return false;

                events[et.Index] = (DistributedResourceEvent)value;

                return true;
            }

         }

        /*
              public async void InvokeMethod(byte index, object[] arguments, DistributedConnection sender)
              {
                  // get function parameters
                  Type t = this.GetType();

                  MethodInfo mi = t.GetMethod(GetFunctionName(index), BindingFlags.DeclaredOnly |
                                                          BindingFlags.Public |
                                                          BindingFlags.Instance | BindingFlags.InvokeMethod);
                  if (mi != null)
                  {
                      try
                      {
                          var res = await invokeMethod(mi, arguments, sender);
                          object rt = Codec.Compose(res);
                          sender.SendParams((byte)0x80, instanceId, index, rt);
                      }
                      catch(Exception ex)
                      {
                          var msg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                          sender.SendParams((byte)0x8E, instanceId, index, Codec.Compose(msg));
                      }
                  }
              }
              */


   
            /// <summary>
            /// Resource interface.
            /// </summary>
        public Instance Instance
        {
            get;
            set;
        }

        /// <summary>
        /// Create a new instance of distributed resource.
        /// </summary>
        public DistributedResource()
        {
            //stack = new DistributedResourceStack(this);
            //this.Instance.ResourceModified += this.OnModified;

        }

        /// <summary>
        /// Resource interface.
        /// </summary>
        /// <param name="trigger"></param>
        /// <returns></returns>
        public AsyncReply<bool> Trigger(ResourceTrigger trigger)
        {

            if (trigger == ResourceTrigger.Initialize)
                this.Instance.ResourceModified += this.OnModified;

            // do nothing.
            return new AsyncReply<bool>(true);
        }
    }
}