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
using Esiur.Engine;
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

        uint instanceId;
        DistributedConnection connection;


        bool isAttached = false;
        bool isReady = false;


        //Structure properties = new Structure();

        string link;
        uint age;
        uint[] ages;
        object[] properties;
        DistributedResourceEvent[] events;

        ResourceTemplate template;

 
        //DistributedResourceStack stack;

 
        bool destroyed;

        /// <summary>
        /// Resource template for the remotely located resource.
        /// </summary>
        public ResourceTemplate Template
        {
            get { return template; }
        }


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
            OnDestroy?.Invoke(this);
        }
         
        /// <summary>
        /// Resource is ready when all its properties are attached.
        /// </summary>
        internal bool IsReady
        {
            get
            {
                return isReady;
            }
        }

        /// <summary>
        /// Resource is attached when all its properties are received.
        /// </summary>
        internal bool IsAttached
        {
            get
            {
                return isAttached;
            }
        }
 
    

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
        public DistributedResource(DistributedConnection connection, ResourceTemplate template, uint instanceId, uint age, string link)
        {
            this.link = link;
            this.connection = connection;
            this.instanceId = instanceId;
            this.template = template;
            this.age = age;
        }

        internal void _Ready()
        {
            isReady = true;
        }
        
        internal bool _Attached(object[] properties)
        {
            
            if (isAttached)
                return false;
            else
            {
                this.properties = properties;
                ages = new uint[properties.Length];
                this.events = new DistributedResourceEvent[template.Events.Length];
                isAttached = true;
            }
           return true;
        }

        internal void _EmitEventByIndex(byte index, object[] args)
        {
            var et = template.GetEventTemplate(index);
            events[index]?.Invoke(this, args);
            Instance.EmitResourceEvent(et.Name, null, args);
        }

        public AsyncReply _Invoke(byte index, object[] args)
        {
            if (destroyed)
                throw new Exception("Trying to access destroyed object");

            if (index >= template.Functions.Length)
                throw new Exception("Function index is incorrect");

            var reply = new AsyncReply();

            var parameters = Codec.ComposeVarArray(args, connection, true);
            connection.SendRequest(Packets.IIPPacket.IIPPacketAction.InvokeFunction, instanceId, index, parameters).Then((res) =>
            {
                Codec.Parse((byte[])res[0], 0, connection).Then((rt) =>
                {
                    reply.Trigger(rt);
                });
            });

 
            return reply;

        }

 
        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        {
            var ft = template.GetFunctionTemplate(binder.Name);

            var reply = new AsyncReply();

            if (isAttached && ft!=null)
            {
                result = _Invoke(ft.Index, args);
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

            if (!isAttached)
                return false;

            var pt = template.GetPropertyTemplate(binder.Name);

            if (pt != null)
            {
                result = properties[pt.Index];
                return true;
            }
            else
            {
                var et = template.GetEventTemplate(binder.Name);
                if (et == null)
                    return false;

                result = events[et.Index];

                return true;
            }
        }


        internal void UpdatePropertyByIndex(byte index, object value)
        {
            var pt = template.GetPropertyTemplate(index);
            properties[index] = value;
            Instance.Modified(pt.Name, value);
        }

        /// <summary>
        /// Set property value.
        /// </summary>
        /// <param name="index">Zero-based property index.</param>
        /// <param name="value">Value</param>
        /// <returns>Indicator when the property is set.</returns>
        internal AsyncReply _Set(byte index, object value)
        {
            if (index >= properties.Length)
                return null;

            var reply = new AsyncReply();

            var parameters = Codec.Compose(value, connection);
            connection.SendRequest(Packets.IIPPacket.IIPPacketAction.SetProperty, instanceId, index, parameters).Then((res) =>
            {
                // not really needed, server will always send property modified, this only happens if the programmer forgot to emit in property setter
                //Update(index, value);
                reply.Trigger(null);
                // nothing to do here
            });

            return reply;
        }

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            if (destroyed)
                throw new Exception("Trying to access destroyed object");

            if (!isAttached)
                return false;

            var pt = template.GetPropertyTemplate(binder.Name);

 
            if (pt != null)
            {
                _Set(pt.Index, value);
                return true;
            }
            else
            {
                var et = template.GetEventTemplate(binder.Name);
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

        }
         
        /// <summary>
        /// Resource interface.
        /// </summary>
        /// <param name="trigger"></param>
        /// <returns></returns>
        public AsyncReply<bool> Trigger(ResourceTrigger trigger)
        {
            // do nothing.
            return new AsyncReply<bool>(true);
        }
    }
}