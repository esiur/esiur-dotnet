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
using Esiur.Net.Packets;

namespace Esiur.Net.IIP;

//[System.Runtime.InteropServices.ComVisible(true)]
public class DistributedResource : DynamicObject, IResource, INotifyPropertyChanged
{

    /// <summary>
    /// Raised when the distributed resource is destroyed.
    /// </summary>
    public event DestroyedEvent OnDestroy;
    //public event PropertyModifiedEvent PropertyModified;
    public event PropertyChangedEventHandler PropertyChanged;

    uint instanceId;
    DistributedConnection connection;


    bool attached = false;
    bool destroyed = false;
    bool suspended = false;

    //Structure properties = new Structure();

    string link;
    //ulong age;

    protected object[] properties;
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
    public DistributedConnection DistributedResourceConnection
    {
        get { return connection; }
    }

    /// <summary>
    /// Resource link
    /// </summary>
    public string DistributedResourceLink
    {
        get { return link; }
    }

    /// <summary>
    /// Instance Id given by the other end.
    /// </summary>
    public uint DistributedResourceInstanceId
    {
        get { return instanceId; }
        internal set { instanceId = value; }
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
    public bool DistributedResourceAttached => attached;

    public bool DistributedResourceSuspended => suspended;


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
            props[i] = new PropertyValue(properties[i], 
                                        Instance.GetAge(i), 
                                        Instance.GetModificationDate(i));

        return props;
    }

    internal Map<byte, PropertyValue> _SerializeAfter(ulong age = 0)
    {
        var rt = new Map<byte, PropertyValue>();

        for (byte i = 0; i < properties.Length; i++)
            if (Instance.GetAge(i) > age)
                rt.Add(i, new PropertyValue(properties[i],
                                            Instance.GetAge(i),
                                            Instance.GetModificationDate(i)));
           

        return rt;
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


    protected internal virtual void _EmitEventByIndex(byte index, object args)
    {
        var et = Instance.Template.GetEventTemplateByIndex(index);
        events[index]?.Invoke(this, args);
        Instance.EmitResourceEvent(et, args);
    }

    public AsyncReply<object> _Invoke(byte index, Map<byte, object> args)
    {
        if (destroyed)
            throw new Exception("Trying to access a destroyed object.");

        if (suspended)
            throw new Exception("Trying to access a suspended object.");

        if (index >= Instance.Template.Functions.Length)
            throw new Exception("Function index is incorrect.");

        var ft = Instance.Template.GetFunctionTemplateByIndex(index);

        if (ft == null)
            throw new Exception("Function template not found.");

        if (ft.IsStatic)
            return connection.StaticCall(Instance.Template.ClassId, index, args);
        else
            return connection.SendInvoke(instanceId, index, args);
    }

    public AsyncReply Subscribe(EventTemplate et)
    {
        if (et == null)
            return new AsyncReply().TriggerError(new AsyncException(ErrorType.Management, (ushort)ExceptionCode.MethodNotFound, ""));

        if (!et.Subscribable)
            return new AsyncReply().TriggerError(new AsyncException(ErrorType.Management, (ushort)ExceptionCode.NotSubscribable, ""));

        return connection.SendListenRequest(instanceId, et.Index);
    }

    public AsyncReply Subscribe(string eventName)
    {
        var et = Instance.Template.GetEventTemplateByName(eventName);

        return Subscribe(et);
    }


    public AsyncReply Unsubscribe(EventTemplate et)
    {
        if (et == null)
            return new AsyncReply().TriggerError(new AsyncException(ErrorType.Management, (ushort)ExceptionCode.MethodNotFound, ""));

        if (!et.Subscribable)
            return new AsyncReply().TriggerError(new AsyncException(ErrorType.Management, (ushort)ExceptionCode.NotListenable, ""));

        return connection.SendUnlistenRequest(instanceId, et.Index);
    }

    public AsyncReply Unsubscribe(string eventName)
    {
        var et = Instance.Template.GetEventTemplateByName(eventName);

        return Unsubscribe(et);
    }


    public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
    {
        if (destroyed)
            throw new Exception("Trying to access a destroyed object.");

        if (suspended)
            throw new Exception("Trying to access a suspended object.");

        var ft = Instance.Template.GetFunctionTemplateByName(binder.Name);

        var reply = new AsyncReply<object>();

        if (attached && ft != null)
        {
            var indexedArgs = new Map<byte, object>();

            if (args.Length == 1)
            {
                // Detect anonymous types
                var type = args[0].GetType();


                if (Codec.IsAnonymous(type))
                {

                    var pis = type.GetProperties();

                    for (byte i = 0; i < ft.Arguments.Length; i++)
                    {
                        var pi = pis.FirstOrDefault(x => x.Name == ft.Arguments[i].Name);
                        if (pi != null)
                            indexedArgs.Add(i, pi.GetValue(args[0]));
                    }

                    result = _Invoke(ft.Index, indexedArgs);
                }
                else
                {
                    indexedArgs.Add((byte)0, args[0]);
                    result = _Invoke(ft.Index, indexedArgs);
                }
            }
            else
            {
                for (byte i = 0; i < args.Length; i++)
                    indexedArgs.Add(i, args[i]);

                result = _Invoke(ft.Index, indexedArgs);
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
    protected internal object _Get(byte index)
    {
        if (index >= properties.Length)
            return null;
        return properties[index];
    }

    public bool TryGetPropertyValue(byte index, out object value)
    {
        if (index >= properties.Length)
        {
            value = null;
            return false;
        }
        else
        {
            value = properties[index];
            return true;
        }
    }

    public override bool TryGetMember(GetMemberBinder binder, out object result)
    {
        if (destroyed)
            throw new Exception("Trying to access a destroyed object.");


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
    protected object _SetSync(byte index, object value)
    {
        //Console.WriteLine("Setting..." + index + " " + value);

        if (destroyed)
            throw new Exception("Trying to access a destroyed object.");

        if (suspended)
            throw new Exception("Trying to access a suspended object.");

        if (!attached)
            return null;

        if (index >= properties.Length)
            return null;

        // Don't set the same current value
        if (properties[index] == value)
            return value;

        var rt = _Set(index, value).Wait();

        //Console.WriteLine("Done Setting");
        return rt;
    }

    /// <summary>
    /// Set property value.
    /// </summary>
    /// <param name="index">Zero-based property index.</param>
    /// <param name="value">Value</param>
    /// <returns>Indicator when the property is set.</returns>
    protected internal AsyncReply<object> _Set(byte index, object value)
    {
        if (destroyed)
            throw new Exception("Trying to access a destroyed object.");

        if (suspended)
            throw new Exception("Trying to access a suspended object.");

        if (!attached)
            return null;

        if (index >= properties.Length)
            return null;

        var reply = new AsyncReply<object>();

        connection.SendSetProperty(instanceId, index, value)
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
            throw new Exception("Trying to access a destroyed object.");

        if (suspended)
            throw new Exception("Trying to access a suspended object.");

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
        {
            this.Instance.PropertyModified += (x) =>
                    this.PropertyChanged?.Invoke(this, new ResourcePropertyChangedEventArgs(x.Name));
        }
        // do nothing.
        return new AsyncReply<bool>(true);
    }

    protected virtual void EmitPropertyChanged(string name)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    ~DistributedResource()
    {
        Destroy();
    }
}