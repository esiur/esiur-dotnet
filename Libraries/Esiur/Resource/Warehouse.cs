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
using Esiur.Data.Types;
using Esiur.Misc;
using Esiur.Net.Packets;
using Esiur.Protocol;
using Esiur.Proxy;
using Esiur.Security.Authority;
using Esiur.Security.Permissions;
using Org.BouncyCastle.Asn1.Cms;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Esiur.Resource;

// Central Resources Manager
public class Warehouse
{

    public static Warehouse Default = new Warehouse();

    //static byte prefixCounter;

    ConcurrentDictionary<uint, WeakReference<IResource>> _resources = new ConcurrentDictionary<uint, WeakReference<IResource>>();
    ConcurrentDictionary<IStore, List<WeakReference<IResource>>> _stores = new ConcurrentDictionary<IStore, List<WeakReference<IResource>>>();

    // Memoizes Tru.FromType results, which are reflection-heavy and recomputed for every
    // array element, record property and tuple field during serialization. Tru instances
    // are immutable once constructed, so caching and sharing them per warehouse is safe.
    internal ConcurrentDictionary<Type, Esiur.Data.Tru> TypeRepresentationCache
        = new ConcurrentDictionary<Type, Esiur.Data.Tru>();

    volatile int _resourceCounter = 0;
    volatile int _typeDefsCounter = 0;


    //KeyList<TypeDefKind, KeyList<uint, LocalTypeDef>> _localTypeDefs
    //    = new KeyList<TypeDefKind, KeyList<uint, LocalTypeDef>>()
    //    {
    //        [TypeDefKind.Resource] = new KeyList<uint, LocalTypeDef>(),
    //        [TypeDefKind.Record] = new KeyList<uint, LocalTypeDef>(),
    //        [TypeDefKind.Enum] = new KeyList<uint, LocalTypeDef>(),
    //    };

    KeyList<ulong, TypeDef> _localTypeDefs
        = new KeyList<ulong, TypeDef>();


    KeyList<string, KeyList<ulong, RemoteTypeDef>> _remoteTypeDefs
        = new KeyList<string, KeyList<ulong, RemoteTypeDef>>();

    // Domain -> Kind -> Type Name -> Proxy Type
    KeyList<string, KeyList<TypeDefKind, KeyList<string, Type>>> _proxyTypeDefs = new();


    Map<string, IAuthenticationProvider> _authenticationProviders = new Map<string, IAuthenticationProvider>();
    List<IPermissionsManager> _permissionsManagers = new List<IPermissionsManager>();


    object _typeDefsLock = new object();

    bool _warehouseIsOpen = false;

    public delegate void StoreEvent(IStore store);
    public event StoreEvent StoreConnected;
    public event StoreEvent StoreDisconnected;

    public delegate AsyncReply<IStore> ProtocolInstance(string name, IResourceContext resourceContext);

    public KeyList<string, ProtocolInstance> Protocols { get; } = new KeyList<string, ProtocolInstance>();

    private Regex urlRegex = new Regex(@"^(?:([\S]*)://([^/]*)/?)");


    public void RegisterAuthenticationProvider(IAuthenticationProvider provider)
    {
        RegisterAuthenticationProvider(provider.DefaultName, provider);
    }

    public void RegisterAuthenticationProvider(string name, IAuthenticationProvider provider)
    {
        _authenticationProviders.Add(name, provider);
    }


    public void RegisterPermissionsManager(IPermissionsManager manager)
    {
        _permissionsManagers.Add(manager);
    }

    public IAuthenticationProvider GetAuthenticationProvider(string name)
    {
        if (_authenticationProviders.ContainsKey(name))
            return _authenticationProviders[name];
        throw new Exception("Authentication provider not found.");
    }

    public IAuthenticationProvider? TryGetAuthenticationProvider(string name)
    {
        if (_authenticationProviders.ContainsKey(name))
            return _authenticationProviders[name];

        return null;
    }


    public Warehouse()
    {
        Protocols.Add("EP",
            async (name, context)
            => await New<EpConnection>(name, context));

        //new LocalTypeDef(typeof(EpAuthPacketIAuthHeader), this);
        //new LocalTypeDef(typeof(EpAuthPacketIAuthDestination), this);
        //new LocalTypeDef(typeof(EpAuthPacketIAuthFormat), this);
    }


    /// <summary>
    /// Get a store by its name.
    /// </summary>
    /// <param name="name">Store instance name</param>
    /// <returns></returns>
    public IStore GetStore(string name)
    {
        foreach (var s in _stores)
            if (s.Key.Instance.Name == name)
                return s.Key;
        return null;
    }

    public WeakReference<IResource>[] Resources => _resources.Values.ToArray();

    /// <summary>
    /// Get a resource by instance Id.
    /// </summary>
    /// <param name="id">Instance Id</param>
    /// <returns></returns>
    public AsyncReply<IResource> GetById(uint id)
    {
        if (_resources.ContainsKey(id))
        {
            IResource r;
            if (_resources[id].TryGetTarget(out r))
                return new AsyncReply<IResource>(r);
            else
                return new AsyncReply<IResource>(null);
        }
        else
            return new AsyncReply<IResource>(null);
    }

    //void LoadGenerated()
    //{
    //    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
    //    {
    //        var generatedType = assembly.GetType("Esiur.Generated");
    //        if (generatedType != null)
    //        {
    //            var resourceTypes = (Type[])generatedType.GetProperty("Resources").GetValue(null);
    //            foreach (var t in resourceTypes)
    //            {
    //                RegisterTypeDef(new TypeDef(t));
    //            }

    //            var recordTypes = (Type[])generatedType.GetProperty("Records").GetValue(null);
    //            foreach (var t in recordTypes)
    //            {
    //                RegisterTypeDef(new TypeDef(t));
    //            }

    //            var enumsTypes = (Type[])generatedType.GetProperty("Enums").GetValue(null);
    //            foreach (var t in enumsTypes)
    //            {
    //                RegisterTypeDef(new TypeDef(t));
    //            }
    //        }
    //    }
    //}

    /// <summary>
    /// Open the warehouse.
    /// This function issues the initialize trigger to all stores and resources.
    /// </summary>
    /// <returns>True, if no problem occurred.</returns>
    public async AsyncReply<bool> Open()
    {
        if (_warehouseIsOpen)
            return false;

        // Load generated models
        //LoadGenerated();


        _warehouseIsOpen = true;

        var resSnap = _resources.Select(x =>
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
            var rt = await r.Handle(ResourceOperation.Initialize);
            //if (!rt)
            //  return false;

            if (!rt)
            {
                Global.Log("Warehouse", LogType.Warning, $"Resource failed at Initialize {r.Instance.Name} [{r.Instance.Definition.Name}]");
            }
            //}
        }

        foreach (var r in resSnap)
        {
            var rt = await r.Handle(ResourceOperation.SystemReady);
            if (!rt)
            {
                Global.Log("Warehouse", LogType.Warning, $"Resource failed at SystemInitialized {r.Instance.Name} [{r.Instance.Definition.Name}]");
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

        foreach (var resource in _resources.Values)
        {
            IResource r;
            if (resource.TryGetTarget(out r))
            {
                if (!(r is IStore))
                    bag.Add(r.Handle(ResourceOperation.Terminate));

            }
        }

        foreach (var store in _stores)
            bag.Add(store.Key.Handle(ResourceOperation.Terminate));


        foreach (var resource in _resources.Values)
        {
            IResource r;
            if (resource.TryGetTarget(out r))
            {
                if (!(r is IStore))
                    bag.Add(r.Handle(ResourceOperation.SystemTerminated));
            }
        }


        foreach (var store in _stores)
            bag.Add(store.Key.Handle(ResourceOperation.SystemTerminated));

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

        foreach (var store in _stores.Keys)
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
    public async AsyncReply<T> Get<T>(string path, IResourceContext resourceContext = null)
        where T : IResource
    {

        if (urlRegex.IsMatch(path))
        {

            var url = urlRegex.Split(path);

            if (Protocols.ContainsKey(url[1]))
            {
                if (!_warehouseIsOpen)
                    await Open();

                var handler = Protocols[url[1]];
                var store = await handler(url[2], resourceContext);

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
    public async AsyncReply<T> Put<T>(string path, T resource, IResourceContext resourceContext = null) where T : IResource
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

        var resourceId = (uint)Interlocked.Increment(ref _resourceCounter);

        resource.Instance = new Instance(this, resourceId, instanceName, resource, store, resourceContext?.Age ?? 0);

        if (resourceContext?.Attributes != null)
            resource.Instance.SetAttributes(resourceContext.Attributes);

        try
        {
            if (resource is IStore)
                _stores.TryAdd(resource as IStore, new List<WeakReference<IResource>>());
            else if ((IResource)resource != store)
            {
                if (!await store.Put(resource, string.Join("/", location.Skip(1).ToArray())))
                    throw new Exception("Store failed to put the resource.");
            }

            var t = resource.GetType();
            Global.Counters["T-" + t.Namespace + "." + t.Name]++;

            _resources.TryAdd(resource.Instance.Id, resourceReference);

            if (_warehouseIsOpen)
            {
                await resource.Handle(ResourceOperation.Initialize, resourceContext);
                if (resource is IStore)
                    await resource.Handle(ResourceOperation.Open, resourceContext);
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

    public T Create<T>(Map<string, object> properties = null)
    {
        return (T)Create(typeof(T), properties);
    }

    public IResource CreateFromIndexedProperties(Type type, Map<byte, object> properties)
    {
        type = ResourceProxy.GetProxy(type);
        var res = Activator.CreateInstance(type) as IResource;

        if (properties != null)
        {
            var typeDef = GetLocalTypeDefByType(type);
            foreach (var p in properties)
            {
                var pi = typeDef.GetPropertyDefByIndex(p.Key).PropertyInfo;

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
            }
        }


        return res;
    }

    public IResource Create(Type type, Map<string, object> properties)
    {
        type = ResourceProxy.GetProxy(type);
        var res = Activator.CreateInstance(type) as IResource;

        if (properties != null)
        {

            foreach (var p in properties)
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

        return res;
    }

    public async AsyncReply<IResource> New(Type type, string path, IResourceContext resourceContext)
    {
        var res = Create(type, resourceContext?.Properties);
        return await Put(path, res, resourceContext);
    }

    public async AsyncReply<T> New<T>(string path, IResourceContext resourceContext = null)
        where T : IResource
    {
        return (T)(await New(typeof(T), path, resourceContext));
    }


    public Type TryGetProxyType(TypeDefKind kind, string domain, string name)
    {
        if (!_proxyTypeDefs.ContainsKey(domain))
            return null;

        if (!_proxyTypeDefs[domain].ContainsKey(kind))
            return null;

        if (!_proxyTypeDefs[domain][kind].ContainsKey(name))
            return null;

        return _proxyTypeDefs[domain][kind][name];
    }

    public Type GetProxyType(TypeDefKind kind, string domain, string name)
    {
        if (!_proxyTypeDefs.ContainsKey(domain))
            throw new Exception($"No proxy types registered for domain {domain}.");


        if (!_proxyTypeDefs[domain].ContainsKey(kind))
            throw new Exception($"No proxy types registered for kind {kind} in domain {domain}.");

        if (!_proxyTypeDefs[domain][kind].ContainsKey(name))
           throw new Exception($"No proxy type registered with name {name} for kind {kind} in domain {domain}.");


        return _proxyTypeDefs[domain][kind][name];
    }


    public void RegisterProxyType(Type type)
    {
        // make sure the type has remote attribute
        var remoteAttr = type.GetCustomAttribute<RemoteAttribute>();

        if (remoteAttr == null)
            throw new Exception("Proxy type must have Remote attribute.");

        //@TODO should add this check t the RemoteAttribute class and use it here, but for now, we will just check the domain and full name format here.
        if (!remoteAttr.AreValidDomains())
            throw new Exception("Invalid domain in Remote attribute.");

        if (!remoteAttr.IsValidFullName())
            throw new Exception("Invalid full name in Remote attribute.");


        // make sure the type implements IResource or IRecord
        if (Codec.ImplementsInterface(type, typeof(IRecord)))
        {
            foreach (var domain in remoteAttr.Domains)
            {
                if (!_proxyTypeDefs.ContainsKey(domain))
                    _proxyTypeDefs.Add(domain, new KeyList<TypeDefKind, KeyList<string, Type>>());

                if (!_proxyTypeDefs[domain].ContainsKey(TypeDefKind.Record))
                    _proxyTypeDefs[domain][TypeDefKind.Record] = new KeyList<string, Type>();

                _proxyTypeDefs[domain][TypeDefKind.Record][remoteAttr.FullName] = type;
            }
        }
        else if (Codec.InheritsClass(type, typeof(EpResource)))
        {
            foreach (var domain in remoteAttr.Domains)
            {

                if (!_proxyTypeDefs.ContainsKey(domain))
                    _proxyTypeDefs.Add(domain, new KeyList<TypeDefKind, KeyList<string, Type>>());

                if (!_proxyTypeDefs[domain].ContainsKey(TypeDefKind.Resource))
                    _proxyTypeDefs[domain][TypeDefKind.Resource] = new KeyList<string, Type>();

                _proxyTypeDefs[domain][TypeDefKind.Resource][remoteAttr.FullName] = type;
            }
        }
        else if (type.IsEnum)
        {
            foreach (var domain in remoteAttr.Domains)
            {
                if (!_proxyTypeDefs.ContainsKey(domain))
                    _proxyTypeDefs.Add(domain, new KeyList<TypeDefKind, KeyList<string, Type>>());

                if (!_proxyTypeDefs[domain].ContainsKey(TypeDefKind.Enum))
                    _proxyTypeDefs[domain][TypeDefKind.Enum] = new KeyList<string, Type>();

                _proxyTypeDefs[domain][TypeDefKind.Enum][remoteAttr.FullName] = type;
            }
        }
        else
        {
            throw new Exception("Proxy type must implement IResource or IRecord or be an enum.");
        }
    }

    /// <summary>
    /// Register TypeDef.
    /// </summary>
    /// <param name="typeDef">Resource type definition.</param>
    public uint RegisterLocalTypeDef(LocalTypeDef typeDef)
    {
        lock (_typeDefsLock)
        {
            //if (_localTypeDefs[typeDef.Kind].ContainsKey(typeDef.Id))
            if (_localTypeDefs.ContainsKey(typeDef.Id))
                throw new Exception($"TypeDef with same class Id already exists. {_localTypeDefs[typeDef.Id].Name} -> {typeDef.Name}");
            //throw new Exception($"TypeDef with same class Id already exists. {_localTypeDefs[typeDef.Kind][typeDef.Id].Name} -> {typeDef.Name}");

            var typeDefId = (uint)Interlocked.Increment(ref _typeDefsCounter);

            typeDef.Id = typeDefId;

            //_localTypeDefs[typeDef.Kind][typeDef.Id] = typeDef;
            _localTypeDefs[typeDef.Id] = typeDef;

            return typeDefId;
        }
    }

    public bool TryRegisterLocalTypeDef(LocalTypeDef typeDef)
    {
        lock (_typeDefsLock)
        {
            if (_localTypeDefs.ContainsKey(typeDef.Id))
                return false;

            var typeDefId = (uint)Interlocked.Increment(ref _typeDefsCounter);
            typeDef.Id = typeDefId;

            _localTypeDefs[typeDef.Id] = typeDef;

            return true;
        }
    }

    internal KeyList<TypeDefKind, KeyList<string , Type>> GetProxyTypesByDomain(string domain)
    {
        return _proxyTypeDefs[domain];
    }

    public bool TryRegisterRemoteTypeDef(string domain, RemoteTypeDef typeDef)
    {
        lock (_typeDefsLock)
        {
            if (!_remoteTypeDefs.ContainsKey(domain))
            {
                _remoteTypeDefs.Add(domain, new KeyList<ulong, RemoteTypeDef>());
            }

            if (_remoteTypeDefs[domain].ContainsKey(typeDef.Id))
                return false;

            // @TODO: Try to find a proxy type for the remote type def, if not found, create a new proxy type and register it in the warehouse.
            _remoteTypeDefs[domain][typeDef.Id] = typeDef;

            var localTypeDefId = (uint)Interlocked.Increment(ref _typeDefsCounter);
            typeDef.LocalTypeDefId = localTypeDefId;
            _localTypeDefs[localTypeDefId] = typeDef;

            return true;
        }
    }


    //public TypeDef FindTypeDefByType(Type type)
    //{
    //    var remote = type.GetCustomAttribute<RemoteAttribute>();

    //    if (remote != null)
    //    {
    //        return GetRemoteTypeDefByName(remote.Domain, remote.FullName);
    //    }
    //    else
    //    {
    //        return GetLocalTypeDefByType(type);
    //    }
    //}

    //public TypeDef FindTypeDefByTypeDefId(TypeDefId typeDefId, string domain)
    //{
    //    if (typeDefId.Remote)
    //    {
    //        return GetRemoteTypeDefById(domain, typeDefId.Value);
    //    }
    //    else
    //    {
    //        return GetLocalTypeDefById(typeDefId.Value);
    //    }
    //}


    /// <summary>
    /// Get a TypeDef by type from the warehouse. If not in the warehouse, a new TypeDef is created and added to the warehouse.
    /// </summary>
    /// <param name="type">.Net type.</param>
    /// <returns>Resource TypeDef.</returns>
    public TypeDef GetLocalTypeDefByType(Type type)
    {
        //if (!(type.IsClass || type.IsEnum))
        //    return null;

        var baseType = ResourceProxy.GetBaseType(type);

        if (baseType == typeof(IResource)
            || baseType == typeof(IRecord))
            return null;

        // Only resources, records and enums have type definitions; bail out for anything else.
        if (!Codec.ImplementsInterface(type, typeof(IResource))
            && !Codec.ImplementsInterface(type, typeof(IRecord))
            && !type.IsEnum)
            return null;

        lock (_typeDefsLock)
        {
            //var typeDef = _localTypeDefs.Values.FirstOrDefault(x => x is LocalTypeDef ltd 
            //                                                    && ltd.DefinedType == baseType);

            foreach (var td in _localTypeDefs.Values)
            {
                if (td is LocalTypeDef ltd && ltd.DefinedType == baseType)
                {
                    return td;
                }
                else if (td is RemoteTypeDef rtd && rtd.ProxyType == baseType)
                {
                    return td;
                }
            }

            //if (typeDef != null)
            //    return typeDef;

            // create new TypeDef for type

            //Console.WriteLine($"Creating {baseType.Name}");

            var ntd = new LocalTypeDef(baseType, this);
            //LocalTypeDef.GetDependencies(ntd, this);
            return ntd;
        }
    }

    /// <summary>
    /// Get a TypeDef by TypeId from the warehouse. If not in the warehouse, a new TypeDef is created and added to the warehouse.
    /// </summary>
    /// <param name="typeId">typeId.</param>
    /// <returns>TypeDef.</returns>
    public TypeDef GetLocalTypeDefById(ulong typeId)
    {
        return _localTypeDefs[typeId];
    }


    public RemoteTypeDef GetRemoteTypeDefById(string domain, ulong typeId)
    {
        if (string.IsNullOrEmpty(domain) || !_remoteTypeDefs.ContainsKey(domain))
            return null;
        return _remoteTypeDefs[domain][typeId];

    }

    public TypeDef GetRemoteTypeDefByName(string domain, string typeName, TypeDefKind? typeDefKind = null)
    {
        if (!string.IsNullOrEmpty(domain) || !_remoteTypeDefs.ContainsKey(domain))
            return null;

        return _remoteTypeDefs[domain].Values.FirstOrDefault(x => x.Name == typeName);
    }

    public TypeDef GetProxyTypeDef(string domain, string typeName, TypeDefKind? typeDefKind = null)
    {
        if (!string.IsNullOrEmpty(domain) || !_remoteTypeDefs.ContainsKey(domain))
            return null;
        sdcsdc
        return _remoteTypeDefs[domain].Values.FirstOrDefault(x => x.Name == typeName);
    }

    //public TypeDef GetRemoteTypeDefByType(Type type)
    //{
    //    var remoteAttr = type.GetCustomAttribute<RemoteAttribute>();

    //    if (remoteAttr == null) return null;

    //    return GetRemoteTypeDefByName(remoteAttr.Domain, remoteAttr.FullName);
    //}

    /// <summary>
    /// Get a TypeDef by type name . If not in the warehouse, a new TypeDef is created and added to the warehouse.
    /// </summary>
    /// <param name="typeName">Class full name.</param>
    /// <returns>TypeDef.</returns>
    public TypeDef GetLocalTypeDefByName(string typeName)
    {
        return _localTypeDefs.Values.FirstOrDefault(x => x.Name == typeName);
    }

    public bool Remove(IResource resource)
    {
        if (resource.Instance == null)
            return false;

        WeakReference<IResource> resourceReference;

        if (_resources.ContainsKey(resource.Instance.Id))
            _resources.TryRemove(resource.Instance.Id, out resourceReference);
        else
            return false;


        if (resource != resource.Instance.Store)
        {
            List<WeakReference<IResource>> list;
            if (_stores.TryGetValue(resource.Instance.Store, out list))
            {

                lock (((ICollection)list).SyncRoot)
                    list.Remove(resourceReference);

            }
        }
        if (resource is IStore)
        {
            var store = resource as IStore;

            List<WeakReference<IResource>> toBeRemoved;

            _stores.TryRemove(store, out toBeRemoved);


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
