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
using Esiur.Net.Packets;
using Esiur.Data.Types;

namespace Esiur.Protocol;

//[System.Runtime.InteropServices.ComVisible(true)]
public class EpResource : DynamicObject, IResource, INotifyPropertyChanged, IDynamicResource
{

    /// <summary>
    /// Raised when the distributed resource is destroyed.
    /// </summary>
    public event DestroyedEvent OnDestroy;
    //public event PropertyModifiedEvent PropertyModified;
    public event PropertyChangedEventHandler PropertyChanged;

    uint _instanceId;
    TypeDef _typeDef;
    EpConnection _connection;


    // Single explicit lifecycle state, replacing the former attached/destroyed/suspended booleans.
    Resource.ResourceStatus _status = Resource.ResourceStatus.Pending;

    // Internal read-only views kept so the existing guard checks read naturally.
    //bool attached => status == Resource.ResourceStatus.Attached || status == Resource.ResourceStatus.Published;
    //bool destroyed => status == Resource.ResourceStatus.Destroyed;
    //bool suspended => status == Resource.ResourceStatus.Suspended;

    //Structure properties = new Structure();

    string _link;
    ulong _age;

    protected object[] _properties;
    //internal List<EpResource> parents = new List<EpResource>();
    //internal List<EpResource> children = new List<EpResource>();

    EpResourceEvent[] _events;



    /// <summary>
    /// Connection responsible for the distributed resource.
    /// </summary>
    public EpConnection ResourceConnection
    {
        get { return _connection; }
    }

    /// <summary>
    /// Resource link
    /// </summary>
    public string ResourceLink
    {
        get { return _link; }
    }

    /// <summary>
    /// Instance Id given by the other end.
    /// </summary>
    public uint ResourceInstanceId
    {
        get { return _instanceId; }
        internal set { _instanceId = value; }
    }

    /// <summary>
    /// IDestructible interface.
    /// </summary>
    public void Destroy()
    {
        _status = Resource.ResourceStatus.Destroyed;
        _connection.SendDetachRequest(_instanceId);
        OnDestroy?.Invoke(this);
    }

    /// <summary>
    /// Suspend resource
    /// </summary>

    internal void Suspend()
    {
        _status = Resource.ResourceStatus.Suspended;
    }

    /// <summary>
    /// Marks the resource as published: attached and delivered to the application as part of a
    /// fully-attached object graph. A resource only transitions Attached -&gt; Published.
    /// </summary>
    internal void Publish()
    {
        if (_status == Resource.ResourceStatus.Attached)
            _status = Resource.ResourceStatus.Published;
    }

    /// <summary>
    /// The resource's current lifecycle state. Only <see cref="Resource.ResourceStatus.Published"/>
    /// guarantees the resource and its whole dependency graph are ready for application use.
    /// </summary>
    public Resource.ResourceStatus Status => _status;

    /// <summary>
    /// Resource is attached when all its own properties are received (it may be Published too).
    /// </summary>
    //public bool ResourceAttached => attached;

    //public bool ResourceSuspended => suspended;

    /// <summary>True once the resource has been published to the application.</summary>
    //public bool ResourcePublished => status == Resource.ResourceStatus.Published;

    /// <summary>
    /// Enumerates the distributed resources directly referenced by this resource's property values
    /// (including those nested inside arrays/lists/maps). Used to walk the dependency graph when
    /// publishing a fully-attached graph to the application.
    /// </summary>
    internal IEnumerable<EpResource> GetReferencedResources()
    {
        if (_properties == null)
            yield break;

        foreach (var value in _properties)
            foreach (var resource in FlattenResources(value))
                yield return resource;
    }

    static IEnumerable<EpResource> FlattenResources(object value)
    {
        if (value is EpResource resource)
        {
            yield return resource;
        }
        else if (value is System.Collections.IDictionary dictionary)
        {
            foreach (var item in dictionary.Values)
                foreach (var r in FlattenResources(item))
                    yield return r;
        }
        else if (value is System.Collections.IEnumerable sequence && !(value is string))
        {
            foreach (var item in sequence)
                foreach (var r in FlattenResources(item))
                    yield return r;
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
    /// <param name="instanceId">Instance Id given by the other end.</param>
    /// <param name="age">Resource age.</param>
    public EpResource(EpConnection connection, uint instanceId, ulong age, string link)
    {
        this._link = link;
        this._connection = connection;
        this._instanceId = instanceId;
        this._age = age;
    }

    internal bool _Attach(PropertyValue[] properties)
    {
        if (_status == ResourceStatus.Attached)
            return false;

        _properties = new object[properties.Length];

        _events = new EpResourceEvent[Instance.Definition.Events.Length];

        for (byte i = 0; i < properties.Length; i++)
        {
            Instance.SetAge(i, properties[i].Age);
            Instance.SetModificationDate(i, properties[i].Date);
            this._properties[i] = properties[i].Value;
        }

        // trigger holded events/property updates.
        //foreach (var r in afterAttachmentTriggers)
        //    r.Key.Trigger(r.Value);

        //afterAttachmentTriggers.Clear();

        _status = Resource.ResourceStatus.Attached;


        return true;
    }

    /// <summary>
    /// Re-attaches a previously attached (then suspended) resource after reconnection by merging
    /// only the properties that changed while disconnected. The peer returns just the delta — the
    /// properties whose age is newer than the age this side last knew — so unchanged properties
    /// keep their existing value/age/date. Returns false if the resource was never attached (no
    /// prior state to merge into), in which case the caller should perform a full attach.
    /// </summary>
    /// <param name="delta">Modified properties keyed by their property index.</param>
    internal bool _Reattach(Map<byte, PropertyValue> delta)
    {
        if (_properties == null || _events == null)
            return false; // no prior state — caller should perform a full attach instead.

        foreach (var kv in delta)
        {
            var index = kv.Key;
            if (index >= _properties.Length)
                continue;

            Instance.SetAge(index, kv.Value.Age);
            Instance.SetModificationDate(index, kv.Value.Date);
            _properties[index] = kv.Value.Value;
        }

        _status = Resource.ResourceStatus.Attached;
        return true;
    }


    protected internal virtual void _EmitEventByIndex(byte index, object args)
    {
        var et = Instance.Definition.GetEventDefByIndex(index);
        _events[index]?.Invoke(this, args);
        Instance.EmitResourceEvent(et, args);
    }

    public AsyncReply _Invoke(byte index, object args)
    {

        if (_status == ResourceStatus.Destroyed)
            throw new Exception("Trying to access a destroyed object.");

        if (_status == ResourceStatus.Suspended)
            throw new Exception("Trying to access a suspended object.");

        if (index >= Instance.Definition.Functions.Length)
            throw new Exception("Function index is incorrect.");

        var ft = Instance.Definition.GetFunctionDefByIndex(index);

        if (ft == null)
            throw new Exception("Function definition not found.");

        if (ft.IsStatic)
            return ft.StreamMode == StreamMode.None
                ? _connection.StaticCall(Instance.Definition.Id, index, args)
                : _connection.StaticStreamCall(Instance.Definition.Id, index, args, ft.StreamMode);

        return ft.StreamMode == StreamMode.None
            ? _connection.SendInvoke(_instanceId, index, args)
            : _connection.SendStreamInvoke(_instanceId, index, args, ft.StreamMode);
    }

    public AsyncStreamReply<T> _InvokeStream<T>(byte index, object args)
    {
        if (_status == ResourceStatus.Destroyed)
            throw new Exception("Trying to access a destroyed object.");

        if (_status == ResourceStatus.Suspended)
            throw new Exception("Trying to access a suspended object.");

        var function = Instance.Definition.GetFunctionDefByIndex(index)
            ?? throw new Exception("Function definition not found.");

        if (function.StreamMode == StreamMode.None)
            throw new Exception("Function is not a stream.");

        if (function.IsStatic)
            return _connection.StaticStreamCall<T>(Instance.Definition.Id, index, args, function.StreamMode);

        return _connection.SendStreamInvoke<T>(_instanceId, index, args, function.StreamMode);
    }

    public AsyncReply Subscribe(EventDef et)
    {
        if (et == null)
        {
            var rt = new AsyncReply();
            rt.TriggerError(new AsyncException(ErrorType.Management, (ushort)ExceptionCode.MethodNotFound, ""));
            return rt;
        }

        if (!et.Subscribable)
        {
            var rt = new AsyncReply();
            rt.TriggerError(new AsyncException(ErrorType.Management, (ushort)ExceptionCode.NotSubscribable, ""));
            return rt;
        }

        return _connection.SendSubscribeRequest(_instanceId, et.Index);
    }

    public AsyncReply Subscribe(string eventName)
    {
        var et = Instance.Definition.GetEventDefByName(eventName);

        return Subscribe(et);
    }


    public AsyncReply Unsubscribe(EventDef et)
    {
        if (et == null)
        {
            var rt = new AsyncReply();
            rt.TriggerError(new AsyncException(ErrorType.Management, (ushort)ExceptionCode.MethodNotFound, ""));
            return rt;
        }

        if (!et.Subscribable)
        {
            var rt = new AsyncReply();
            rt.TriggerError(new AsyncException(ErrorType.Management, (ushort)ExceptionCode.NotSubscribable, ""));
            return rt;
        }

        return _connection.SendUnsubscribeRequest(_instanceId, et.Index);
    }

    public AsyncReply Unsubscribe(string eventName)
    {
        var et = Instance.Definition.GetEventDefByName(eventName);

        return Unsubscribe(et);
    }


    public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
    {
        if (_status == ResourceStatus.Destroyed)
            throw new Exception("Trying to access a destroyed object.");

        if (_status == ResourceStatus.Suspended)
            throw new Exception("Trying to access a suspended object.");


        if (_status != ResourceStatus.Published)
        {
            result = null;
            return false;
        }

        var ft = Instance.Definition.GetFunctionDefByName(binder.Name) 
            ?? throw new Exception($"{binder.Name} does not exist");

        var reply = new AsyncReply<object>();


        if (args.Length == 1)
        {
            // Detect anonymous types
            var type = args[0].GetType();


            if (Codec.IsAnonymous(type))
            {
                var indexedArgs = new Map<byte, object>();

                var pis = type.GetProperties();

                for (byte i = 0; i < ft.Arguments.Length; i++)
                {
                    var pi = pis.FirstOrDefault(x => x.Name == ft.Arguments[i].Name);
                    if (pi != null)
                        indexedArgs.Add(i, pi.GetValue(args[0]));
                }

                result = _Invoke(ft.Index, indexedArgs);
            }
            else if (args[0] is object[] || args[0] is Map<byte, object>)
            {
                result = _Invoke(ft.Index, new object[] { args });
            }
            else
            {
                result = _Invoke(ft.Index, args);
            }
        }
        else
        {

            result = _Invoke(ft.Index, args);
        }

        return true;

    }

    ///// <summary>
    ///// Get a property value.
    ///// </summary>
    ///// <param name="index">Zero-based property index.</param>
    ///// <returns>Value</returns>
    //protected internal object _Get(byte index)
    //{
    //}

    public bool TryGetPropertyValue(byte index, out object value)
    {
        if (index >= _properties.Length)
        {
            value = null;
            return false;
        }
        else
        {
            value = _properties[index];
            return true;
        }
    }

    public override bool TryGetMember(GetMemberBinder binder, out object result)
    {
        if (_status == ResourceStatus.Destroyed)
            throw new Exception("Trying to access a destroyed object.");


        result = null;

        if (_status != ResourceStatus.Published)
            return false;

        var pt = Instance.Definition.GetPropertyDefByName(binder.Name);

        if (pt != null)
        {
            result = _properties[pt.Index];
            return true;
        }
        else
        {
            var et = Instance.Definition.GetEventDefByName(binder.Name);
            if (et == null)
                return false;

            result = _events[et.Index];

            return true;
        }
    }


    internal void _UpdatePropertyByIndex(byte index, object value)
    {
        var pt = Instance.Definition.GetPropertyDefByIndex(index);
        _properties[index] = value;
        Instance.EmitModification(pt, value);
    }

    /// <summary>
    /// Set property value.
    /// </summary>
    /// <param name="index">Zero-based property index.</param>
    /// <param name="value">Value</param>
    /// <returns>Indicator when the property is set.</returns>
    //protected object _SetSync(byte index, object value)
    //{
    //}

    ///// <summary>
    ///// Set property value.
    ///// </summary>
    ///// <param name="index">Zero-based property index.</param>
    ///// <param name="value">Value</param>
    ///// <returns>Indicator when the property is set.</returns>
    //protected internal AsyncReply<object> _Set(byte index, object value)
    //{
    //}

    public override bool TrySetMember(SetMemberBinder binder, object value)
    {
        if (_status == ResourceStatus.Destroyed)
            throw new Exception("Trying to access a destroyed object.");

        if (_status == ResourceStatus.Suspended)
            throw new Exception("Trying to access a suspended object.");

        if (_status != ResourceStatus.Published)
            return false;

        var pt = Instance.Definition.GetPropertyDefByName(binder.Name);

        if (pt != null)
        {
            SetResourceProperty(pt.Index, value);
            return true;
        }
        else
        {
            var et = Instance.Definition.GetEventDefByName(binder.Name);
            if (et == null)
                return false;

            _events[et.Index] = (EpResourceEvent)value;

            return true;
        }

    }


    /// <summary>
    /// Resource interface.
    /// </summary>
    public Instance Instance
    {
        get;
        set;
    }

    public TypeDef ResourceDefinition
    {
        get
        {
            return _typeDef;
        }
        internal set
        {
            _typeDef = value;
        }
    }

    /// <summary>
    /// Create a new instance of distributed resource.
    /// </summary>
    public EpResource()
    {
        //stack = new DistributedResourceStack(this);
        //this.Instance.ResourceModified += this.OnModified;

    }

    /// <summary>
    /// Resource interface.
    /// </summary>
    /// <param name="trigger"></param>
    /// <returns></returns>
    public AsyncReply<bool> Handle(ResourceOperation operation, IResourceContext context = null)
    {

        if (operation == ResourceOperation.Initialize)
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

    public PropertyValue[] SerializeResource()
    {
        var props = new PropertyValue[_properties.Length];

        for (byte i = 0; i < _properties.Length; i++)
            props[i] = new PropertyValue(_properties[i],
                                        Instance.GetAge(i),
                                        Instance.GetModificationDate(i));

        return props;
    }

    public Map<byte, PropertyValue> SerializeResourceAfter(ulong age = 0)
    {
        var rt = new Map<byte, PropertyValue>();

        for (byte i = 0; i < _properties.Length; i++)
            if (Instance.GetAge(i) > age)
                rt.Add(i, new PropertyValue(_properties[i],
                                            Instance.GetAge(i),
                                            Instance.GetModificationDate(i)));


        return rt;
    }




    public object GetResourceProperty(byte index)
    {
        if (index >= _properties.Length)
            return null;
        return _properties[index];
    }

    public AsyncReply SetResourcePropertyAsync(byte index, object value)
    {
        if (_status == ResourceStatus.Destroyed)
            throw new Exception("Trying to access a destroyed object.");

        if (_status == ResourceStatus.Suspended)
            throw new Exception("Trying to access a suspended object.");

        if (_status != ResourceStatus.Published)
            throw new Exception("Resource is not published.");

        if (index >= _properties.Length)
            throw new Exception("Property index not found."); ;

        var reply = new AsyncReply<object>();

        _connection.SendSetProperty(_instanceId, index, value)
                    .Then((res) =>
                    {
                        // not really needed, server will always send property modified, 
                        // this only happens if the programmer forgot to emit in property setter
                        _properties[index] = value;
                        reply.Trigger(null);
                    })
                    .Error(reply.TriggerError);

        return reply;

    }

    public void SetResourceProperty(byte index, object value)
    {
        // Don't set the same current value
        if (_properties[index] == value)
            return;

        SetResourcePropertyAsync(index, value).Wait();

        return;
    }

    ~EpResource()
    {
        Destroy();
    }
}
