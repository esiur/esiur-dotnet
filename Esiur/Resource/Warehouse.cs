using Esiur.Data;
using Esiur.Engine;
using Esiur.Resource.Template;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Resource
{
    // Centeral Resource Issuer
    public static class Warehouse
    {
        //static byte prefixCounter;

        static List<IStore> stores = new List<IStore>();
        static Dictionary<uint, IResource> resources = new Dictionary<uint, IResource>();
        static uint resourceCounter = 0;

        static KeyList<Guid, ResourceTemplate> templates = new KeyList<Guid, ResourceTemplate>();


        /// <summary>
        /// Get a store by its name.
        /// </summary>
        /// <param name="name">Store instance name</param>
        /// <returns></returns>
        public static IStore GetStore(string name)
        {
            foreach (var s in stores)
                if (s.Instance.Name == name)
                    return s;
            return null;
        }

        /// <summary>
        /// Get a resource by instance Id.
        /// </summary>
        /// <param name="id">Instance Id</param>
        /// <returns></returns>
        public static AsyncReply<IResource> Get(uint id)
        {
            if (resources.ContainsKey(id))
                return new AsyncReply<IResource>(resources[id]);
            else
                return null;
        }

        /// <summary>
        /// Open the warehouse.
        /// This function issues the initialize trigger to all stores and resources.
        /// </summary>
        /// <returns>True, if no problem occurred.</returns>
        public static AsyncReply<bool> Open()
        {
            var bag = new AsyncBag<bool>();

            foreach (var store in stores)
                bag.Add(store.Trigger(ResourceTrigger.Initialize));

            foreach (var store in stores)
                bag.Add(store.Trigger(ResourceTrigger.SystemInitialized));

            bag.Seal();

            var rt = new AsyncReply<bool>();
            bag.Then((x) =>
            {
                foreach(var b in x)
                    if (!b)
                    {
                        rt.Trigger(false);
                        return;
                    }

                rt.Trigger(true);
            });

            return rt;
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
                if (!(resource is IStore))
                    bag.Add(resource.Trigger(ResourceTrigger.Terminate));

            foreach (var store in stores)
                bag.Add(store.Trigger(ResourceTrigger.Terminate));

            foreach (var resource in resources.Values)
                if (!(resource is IStore))
                    bag.Add(resource.Trigger(ResourceTrigger.SystemTerminated));

            foreach (var store in stores)
                bag.Add(store.Trigger(ResourceTrigger.SystemTerminated));

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

        /// <summary>
        /// Get a resource by its path.
        /// Resource path is sperated by '/' character, e.g. "system/http".
        /// </summary>
        /// <param name="path"></param>
        /// <returns>Resource instance.</returns>
        public static AsyncReply<IResource> Get(string path)
        {

            var p = path.Split('/');
            IResource res;

            foreach(IStore d in stores)
                if (p[0] == d.Instance.Name)
                {
                    var i = 1;
                    res = d;
                    while(p.Length > i)
                    {
                        var si = i;

                        foreach (IResource r in res.Instance.Children)
                            if (r.Instance.Name == p[i])
                            {
                                i++;
                                res = r;
                                break;
                            }

                        if (si == i)
                            // not found, ask the store
                            return d.Get(path.Substring(p[0].Length + 1));
                    }

                    return new AsyncReply<IResource>(res);
                }

            return new AsyncReply<IResource>(null);
        }

        /// <summary>
        /// Put a resource in the warehouse.
        /// </summary>
        /// <param name="resource">Resource instance.</param>
        /// <param name="name">Resource name.</param>
        /// <param name="store">IStore that manages the resource. Can be null if the resource is a store.</param>
        /// <param name="parent">Parent resource. if not presented the store becomes the parent for the resource.</param>
        public static void Put(IResource resource, string name, IStore store = null, IResource parent = null)
        {
            resource.Instance = new Instance(resourceCounter++, name, resource, store);

            if (store == parent)
                parent = null;

            if (parent == null)
            {
                if (!(resource is IStore))
                    store.Instance.Children.Add(resource);
            }
            else
                parent.Instance.Children.Add(resource);


            if (resource is IStore)
                stores.Add(resource as IStore);
            else
                store.Put(resource);

            resources.Add(resource.Instance.Id, resource);
        }

        public static T New<T>(string name, IStore store = null, IResource parent = null)
        {
            var res = Activator.CreateInstance(typeof(T)) as IResource;
            Put(res, name, store, parent);
            return (T)res;
        }

        /// <summary>
        /// Put a resource template in the templates warehouse.
        /// </summary>
        /// <param name="template">Resource template.</param>
        public static void PutTemplate(ResourceTemplate template)
        {
            if (templates.ContainsKey(template.ClassId))
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
    }
}
