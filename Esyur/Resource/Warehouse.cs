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
using Esyur.Proxy;
using Esyur.Resource.Template;
using Esyur.Security.Permissions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Esyur.Net.IIP;
using System.Text.RegularExpressions;
using Esyur.Misc;
using System.Collections.Concurrent;

namespace Esyur.Resource
{
    // Centeral Resource Issuer
    public static class Warehouse
    {
        //static byte prefixCounter;

        //static AutoList<IStore, Instance> stores = new AutoList<IStore, Instance>(null);
        static ConcurrentDictionary<uint, WeakReference<IResource>> resources = new ConcurrentDictionary<uint, WeakReference<IResource>>();
        static ConcurrentDictionary<IStore, List<WeakReference<IResource>>> stores = new ConcurrentDictionary<IStore, List<WeakReference<IResource>>>();


        static uint resourceCounter = 0;

        static KeyList<Guid, ResourceTemplate> templates = new KeyList<Guid, ResourceTemplate>();

        static bool warehouseIsOpen = false;

        public delegate void StoreConnectedEvent(IStore store, string name);
        public delegate void StoreDisconnectedEvent(IStore store);

        public static event StoreConnectedEvent StoreConnected;
        public static event StoreDisconnectedEvent StoreDisconnected;

        public static KeyList<string, Func<IStore>> Protocols { get; } = getSupportedProtocols();

        private static Regex urlRegex = new Regex(@"^(?:([\S]*)://([^/]*)/?)");

        //private static object resourcesLock = new object();

        static KeyList<string, Func<IStore>> getSupportedProtocols()
        {
            var rt = new KeyList<string, Func<IStore>>();
            rt.Add("iip", () => new DistributedConnection());
            return rt;
        }

        /// <summary>
        /// Get a store by its name.
        /// </summary>
        /// <param name="name">Store instance name</param>
        /// <returns></returns>
        public static IStore GetStore(string name)
        {
            foreach (var s in stores)
                if (s.Key.Instance.Name == name)
                    return s.Key;
            return null;
        }

        public static WeakReference<IResource>[] Resources => resources.Values.ToArray();

        /// <summary>
        /// Get a resource by instance Id.
        /// </summary>
        /// <param name="id">Instance Id</param>
        /// <returns></returns>
        public static AsyncReply<IResource> GetById(uint id)
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

        /// <summary>
        /// Open the warehouse.
        /// This function issues the initialize trigger to all stores and resources.
        /// </summary>
        /// <returns>True, if no problem occurred.</returns>
        public static async AsyncReply<bool> Open()
        {
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
                    Console.WriteLine($"Resource failed at Initialize {r.Instance.Name} [{r.Instance.Template.ClassName}]");
                }
                //}
            }

            foreach (var r in resSnap)
            {
                //IResource r;
                //if (rk.Value.TryGetTarget(out r))
                //{
                var rt = await r.Trigger(ResourceTrigger.SystemInitialized);
                if (!rt)
                {
                    Console.WriteLine($"Resource failed at SystemInitialized {r.Instance.Name} [{r.Instance.Template.ClassName}]");
                }
                //return false;
                //}
            }


            return true;

            /*
            var bag = new AsyncBag<bool>();

            //foreach (var store in stores)
            //    bag.Add(store.Trigger(ResourceTrigger.Initialize));


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

                var rBag = new AsyncBag<bool>();
                foreach (var rk in resources)
                {
                    IResource r;
                    if (rk.Value.TryGetTarget(out r))
                        rBag.Add(r.Trigger(ResourceTrigger.Initialize));
                }

                rBag.Seal();

                rBag.Then(y =>
                {
                    foreach (var b in y)
                        if (!b)
                        {
                            rt.Trigger(false);
                            return;
                        }

                    rt.Trigger(true);
                    warehouseIsOpen = true;
                });

            });


            return rt;
            */
        }

        /// <summary>
        /// Close the warehouse.
        /// This function issues terminate trigger to all resources and stores.
        /// </summary>
        /// <returns>True, if no problem occurred.</returns>
        public static AsyncReply<bool> Close()
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


        /*
        private static IResource[] QureyIn(string[] path, int index, IEnumerable<IResource> resources)// AutoList<IResource, Instance> resources)
        {
            var rt = new List<IResource>();

            if (index == path.Length - 1)
            {
                if (path[index] == "")
                    foreach (IResource child in resources)
                      rt.Add(child);
                 else
                    foreach (IResource child in resources)
                        if (child.Instance.Name == path[index])
                            rt.Add(child);
            }
            else
                foreach (IResource child in resources)
                    if (child.Instance.Name == path[index])
                        rt.AddRange(QureyIn(path, index+1, child.Instance.Children<IResource>()));

            return rt.ToArray();
        }

        public static AsyncReply<IResource[]> Query(string path)
        {
            if (path == null || path == "")
            {
                var roots = stores.Where(s => s.Instance.Parents<IResource>().Count() == 0).ToArray();
                return new AsyncReply<IResource[]>(roots);
            }
            else
            {
                var rt = new AsyncReply<IResource[]>();
                Get(path).Then(x =>
                {
                    var p = path.Split('/');

                    if (x == null)
                    {
                        rt.Trigger(QureyIn(p, 0, stores));
                    }
                    else
                    {
                        var ar = QureyIn(p, 0, stores).Where(r => r != x).ToList();
                        ar.Insert(0, x);
                        rt.Trigger(ar.ToArray());
                    }
                });

                return rt;

            }

        }
        */



        public static async AsyncReply<IResource[]> Query(string path)
        {
            var rt = new AsyncReply<IResource[]>();

            var p = path.Trim().Split('/');
            IResource resource;

            foreach (var store in stores.Keys)
                if (p[0] == store.Instance.Name)
                {

                    if (p.Length == 1)
                        return new IResource[] { store };

                    var res = await store.Get(String.Join("/", p.Skip(1).ToArray()));
                    if (res != null)
                        return new IResource[] { res };


                    resource = store;
                    for (var i = 1; i < p.Length; i++)
                    {
                        var children = await resource.Instance.Children<IResource>(p[i]);
                        if (children.Length > 0)
                        {
                            if (i == p.Length - 1)
                                return children;
                            else
                                resource = children[0];
                        }
                        else
                            break;
                    }

                    return null;
                }



            return null;
        }

        /// <summary>
        /// Get a resource by its path.
        /// Resource path is sperated by '/' character, e.g. "system/http".
        /// </summary>
        /// <param name="path"></param>
        /// <returns>Resource instance.</returns>
        public static AsyncReply<IResource> Get(string path, object attributes = null, IResource parent = null, IPermissionsManager manager = null)
        {
            var rt = new AsyncReply<IResource>();

            // Should we create a new store ?

            if (urlRegex.IsMatch(path))
            {

                //if (path.Contains("://"))
                //{
                var url = urlRegex.Split(path);
                //var url = path.Split(new string[] { "://" }, 2, StringSplitOptions.None);
                //var hostname = url[1].Split(new char[] { '/' }, 2)[0];
                //var pathname = string.Join("/", url[1].Split(new char[] { '/' }).Skip(1));


                if (Protocols.ContainsKey(url[1]))
                {
                    var handler = Protocols[url[1]];

                    var store = handler();
                    Put(store, url[2], null, parent, null, 0, manager, attributes);


                    store.Trigger(ResourceTrigger.Open).Then(x =>
                    {

                        warehouseIsOpen = true;

                        if (url[3].Length > 0 && url[3] != "")
                            store.Get(url[3]).Then(r =>
                            {
                                rt.Trigger(r);
                            }).Error(e => rt.TriggerError(e));
                        else
                            rt.Trigger(store);
                    }).Error(e =>
                    {
                        rt.TriggerError(e);
                        Warehouse.Remove(store);
                    });

                    return rt;
                }
            }


            Query(path).Then(rs =>
            {
                //                rt.TriggerError(new Exception());
                if (rs != null && rs.Length > 0)
                    rt.Trigger(rs.First());
                else
                    rt.Trigger(null);
            });

            return rt;


        }

        /// <summary>
        /// Put a resource in the warehouse.
        /// </summary>
        /// <param name="resource">Resource instance.</param>
        /// <param name="name">Resource name.</param>
        /// <param name="store">IStore that manages the resource. Can be null if the resource is a store.</param>
        /// <param name="parent">Parent resource. if not presented the store becomes the parent for the resource.</param>
        public static void Put(IResource resource, string name, IStore store = null, IResource parent = null, ResourceTemplate customTemplate = null, ulong age = 0, IPermissionsManager manager = null, object attributes = null)
        {
            if (resource.Instance != null)
                throw new Exception("Resource has a store.");

            var resourceReference = new WeakReference<IResource>(resource);

            if (store == null)
            {
                // assign parent as a store
                if (parent is IStore)
                {
                    store = (IStore)parent;
                    stores[store].Add(resourceReference);
                }
                // assign parent's store as a store
                else if (parent != null)
                {
                    store = parent.Instance.Store;
                    stores[store].Add(resourceReference);
                }
                // assign self as a store (root store)
                else if (resource is IStore)
                {
                    store = (IStore)resource;
                }
                else
                    throw new Exception("Can't find a store for the resource.");
            }

            resource.Instance = new Instance(resourceCounter++, name, resource, store, customTemplate, age);

            if (attributes != null)
                resource.Instance.SetAttributes(Structure.FromObject(attributes));

            if (manager != null)
                resource.Instance.Managers.Add(manager);

            if (store == parent)
                parent = null;


            /*
            if (parent == null)
            {
                if (!(resource is IStore))
                    store.Instance.Children.Add(resource);
            }
            else
                parent.Instance.Children.Add(resource);
               */


            if (resource is IStore)
            {
                stores.TryAdd(resource as IStore, new List<WeakReference<IResource>>());
                StoreConnected?.Invoke(resource as IStore, name);
            }
            //else


            store.Put(resource);


            if (parent != null)
            {
                parent.Instance.Store.AddChild(parent, resource);
                store.AddParent(resource, parent);
                //store.AddChild(parent, resource);

            }

            var t = resource.GetType();
            Global.Counters["T-" + t.Namespace + "." + t.Name]++;

            //var wr = new WeakReference<IResource>(resource);

            //lock (resourcesLock)
            resources.TryAdd(resource.Instance.Id, resourceReference);

            if (warehouseIsOpen)
                resource.Trigger(ResourceTrigger.Initialize);

        }

        public static IResource New(Type type, string name, IStore store = null, IResource parent = null, IPermissionsManager manager = null, object attributes = null, object properties = null)
        {
            type = ResourceProxy.GetProxy(type);


            /*
            if (arguments != null)
            {
                var constructors = type.GetConstructors(System.Reflection.BindingFlags.Public);
                
                foreach(var constructor in constructors)
                {
                    var pi = constructor.GetParameters();
                    if (pi.Length == constructor.le)
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

                constructors[0].
            }
            */
            var res = Activator.CreateInstance(type) as IResource;


            if (properties != null)
            {
                var ps = Structure.FromObject(properties);

                foreach (var p in ps)
                {
                    var pi = type.GetProperty(p.Key, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);
                    if (pi != null)
                        pi.SetValue(res, p.Value);
                }
            }

            if (store != null || parent != null || res is IStore)
                Put(res, name, store, parent, null, 0, manager, attributes);

            return res;

        }

        public static T New<T>(string name, IStore store = null, IResource parent = null, IPermissionsManager manager = null, object attributes = null, object properties = null)
            where T : IResource
        {
            return (T)New(typeof(T), name, store, parent, manager, attributes, properties);
        }

        /// <summary>
        /// Put a resource template in the templates warehouse.
        /// </summary>
        /// <param name="template">Resource template.</param>
        public static void PutTemplate(ResourceTemplate template)
        {
            if (!templates.ContainsKey(template.ClassId))
                templates.Add(template.ClassId, template);
        }


        /// <summary>
        /// Get a template by type from the templates warehouse. If not in the warehouse, a new ResourceTemplate is created and added to the warehouse.
        /// </summary>
        /// <param name="type">.Net type.</param>
        /// <returns>Resource template.</returns>
        public static ResourceTemplate GetTemplate(Type type)
        {
            // loaded ?
            foreach (var t in templates.Values)
                if (t.ClassName == type.FullName)
                    return t;

            var template = new ResourceTemplate(type);
            templates.Add(template.ClassId, template);

            return template;
        }

        /// <summary>
        /// Get a template by class Id from the templates warehouse. If not in the warehouse, a new ResourceTemplate is created and added to the warehouse.
        /// </summary>
        /// <param name="classId">Class Id.</param>
        /// <returns>Resource template.</returns>
        public static AsyncReply<ResourceTemplate> GetTemplate(Guid classId)
        {
            if (templates.ContainsKey(classId))
                return new AsyncReply<ResourceTemplate>(templates[classId]);
            return null;
        }

        /// <summary>
        /// Get a template by class name from the templates warehouse. If not in the warehouse, a new ResourceTemplate is created and added to the warehouse.
        /// </summary>
        /// <param name="className">Class name.</param>
        /// <returns>Resource template.</returns>
        public static AsyncReply<ResourceTemplate> GetTemplate(string className)
        {
            foreach (var t in templates.Values)
                if (t.ClassName == className)
                    return new AsyncReply<ResourceTemplate>(t);

            return null;
        }

        public static bool Remove(IResource resource)
        {

            if (resource.Instance == null)
                return false;

            //lock (resourcesLock)
            //{

            WeakReference<IResource> resourceReference;

            if (resources.ContainsKey(resource.Instance.Id))
                resources.TryRemove(resource.Instance.Id, out resourceReference);
            else
                return false;
            //}

            if (resource != resource.Instance.Store)
                stores[resource.Instance.Store].Remove(resourceReference);

            if (resource is IStore)
            {
                var store = resource as IStore;

                List<WeakReference<IResource>> toBeRemoved;// = stores[store];

                stores.TryRemove(store, out toBeRemoved);

                //lock (resourcesLock)
                //{
                //    // remove all objects associated with the store
                //    toBeRemoved = resources.Values.Where(x =>
                //   {
                //       IResource r;
                //       if (x.TryGetTarget(out r))
                //           return r.Instance.Store == resource;
                //       else
                //           return false;
                //   }).ToArray();
                //}


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

            return true;
        }
    }
}
