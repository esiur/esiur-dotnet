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

using Esiur.Core;
using Esiur.Data;
using Esiur.Misc;
using Esiur.Net.IIP;
using Esiur.Net.Packets;
using Esiur.Proxy;
using Esiur.Resource.Template;
using Esiur.Security.Permissions;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Esiur.Resource;

// Central Resources Manager
public class Warehouse
{

    public static Warehouse Default = new Warehouse();

    //static byte prefixCounter;

    //static AutoList<IStore, Instance> stores = new AutoList<IStore, Instance>(null);
    ConcurrentDictionary<uint, WeakReference<IResource>> resources = new ConcurrentDictionary<uint, WeakReference<IResource>>();
    ConcurrentDictionary<IStore, List<WeakReference<IResource>>> stores = new ConcurrentDictionary<IStore, List<WeakReference<IResource>>>();


    uint resourceCounter = 0;


    KeyList<TemplateType, KeyList<UUID, TypeTemplate>> templates
        = new KeyList<TemplateType, KeyList<UUID, TypeTemplate>>()
        {
            //[TemplateType.Unspecified] = new KeyList<Guid, TypeTemplate>(),
            [TemplateType.Resource] = new KeyList<UUID, TypeTemplate>(),
            [TemplateType.Record] = new KeyList<UUID, TypeTemplate>(),
            //[TemplateType.Wrapper] = new KeyList<Guid, TypeTemplate>(),
            [TemplateType.Enum] = new KeyList<UUID, TypeTemplate>(),
        };

    bool warehouseIsOpen = false;

    public delegate void StoreEvent(IStore store);
    public event StoreEvent StoreConnected;
    public event StoreEvent StoreDisconnected;

    public delegate AsyncReply<IStore> ProtocolInstance(string name, object properties);

    public KeyList<string, ProtocolInstance> Protocols { get; } = new KeyList<string, ProtocolInstance>();

    private Regex urlRegex = new Regex(@"^(?:([\S]*)://([^/]*)/?)");


    public Warehouse()
    {
        Protocols.Add("iip",
            async (name, attributes)
            => await New<DistributedConnection>(name, null, attributes));

        new TypeTemplate(typeof(IIPAuthPacketIAuthHeader), this);

        new TypeTemplate(typeof(IIPAuthPacketIAuthDestination), this);
        new TypeTemplate(typeof(IIPAuthPacketIAuthFormat), this);
    }


    /// <summary>
    /// Get a store by its name.
    /// </summary>
    /// <param name="name">Store instance name</param>
    /// <returns></returns>
    public IStore GetStore(string name)
    {
        foreach (var s in stores)
            if (s.Key.Instance.Name == name)
                return s.Key;
        return null;
    }

    public WeakReference<IResource>[] Resources => resources.Values.ToArray();

    /// <summary>
    /// Get a resource by instance Id.
    /// </summary>
    /// <param name="id">Instance Id</param>
    /// <returns></returns>
    public AsyncReply<IResource> GetById(uint id)
    {
        if (resources.ContainsKey(id))
        {
            IResource r;
            if (resources[id].TryGetTarget(out r))
                return new AsyncReply<IResource>(r);
            else
                return new AsyncReply<IResource>(null);
        }
        else
            return new AsyncReply<IResource>(null);
    }

    void LoadGenerated()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var generatedType = assembly.GetType("Esiur.Generated");
            if (generatedType != null)
            {
                var resourceTypes = (Type[])generatedType.GetProperty("Resources").GetValue(null);
                foreach (var t in resourceTypes)
                {
                    PutTemplate(new TypeTemplate(t));
                }

                var recordTypes = (Type[])generatedType.GetProperty("Records").GetValue(null);
                foreach (var t in recordTypes)
                {
                    PutTemplate(new TypeTemplate(t));
                }

                var enumsTypes = (Type[])generatedType.GetProperty("Enums").GetValue(null);
                foreach (var t in enumsTypes)
                {
                    PutTemplate(new TypeTemplate(t));
                }
            }
        }
    }

    /// <summary>
    /// Open the warehouse.
    /// This function issues the initialize trigger to all stores and resources.
    /// </summary>
    /// <returns>True, if no problem occurred.</returns>
    public async AsyncReply<bool> Open()
    {
        if (warehouseIsOpen)
            return false;

        // Load generated models
        LoadGenerated();


        warehouseIsOpen = true;

        var resSnap = resources.Select(x =>
        {
            IResource r;
            if (x.Value.TryGetTarget(out r))
                return r;
            else
                return null;
        }).Where(r => r != null).ToArray();

        foreach (var r in resSnap)
        {
            //IResource r;
            //if (rk.Value.TryGetTarget(out r))
            //{
            var rt = await r.Trigger(ResourceTrigger.Initialize);
            //if (!rt)
            //  return false;

            if (!rt)
            {
                Global.Log("Warehouse", LogType.Warning, $"Resource failed at Initialize {r.Instance.Name} [{r.Instance.Template.ClassName}]");
            }
            //}
        }

        foreach (var r in resSnap)
        {
            var rt = await r.Trigger(ResourceTrigger.SystemInitialized);
            if (!rt)
            {
                Global.Log("Warehouse", LogType.Warning, $"Resource failed at SystemInitialized {r.Instance.Name} [{r.Instance.Template.ClassName}]");
            }
        }


        return true;

    }

    /// <summary>
    /// Close the warehouse.
    /// This function issues terminate trigger to all resources and stores.
    /// </summary>
    /// <returns>True, if no problem occurred.</returns>
    public AsyncReply<bool> Close()
    {

        var bag = new AsyncBag<bool>();

        foreach (var resource in resources.Values)
        {
            IResource r;
            if (resource.TryGetTarget(out r))
            {
                if (!(r is IStore))
                    bag.Add(r.Trigger(ResourceTrigger.Terminate));

            }
        }

        foreach (var store in stores)
            bag.Add(store.Key.Trigger(ResourceTrigger.Terminate));


        foreach (var resource in resources.Values)
        {
            IResource r;
            if (resource.TryGetTarget(out r))
            {
                if (!(r is IStore))
                    bag.Add(r.Trigger(ResourceTrigger.SystemTerminated));
            }
        }


        foreach (var store in stores)
            bag.Add(store.Key.Trigger(ResourceTrigger.SystemTerminated));

        bag.Seal();

        var rt = new AsyncReply<bool>();
        bag.Then((x) =>
        {
            foreach (var b in x)
                if (!b)
                {
                    rt.Trigger(false);
                    return;
                }

            rt.Trigger(true);
        });

        return rt;
    }


    public async AsyncReply<IResource> Query(string path)
    {
        var p = path.Trim().TrimStart('/').Split('/');

        foreach (var store in stores.Keys)
        {
            if (p[0] == store.Instance.Name)
            {
                if (p.Length == 1)
                    return store;

                var res = await store.Get(String.Join("/", p.Skip(1).ToArray()));
                if (res != null)
                    return res;

                return null;
            }
        }

        return null;
    }

    /// <summary>
    /// Get a resource by its path.
    /// Resource path is sperated by '/' character, e.g. "system/http".
    /// </summary>
    /// <param name="path"></param>
    /// <returns>Resource instance.</returns>
    public async AsyncReply<T> Get<T>(string path, object attributes = null, IResource parent = null, IPermissionsManager manager = null)
        where T : IResource
    {

        if (urlRegex.IsMatch(path))
        {

            var url = urlRegex.Split(path);

            if (Protocols.ContainsKey(url[1]))
            {
                if (!warehouseIsOpen)
                    await Open();

                var handler = Protocols[url[1]];
                var store = await handler(url[2], attributes);

                try
                {
                    if (url[3].Length > 0 && url[3] != "")
                        return (T)await store.Get(url[3]);
                    else
                        return (T)store;
                }
                catch (Exception ex)
                {
                    Remove(store);
                    throw ex;
                }
            }
        }

        var res = await Query(path);

        if (res == null)
            return default(T);
        else
            return (T)res;

    }

    /// <summary>
    /// Put a resource in the warehouse.
    /// </summary>
    /// <param name="name">Resource name.</param>
    /// <param name="resource">Resource instance.</param>
    /// <param name="store">IStore that manages the resource. Can be null if the resource is a store.</param>
    /// <param name="parent">Parent resource. if not presented the store becomes the parent for the resource.</param>
    //public async AsyncReply<T> Put<T>(string instanceName, T resource, IStore store, TypeTemplate customTemplate = null, ulong age = 0, IPermissionsManager manager = null, object attributes = null) where T : IResource
    //{
    //    if (resource.Instance != null)
    //        throw new Exception("Resource has a store.");


    //    var resourceReference = new WeakReference<IResource>(resource);


    //    if (resource is IStore && store == null)
    //        store = (IStore)resource;

    //    if (store == null)
    //        throw new Exception("Resource store is not set.");


    //    resource.Instance = new Instance(this, resourceCounter++, instanceName, resource, store, customTemplate, age);

    //    if (attributes != null)
    //        if (attributes is Map<string, object> attrs)
    //            resource.Instance.SetAttributes(attrs);
    //        else
    //            resource.Instance.SetAttributes(Map<string, object>.FromObject(attributes));

    //    if (manager != null)
    //        resource.Instance.Managers.Add(manager);

    //    //if (store == parent)
    //    //    parent = null;


    //    try
    //    {
    //        if (resource is IStore)
    //            stores.TryAdd(resource as IStore, new List<WeakReference<IResource>>());


    //        if (!await store.Put(resource))
    //            throw new Exception("Store failed to put the resource");
    //        //return default(T);


    //        //if (parent != null)
    //        //{
    //        //    await parent.Instance.Store.AddChild(parent, resource);
    //        //    await store.AddParent(resource, parent);
    //        //}

    //        var t = resource.GetType();
    //        Global.Counters["T-" + t.Namespace + "." + t.Name]++;

    //        resources.TryAdd(resource.Instance.Id, resourceReference);

    //        if (warehouseIsOpen)
    //        {
    //            await resource.Trigger(ResourceTrigger.Initialize);
    //            if (resource is IStore)
    //                await resource.Trigger(ResourceTrigger.Open);
    //        }

    //        if (resource is IStore)
    //            StoreConnected?.Invoke(resource as IStore);
    //    }
    //    catch (Exception ex)
    //    {
    //        Remove(resource);
    //        throw ex;
    //    }

    //    return resource;

    //}

    /// <summary>
    /// Put a resource in the warehouse.
    /// </summary>
    /// <param name="name">Resource name.</param>
    /// <param name="resource">Resource instance.</param>
    /// <param name="store">IStore that manages the resource. Can be null if the resource is a store.</param>
    /// <param name="parent">Parent resource. if not presented the store becomes the parent for the resource.</param>
    public async AsyncReply<T> Put<T>(string path, T resource, ulong age = 0, IPermissionsManager manager = null, object attributes = null) where T : IResource
    {
        if (resource.Instance != null)
            throw new Exception("Resource already initialized.");

        if (string.IsNullOrEmpty(path))
            throw new ArgumentNullException("Invalid path.");

        var location = path.TrimStart('/').Split('/');

        IStore store = null;
        //IResource parent = null;

        var instanceName = location.Last();

        if (location.Length == 1)
        {
            if (!(resource is IStore))
                throw new Exception("Resource is not a store, root level path is not allowed.");

            store = (IStore)resource;
        }
        else
        {

            // get parent
            var parent = await Get<IResource>(string.Join("/", location.Take(location.Length - 1)));

            if (parent == null)
                throw new Exception("Can't find parent");

            store = parent.Instance.Store;// GetStore(location[0]);

            //if (store == null)
            //    throw new Exception("Store not found.");
        }

        var resourceReference = new WeakReference<IResource>(resource);

        resource.Instance = new Instance(this, resourceCounter++, instanceName, resource, store, age);

        if (attributes != null)
            if (attributes is Map<string, object> attrs)
                resource.Instance.SetAttributes(attrs);
            else
                resource.Instance.SetAttributes(Map<string, object>.FromObject(attributes));

        try
        {
            if (resource is IStore)
                stores.TryAdd(resource as IStore, new List<WeakReference<IResource>>());
            else if ((IResource)resource != store)
            {
                if (!await store.Put(resource, string.Join("/", location.Skip(1).ToArray())))
                    throw new Exception("Store failed to put the resource.");
            }

            var t = resource.GetType();
            Global.Counters["T-" + t.Namespace + "." + t.Name]++;

            resources.TryAdd(resource.Instance.Id, resourceReference);
            
            if (warehouseIsOpen)
            {
                await resource.Trigger(ResourceTrigger.Initialize);
                if (resource is IStore)
                    await resource.Trigger(ResourceTrigger.Open);
            }

            if (resource is IStore)
                StoreConnected?.Invoke(resource as IStore);

        }
        catch (Exception ex)
        {
            Remove(resource);
            throw ex;
        }

        return resource;

    }

    public T Create<T>(object properties = null)
    {
        return (T)Create(typeof(T), properties);
    }

    public IResource Create(Type type, object properties = null)
    {
        type = ResourceProxy.GetProxy(type);

        var res = Activator.CreateInstance(type) as IResource;


        if (properties != null)
        {
            if (properties is Map<byte, object> map)
            {
                var template = GetTemplateByType(type);
                foreach (var kvp in map)
                    template.GetPropertyTemplateByIndex(kvp.Key).PropertyInfo.SetValue(res, kvp.Value);
            }
            else
            {
                var ps = Map<string, object>.FromObject(properties);

                foreach (var p in ps)
                {
                    var pi = type.GetProperty(p.Key, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (pi != null)
                    {
                        if (pi.CanWrite)
                        {
                            try
                            {
                                pi.SetValue(res, p.Value);
                            }
                            catch (Exception ex)
                            {
                                Global.Log(ex);
                            }
                        }
                    }
                    else
                    {
                        var fi = type.GetField(p.Key, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (fi != null)
                        {
                            try
                            {
                                fi.SetValue(res, p.Value);
                            }
                            catch (Exception ex)
                            {
                                Global.Log(ex);
                            }
                        }
                    }
                }
            }
        }

        return res;
    }

    public async AsyncReply<IResource> New(Type type, string path, IPermissionsManager manager = null, object attributes = null, object properties = null)
    {
        var res = Create(type, properties);
        return await Put(path, res, 0, manager, attributes);
    }

    public async AsyncReply<T> New<T>(string path, IPermissionsManager manager = null, object attributes = null, object properties = null)
        where T : IResource
    {
        return (T)(await New(typeof(T), path, manager, attributes, properties));
    }

    /// <summary>
    /// Put a resource template in the templates warehouse.
    /// </summary>
    /// <param name="template">Resource template.</param>
    public void PutTemplate(TypeTemplate template)
    {
        if (templates[template.Type].ContainsKey(template.ClassId))
            throw new Exception($"Template with same class Id already exists. {templates[template.Type][template.ClassId].ClassName} -> {template.ClassName}");

        templates[template.Type][template.ClassId] = template;
    }


    /// <summary>
    /// Get a template by type from the templates warehouse. If not in the warehouse, a new ResourceTemplate is created and added to the warehouse.
    /// </summary>
    /// <param name="type">.Net type.</param>
    /// <returns>Resource template.</returns>
    public TypeTemplate GetTemplateByType(Type type)
    {
        if (!(type.IsClass || type.IsEnum))
            return null;

        var baseType = ResourceProxy.GetBaseType(type);

        if (baseType == typeof(IResource)
            || baseType == typeof(IRecord))
            return null;

        TemplateType templateType;
        if (Codec.ImplementsInterface(type, typeof(IResource)))
            templateType = TemplateType.Resource;
        else if (Codec.ImplementsInterface(type, typeof(IRecord)))
            templateType = TemplateType.Record;
        else if (type.IsEnum)
            templateType = TemplateType.Enum;
        else
            return null;

        var template = templates[templateType].Values.FirstOrDefault(x => x.DefinedType == baseType);
        if (template != null)
            return template;

        // create new template for type
        template = new TypeTemplate(baseType, this);
        TypeTemplate.GetDependencies(template, this);

        return template;
    }

    /// <summary>
    /// Get a template by class Id from the templates warehouse. If not in the warehouse, a new ResourceTemplate is created and added to the warehouse.
    /// </summary>
    /// <param name="classId">Class Id.</param>
    /// <returns>Resource template.</returns>
    public TypeTemplate GetTemplateByClassId(UUID classId, TemplateType? templateType = null)
    {
        if (templateType == null)
        {
            // look into resources
            var template = templates[TemplateType.Resource][classId];
            if (template != null)
                return template;

            // look into records
            template = templates[TemplateType.Record][classId];
            if (template != null)
                return template;

            // look into enums
            template = templates[TemplateType.Enum][classId];
            return template;
            //if (template != null)


            //// look in wrappers
            //template = templates[TemplateType.Wrapper][classId];
            //return template;
        }
        else
            return templates[templateType.Value][classId];

    }

    /// <summary>
    /// Get a template by class name from the templates warehouse. If not in the warehouse, a new ResourceTemplate is created and added to the warehouse.
    /// </summary>
    /// <param name="className">Class name.</param>
    /// <returns>Resource template.</returns>
    public TypeTemplate GetTemplateByClassName(string className, TemplateType? templateType = null)
    {
        if (templateType == null)
        {
            // look into resources
            var template = templates[TemplateType.Resource].Values.FirstOrDefault(x => x.ClassName == className);
            if (template != null)
                return template;

            // look into records
            template = templates[TemplateType.Record].Values.FirstOrDefault(x => x.ClassName == className);
            if (template != null)
                return template;

            // look into enums
            template = templates[TemplateType.Enum].Values.FirstOrDefault(x => x.ClassName == className);
            //if (template != null)
            return template;

            //// look in wrappers
            //template = templates[TemplateType.Wrapper].Values.FirstOrDefault(x => x.ClassName == className);
            //return template;
        }
        else
        {
            return templates[templateType.Value].Values.FirstOrDefault(x => x.ClassName == className);
        }
    }

    public bool Remove(IResource resource)
    {

        if (resource.Instance == null)
            return false;


        WeakReference<IResource> resourceReference;

        if (resources.ContainsKey(resource.Instance.Id))
            resources.TryRemove(resource.Instance.Id, out resourceReference);
        else
            return false;


        if (resource != resource.Instance.Store)
        {
            List<WeakReference<IResource>> list;
            if (stores.TryGetValue(resource.Instance.Store, out list))
            {

                lock (((ICollection)list).SyncRoot)
                    list.Remove(resourceReference);

            }
        }
        if (resource is IStore)
        {
            var store = resource as IStore;

            List<WeakReference<IResource>> toBeRemoved;

            stores.TryRemove(store, out toBeRemoved);


            foreach (var o in toBeRemoved)
            {
                IResource r;
                if (o.TryGetTarget(out r))
                    Remove(r);
            }

            StoreDisconnected?.Invoke(resource as IStore);
        }

        if (resource.Instance.Store != null)
            resource.Instance.Store.Remove(resource);

        resource.Destroy();

        resource.Instance = null;

        return true;
    }
}
