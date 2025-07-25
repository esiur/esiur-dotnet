﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Esiur.Data;
using System.Runtime.CompilerServices;
using System.Reflection;
using Esiur.Net.IIP;
using Esiur.Misc;
using Esiur.Security.Permissions;
using Esiur.Resource.Template;
using Esiur.Security.Authority;
using Esiur.Proxy;
using Esiur.Core;
using System.Text.Json;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection.Emit;

namespace Esiur.Resource;

[NotMapped]
public class Instance
{
    string name;

    // public int IntVal { get; set; }


    WeakReference<IResource> resource;
    IStore store;
    TypeTemplate template;
    AutoList<IPermissionsManager, Instance> managers;


    public event PropertyModifiedEvent PropertyModified;



    public event EventOccurredEvent EventOccurred;
    public event CustomEventOccurredEvent CustomEventOccurred;
    public event ResourceDestroyedEvent Destroyed;

    bool loading = false;

    //KeyList<string, object> attributes;

    List<ulong?> ages = new();
    List<DateTime?> modificationDates = new();
    private ulong instanceAge;
    private DateTime instanceModificationDate;

    uint id;

    public KeyList<string, object> Variables { get; } = new KeyList<string, object>();

    /// <summary>
    /// Instance attributes are custom properties associated with the instance, a place to store information by IStore.
    /// </summary>
    //public KeyList<string, object> Attributes
    //{
    //    get
    //    {
    //        return attributes;
    //    }
    //}

    public override string ToString()
    {
        return name + " (" + Link + ")";
    }

    public bool RemoveAttributes(string[] attributes = null)
    {

        return false;

        /*
        IResource res;

        if (!resource.TryGetTarget(out res))
            return false;

        return store.RemoveAttributes(res, attributes);
        */

        /*
        if (attributes == null)
            this.attributes.Clear();
        else
        {
            foreach (var attr in attributes)
                this.attributes.Remove(attr);
        }

        return true;
        */
    }

    public Map<string, object> GetAttributes(string[] attributes = null)
    {
        // @TODO
        var rt = new Map<string, object>();

        if (attributes != null)
        {
            for (var i = 0; i < attributes.Length; i++)
            {
                var at = template.GetAttributeTemplate(attributes[i]);
                if (at != null)
                {

                }
            }
        }

        return rt;



        /*
        var st = new Structure();

        if (attributes == null)
        {
            var clone = this.attributes.Keys.ToList();
            clone.Add("managers");
            attributes = clone.ToArray();// this.attributes.Keys.ToList().Add("managers");
        }

        foreach (var attr in attributes)
        {
            if (attr == "name")
                st["name"] = this.name;
            else if (attr == "managers")
            {
                var mngrs = new List<Structure>();

                foreach (var manager in this.managers)
                    mngrs.Add(new Structure()
                    {
                        ["type"] = manager.GetType().FullName + "," + manager.GetType().GetTypeInfo().Assembly.GetName().Name,
                        ["settings"] = manager.Settings
                    });

                st["managers"] = mngrs.ToArray();
            }
            else if (attr == "parents")
            {
                //st["parents"] = parents.ToArray();
            }
            else if (attr == "children")
            {
                //st["children"] = children.ToArray();
            }
            else if (attr == "childrenCount")
            {
                //st["childrenCount"] = children.Count;
            }
            else if (attr == "type")
            {
                st["type"] = resource.GetType().FullName;
            }
            else
                st[attr] = this.attributes[attr];
        }

        return st;
        */
    }

    public bool SetAttributes(Map<string, object> attributes, bool clearAttributes = false)
    {

        // @ TODO
        IResource res;

        if (resource.TryGetTarget(out res))
        {
            foreach (var kv in attributes)
            {
                var at = template.GetAttributeTemplate(kv.Key);

                if (at != null)
                    if (at.PropertyInfo.CanWrite)
                        at.PropertyInfo.SetValue(res, DC.CastConvert(kv.Value, at.PropertyInfo.PropertyType));

            }
        }

        return true;


        /*
        try
        {

            if (clearAttributes)
                this.attributes.Clear();

            foreach (var attr in attributes)
                if (attr.Key == "name")
                    this.name = attr.Value as string;
                else if (attr.Key == "managers")
                {
                    this.managers.Clear();

                    var mngrs = attr.Value as object[];

                    foreach (var mngr in mngrs)
                    {
                        var m = mngr as Structure;
                        var type = Type.GetType(m["type"] as string);
                        if (Codec.ImplementsInterface(type, typeof(IPermissionsManager)))
                        {
                            var settings = m["settings"] as Structure;
                            var manager = Activator.CreateInstance(type) as IPermissionsManager;

                            IResource res;
                            if (this.resource.TryGetTarget(out res))
                            {
                                manager.Initialize(settings, res);
                                this.managers.Add(manager);
                            }
                        }
                        else
                            return false;
                    }
                }
                else
                {
                    this.attributes[attr.Key] = attr.Value;
                }

        }
        catch
        {
            return false;
        }

        return true;
        */
    }

    /*
    public Structure GetAttributes()
    {
        var st = new Structure();
        foreach (var a in attributes.Keys)
            st[a] = attributes[a];

        st["name"] = name;

        var mngrs = new List<Structure>();

        foreach (var manager in managers)
        {
            var mngr = new Structure();
            mngr["settings"] = manager.Settings;
            mngr["type"] = manager.GetType().FullName;
            mngrs.Add(mngr);
        }

        st["managers"] = mngrs;

        return st;
    }*/

    /// <summary>
    /// Get the age of a given property index.
    /// </summary>
    /// <param name="index">Zero-based property index.</param>
    /// <returns>Age.</returns>
    public ulong? GetAge(byte index)
    {
        if (index < ages.Count)
            return ages[index];
        else
            return 0;
    }

    /// <summary>
    /// Set the age of a property.
    /// </summary>
    /// <param name="index">Zero-based property index.</param>
    /// <param name="value">Age.</param>
    public void SetAge(byte index, ulong? value)
    {
        if (index < ages.Count)
        {
            ages[index] = value;
            if (value > instanceAge)
                instanceAge = (ulong)value;
        }
    }

    /// <summary>
    /// Set the modification date of a property.
    /// </summary>
    /// <param name="index">Zero-based property index.</param>
    /// <param name="value">Modification date.</param>
    public void SetModificationDate(byte index, DateTime? value)
    {
        if (index < modificationDates.Count)
        {
            modificationDates[index] = value;
            if (value > instanceModificationDate)
                instanceModificationDate = (DateTime)value;
        }
    }

    /// <summary>
    /// Get modification date of a specific property.
    /// </summary>
    /// <param name="index">Zero-based property index</param>
    /// <returns>Modification date.</returns>
    public DateTime? GetModificationDate(byte index)
    {
        if (index < modificationDates.Count)
            return modificationDates[index];
        else
            return DateTime.MinValue;
    }


    /// <summary>
    /// Load property value (used by stores)
    /// </summary>
    /// <param name="name">Property name</param>
    /// <param name="age">Property age</param>
    /// <param name="value">Property value</param>
    /// <returns></returns>
    public bool LoadProperty(string name, ulong? age, DateTime? modificationDate, object value)
    {

        IResource res;

        if (!resource.TryGetTarget(out res))
            return false;

        var pt = template.GetPropertyTemplateByName(name);

        if (pt == null)
            return false;

        /*
#if NETSTANDARD
        var pi = resource.GetType().GetTypeInfo().GetProperty(name, new[] { resource.GetType() });
#else
        var pi = resource.GetType().GetProperty(pt.Name);
#endif
*/

        if (pt.PropertyInfo.PropertyType.IsGenericType
            && pt.PropertyInfo.PropertyType.GetGenericTypeDefinition() == typeof(PropertyContext<>))
            return false;


        if (pt.PropertyInfo.CanWrite)
        {
            try
            {
                loading = true;

                pt.PropertyInfo.SetValue(res, DC.CastConvert(value, pt.PropertyInfo.PropertyType));
            }
            catch (Exception ex)
            {
                //Console.WriteLine(resource.ToString() + " " + name);
                Global.Log(ex);
            }

            loading = false;
        }


        SetAge(pt.Index, age);
        SetModificationDate(pt.Index, modificationDate);

        return true;
    }

    /// <summary>
    /// Age of the instance, incremented by 1 in every modification.
    /// </summary>
    public ulong Age
    {
        get { return instanceAge; }
        internal set { instanceAge = value; }
    }

    /// <summary>
    /// Last modification date.
    /// </summary>
    public DateTime? ModificationDate
    {
        get
        {
            return instanceModificationDate;
        }
    }

    /// <summary>
    /// Instance Id.
    /// </summary>
    public uint Id
    {
        get { return id; }
    }

    /// <summary>
    /// Import properties from bytes array.
    /// </summary>
    /// <param name="properties"></param>
    /// <returns></returns>
    public bool Deserialize(PropertyValue[] properties)
    {
        for (byte i = 0; i < properties.Length; i++)
        {
            var pt = this.template.GetPropertyTemplateByIndex(i);
            if (pt != null)
            {
                var pv = properties[i];
                LoadProperty(pt.Name, pv.Age, pv.Date, pv.Value);
            }
        }

        return true;
    }

    public string ToJson()
    {
        IResource res;
        if (resource.TryGetTarget(out res))
            return JsonSerializer.Serialize(res, Global.SerializeOptions);
        else
            return null;
    }

    /// <summary>
    /// Export all properties with ResourceProperty attributed as bytes array.
    /// </summary>
    /// <returns></returns>
    public PropertyValue[] Serialize()
    {
        List<PropertyValue> props = new List<PropertyValue>();

        foreach (var pt in template.Properties)
        {
            IResource res;

            if (resource.TryGetTarget(out res))
            {
                var rt = pt.PropertyInfo.GetValue(res, null);
                props.Add(new PropertyValue(rt, ages[pt.Index], modificationDates[pt.Index]));
            }
        }

        return props.ToArray();
    }

    /*
    public bool Deserialize(byte[] data, uint offset, uint length)
    {

        var props = Codec.ParseValues(data, offset, length);
        Deserialize(props);
        return true;
    }
    */
    /*
    public byte[] Serialize(bool includeLength = false, DistributedConnection sender = null)
    {

        //var bl = new BinaryList();
        List<object> props = new List<object>();

        foreach (var pt in template.Properties)
        {

            var pi = resource.GetType().GetProperty(pt.Name);

            var rt = pi.GetValue(resource, null);

            // this is a cool hack to let the property know the sender
            if (rt is Func<DistributedConnection, object>)
                rt = (rt as Func<DistributedConnection, object>)(sender);

            props.Add(rt);

         }

        if (includeLength)
        {
            return Codec.Compose(props.ToArray(), false);
        }
        else
        {
            var rt = Codec.Compose(props.ToArray(), false);
            return DC.Clip(rt, 4, (uint)(rt.Length - 4));
        }
    }

    public byte[] StorageSerialize()
    {

        var props = new List<object>();

        foreach(var pt in  template.Properties)
        {
            if (!pt.Storable)
               continue;

            var pi = resource.GetType().GetProperty(pt.Name);

            if (!pi.CanWrite)
                continue;

            var rt = pi.GetValue(resource, null);

            props.Add(rt);

         }

        return Codec.Compose(props.ToArray(), false);
    }
    */

    /// <summary>
    /// If True, the instance can be stored to disk.
    /// </summary>
    /// <returns></returns>
    public bool IsStorable()
    {
#if NETSTANDARD
        var attrs = resource.GetType().GetTypeInfo().GetCustomAttributes(typeof(Storable), true).ToArray();
#else
            var attrs = resource.GetType().GetCustomAttributes(typeof(Storable), true);
#endif
        return attrs.Length > 0;

    }


    internal void EmitModification(PropertyTemplate pt, object value)
    {

        IResource res;
        if (this.resource.TryGetTarget(out res))
        {
            instanceAge++;
            var now = DateTime.UtcNow;

            ages[pt.Index] = instanceAge;
            modificationDates[pt.Index] = now;

            if (pt.Recordable)
            {
                store.Record(res, pt.Name, value, ages[pt.Index], now);
            }
            else //if (pt.Storage == StorageMode.Recordable)
            {
                store.Modify(res, pt.Name, value, ages[pt.Index], now);
            }

            //ResourceModified?.Invoke(res, pt.Name, value);

            PropertyModified?.Invoke(new PropertyModificationInfo(res, pt, value, instanceAge));
        }
    }

    /// <summary>
    /// Notify listeners that a property was modified.
    /// </summary>
    /// <param name="propertyName"></param>
    /// <param name="newValue"></param>
    /// <param name="oldValue"></param>
    public void Modified([CallerMemberName] string propertyName = "")
    {
        if (loading)
            return;

        object value;
        if (GetPropertyValue(propertyName, out value))
        {
            var pt = template.GetPropertyTemplateByName(propertyName);
            EmitModification(pt, value);
        }
    }

 

    //        internal void EmitResourceEvent(string name, string[] users, DistributedConnection[] connections, object[] args)

    internal void EmitCustomResourceEvent(object issuer, Func<Session, bool> receivers, EventTemplate eventTemplate, object value)
    {
        IResource res;
        if (this.resource.TryGetTarget(out res))
        {
            CustomEventOccurred?.Invoke(new CustomEventOccurredInfo(res, eventTemplate, receivers, issuer, value));
        }
    }

    internal void EmitResourceEvent(EventTemplate eventTemplate, object value)
    {
        IResource res;
        if (this.resource.TryGetTarget(out res))
        {
            EventOccurred?.Invoke(new EventOccurredInfo(res, eventTemplate, value));
        }
    }

    internal void EmitResourceEventByIndex(byte eventIndex, object value)
    {
        IResource res;
        if (this.resource.TryGetTarget(out res))
        {
            var eventTemplate = template.GetEventTemplateByIndex(eventIndex);
            EventOccurred?.Invoke(new EventOccurredInfo(res, eventTemplate, value));
        }
    }

    internal void EmitCustomResourceEventByIndex(object issuer, Func<Session, bool> receivers, byte eventIndex, object value)
    {
        IResource res;
        if (this.resource.TryGetTarget(out res))
        {
            var eventTemplate = template.GetEventTemplateByIndex(eventIndex);
            CustomEventOccurred?.Invoke(new CustomEventOccurredInfo(res, eventTemplate, receivers, issuer, value));
        }
    }


    /// <summary>
    /// Get the value of a given property by name.
    /// </summary>
    /// <param name="name">Property name</param>
    /// <param name="value">Output value</param>
    /// <returns>True, if the resource has the property.</returns>
    public bool GetPropertyValue(string name, out object value)
    {
        /*
#if NETSTANDARD
        PropertyInfo pi = resource.GetType().GetTypeInfo().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

#else
        PropertyInfo pi = resource.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
#endif
*/

        var pt = template.GetPropertyTemplateByName(name);

        if (pt != null && pt.PropertyInfo != null)
        {
            /*
#if NETSTANDARD
            object[] ca = pi.GetCustomAttributes(typeof(ResourceProperty), false).ToArray();

#else
            object[] ca = pi.GetCustomAttributes(typeof(ResourceProperty), false);
#endif

            if (ca.Length > 0)
            {
                value = pi.GetValue(resource, null);
                //if (value is Func<IManager, object>)
                //  value = (value as Func<IManager, object>)(sender);
                return true;
            }
            */

            IResource res;
            if (resource.TryGetTarget(out res))
                value = pt.PropertyInfo.GetValue(res, null);
            else
            {
                value = null;
                return false;
            }

            return true;

        }

        value = null;
        return false;
    }


    /*
    public bool Inherit
    {
        get { return inherit; }
    }*/

    /// <summary>
    /// List of parents.
    /// </summary>
    //public AutoList<IResource, Instance> Parents => parents;

    /// <summary>
    /// Store responsible for creating and keeping the resource.
    /// </summary>
    public IStore Store
    {
        get { return store; }
    }

    public bool IsDestroyed { get; private set; }

    /// <summary>
    /// List of children.
    /// </summary>
    // public AutoList<IResource, Instance> Children => children;

    /// <summary>
    /// The unique and permanent link to the resource.
    /// </summary>
    public string Link
    {
        get
        {
            IResource res;
            if (this.resource.TryGetTarget(out res))
            {
                if (res == res.Instance.store)
                    return name; // root store
                else
                    return store.Link(res);
            }
            else
                return null;
        }
    }

    public AsyncBag<T> Children<T>(string name = null) where T : IResource
    {
        IResource res;
        if (this.resource.TryGetTarget(out res))
        {
            //if (!(store is null))
            return store.Children<T>(res, name);
            //else
            //    return (res as IStore).Children<T>(res, name);
        }
        else
            return new AsyncBag<T>(null);
    }

    public AsyncBag<T> Parents<T>(string name = null) where T : IResource
    {
        IResource res;
        if (this.resource.TryGetTarget(out res))
        {
            return store.Parents<T>(res, name);
        }
        else
            return new AsyncBag<T>(null);
    }

    /*
    {
        get
        {
            if (this.store != null)
                return this.store.Link(this.resource);
            else
            {
                var l = new List<string>();
                //l.Add(name);

                var p = this.resource; // parents.First();

                while (true)
                {
                    l.Insert(0, p.Instance.name);

                    if (p.Instance.parents.Count == 0)
                        break;

                    p = p.Instance.parents.First();
                }

                return String.Join("/", l.ToArray());
            }
        }
    }
    *
    */

    /// <summary>
    /// Instance name.
    /// </summary>
    public string Name
    {
        get { return name; }
        set { name = value; }
    }


    /// <summary>
    /// Resource managed by this instance.
    /// </summary>
    public IResource Resource
    {
        get
        {
            IResource res;
            if (this.resource.TryGetTarget(out res))
            {
                return res;
            }
            else
                return null;
        }
    }

    /// <summary>
    /// Resource template describes the properties, functions and events of the resource.
    /// </summary>
    public TypeTemplate Template
    {
        get { return template; }

        /*
        internal set
        {
            template = Warehouse.GetTemplate(resource.GetType());

            // set ages
            for (byte i = 0; i < template.Properties.Length; i++)
            {
                ages.Add(0);
                modificationDates.Add(DateTime.MinValue);
            }
        }
        */
    }

    /// <summary>
    /// Check for permission.
    /// </summary>
    /// <param name="session">Caller sessions.</param>
    /// <param name="action">Action type</param>
    /// <param name="member">Function, property or event to check for permission.</param>
    /// <param name="inquirer">Permission inquirer.</param>
    /// <returns>Ruling.</returns>
    public Ruling Applicable(Session session, ActionType action, MemberTemplate member, object inquirer = null)
    {
        IResource res;
        if (this.resource.TryGetTarget(out res))
        {
            //return store.Applicable(res, session, action, member, inquirer);

            foreach (IPermissionsManager manager in managers)
            {
                var r = manager.Applicable(res, session, action, member, inquirer);
                if (r != Ruling.DontCare)
                    return r;
            }

        }

        return Ruling.DontCare;

    }

    /// <summary>
    /// Execution managers.
    /// </summary>
    public AutoList<IPermissionsManager, Instance> Managers => managers;

    /// <summary>
    /// Create new instance.
    /// </summary>
    /// <param name="id">Instance Id.</param>
    /// <param name="name">Name of the instance.</param>
    /// <param name="resource">Resource to manage.</param>
    /// <param name="store">Store responsible for the resource.</param>
    public Instance(uint id, string name, IResource resource, IStore store, TypeTemplate customTemplate = null, ulong age = 0)
    {
        this.store = store;
        this.resource = new WeakReference<IResource>(resource);
        this.id = id;
        this.name = name ?? "";
        this.instanceAge = age;

        //this.attributes = new KeyList<string, object>(this);
        //children = new AutoList<IResource, Instance>(this);
        //parents = new AutoList<IResource, Instance>(this);
        managers = new AutoList<IPermissionsManager, Instance>(this);
        //children.OnAdd += Children_OnAdd;
        //children.OnRemoved += Children_OnRemoved;
        //parents.OnAdd += Parents_OnAdd;
        //parents.OnRemoved += Parents_OnRemoved;

        resource.OnDestroy += Resource_OnDestroy;

        if (customTemplate != null)
            this.template = customTemplate;
        else
            this.template = Warehouse.GetTemplateByType(resource.GetType());

        // set ages
        for (byte i = 0; i < template.Properties.Length; i++)
        {
            ages.Add(0);
            modificationDates.Add(DateTime.MinValue);
        }


        // connect events
        if (!(resource is DistributedResource))
        {

            Type t = ResourceProxy.GetBaseType(resource);

            var events = t.GetTypeInfo().GetEvents(BindingFlags.Public | BindingFlags.Instance);

            var emitEventByIndexMethod = GetType().GetMethod("EmitResourceEventByIndex", BindingFlags.Instance | BindingFlags.NonPublic);
            var emitCustomEventByIndexMethod = GetType().GetMethod("EmitCustomResourceEventByIndex", BindingFlags.Instance | BindingFlags.NonPublic);

            foreach (var evt in template.Events)
            {

                if (evt.EventInfo == null)
                    continue;

                var eventGenericType = evt.EventInfo.EventHandlerType.GetGenericTypeDefinition();

                if (eventGenericType == typeof(ResourceEventHandler<>))
                {

                    var dm = new DynamicMethod("_", null,
                       new Type[] { typeof(Instance), evt.EventInfo.EventHandlerType.GenericTypeArguments[0] },
                       typeof(Instance).Module, true);


                    var il = dm.GetILGenerator();
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldc_I4, (int)evt.Index);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Box, evt.EventInfo.EventHandlerType.GenericTypeArguments[0]);
                    il.Emit(OpCodes.Callvirt, emitEventByIndexMethod);
                    il.Emit(OpCodes.Nop);
                    il.Emit(OpCodes.Ret);


                    var proxyDelegate = dm.CreateDelegate(evt.EventInfo.EventHandlerType, this);

                    //ResourceEventHandler<object> proxyDelegate = new ResourceEventHandler<object>((args) => EmitResourceEvent(evt, args));
                    evt.EventInfo.AddEventHandler(resource, proxyDelegate);


                }
                else if (eventGenericType == typeof(CustomResourceEventHandler<>))
                {
                    var dm = new DynamicMethod("_", null,
                       new Type[] { typeof(Instance), typeof(object), typeof(Func<Session, bool>),
                       evt.EventInfo.EventHandlerType.GenericTypeArguments[0] },
                       typeof(Instance).Module, true);


                    var il = dm.GetILGenerator();
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Ldarg_2);
                    il.Emit(OpCodes.Ldc_I4, (int)evt.Index);
                    il.Emit(OpCodes.Ldarg_3);
                    il.Emit(OpCodes.Box, evt.EventInfo.EventHandlerType.GenericTypeArguments[0]);
                    il.Emit(OpCodes.Callvirt, emitCustomEventByIndexMethod);
                    il.Emit(OpCodes.Nop);
                    il.Emit(OpCodes.Ret);


                    var proxyDelegate = dm.CreateDelegate(evt.EventInfo.EventHandlerType, this);

                    evt.EventInfo.AddEventHandler(resource, proxyDelegate);
                }

            }

        }
    }


    //IQueryable<IResource> Children => store.GetChildren(this);


    /*
     *         private void Children_OnRemoved(Instance parent, IResource value)
    {
        value.Instance.parents.Remove(resource);
    }

    private void Children_OnAdd(Instance parent, IResource value)
    {
        if (!value.Instance.parents.Contains(resource))
            value.Instance.parents.Add(resource);
    }

    private void Parents_OnRemoved(Instance parent, IResource value)
    {
        value.Instance.children.Remove(resource);
    }

    private void Parents_OnAdd(Instance parent, IResource value)
    {
        if (!value.Instance.children.Contains(resource))
            value.Instance.children.Add(resource);
    }
    */

    private void Resource_OnDestroy(object sender)
    {
        IsDestroyed = true;
        Destroyed?.Invoke((IResource)sender);
    }
}
