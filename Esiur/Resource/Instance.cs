using System;
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

namespace Esiur.Resource
{
    public class Instance
    {
        string name;

        AutoList<IResource, Instance> children;// = new AutoList<IResource, Instance>();
        IResource resource;
        IStore store;
        AutoList<IResource, Instance> parents;// = new AutoList<IResource>();
        bool inherit;
        ResourceTemplate template;

        AutoList<IPermissionManager, Instance> managers;// = new AutoList<IPermissionManager, Instance>();

        public delegate void ResourceModifiedEvent(IResource resource, string propertyName, object newValue, object oldValue);
        public delegate void ResourceEventOccurredEvent(IResource resource, string eventName, string[] receivers, object[] args);
        public delegate void ResourceDestroyedEvent(IResource resource);

        public event ResourceModifiedEvent ResourceModified;
        public event ResourceEventOccurredEvent ResourceEventOccured;
        public event ResourceDestroyedEvent ResourceDestroyed;

        KeyList<string, object> attributes = new KeyList<string, object>();

        List<uint> ages = new List<uint>();
        private uint age;

        uint id;

 
        /// <summary>
        /// Instance attributes are custom properties associated with the instance, a place to store information by IStore.
        /// </summary>
        public KeyList<string, object> Attributes
        {
            get
            {
                return attributes;
            }
        }

        /// <summary>
        /// Get the age of a given property index.
        /// </summary>
        /// <param name="index">Zero-based property index.</param>
        /// <returns>Age.</returns>
        public uint GetAge(byte index)
        {
            if (index < ages.Count)
                return ages[index];
            else
                return 0;
        }

        /// <summary>
        /// Age of the instance, increments by 1 in every modification.
        /// </summary>
        public uint Age
        {
            get { return age; }
            internal set { age = value; }
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
        public bool Deserialize(object[] properties)
        { 
            foreach (var pt in template.Properties)
            {
#if NETSTANDARD1_5
                var pi = resource.GetType().GetTypeInfo().GetProperty(pt.Name);
#else
                var pi = resource.GetType().GetProperty(pt.Name);
#endif
                if (!(properties[pt.Index] is NotModified))
                    pi.SetValue(resource, properties[pt.Index]);
            }
            return true;
        }

        /// <summary>
        /// Export all properties with ResourceProperty attributed as bytes array.
        /// </summary>
        /// <returns></returns>
        public object[] Serialize()
        {
            List<object> props = new List<object>();

            foreach (var pt in template.Properties)
            {
#if NETSTANDARD1_5
                var pi = resource.GetType().GetTypeInfo().GetProperty(pt.Name);
#else
                var pi = resource.GetType().GetProperty(pt.Name);
#endif
                var rt = pi.GetValue(resource, null);
                props.Add(rt);
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
#if NETSTANDARD1_5
            var attrs = resource.GetType().GetTypeInfo().GetCustomAttributes(typeof(Storable), true).ToArray();
#else
            var attrs = resource.GetType().GetCustomAttributes(typeof(Storable), true);
#endif
            return attrs.Length > 0;

        }


        /// <summary>
        /// Notify listeners that a property was modified.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="newValue"></param>
        /// <param name="oldValue"></param>
        public void Modified([CallerMemberName] string propertyName = "", object newValue = null, object oldValue = null)
        {
            if (newValue == null)
            {
                object val;
                if (GetPropertyValue(propertyName, out val))
                    ResourceModified?.Invoke(resource, propertyName, val, oldValue);
            }
            else
                ResourceModified?.Invoke(resource, propertyName, newValue, oldValue);
        }

        internal void EmitResourceEvent(string name, string[] receivers, object[] args)
        {
            ResourceEventOccured?.Invoke(resource, name, receivers, args);
        }

        /// <summary>
        /// Get the value of a given property by name.
        /// </summary>
        /// <param name="name">Property name</param>
        /// <param name="value">Output value</param>
        /// <returns>True, if the resource has the property.</returns>
        public bool GetPropertyValue(string name, out object value)
        {
#if NETSTANDARD1_5
            PropertyInfo pi = resource.GetType().GetTypeInfo().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

#else
            PropertyInfo pi = resource.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
#endif

            if (pi != null)
            {

#if NETSTANDARD1_5
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
            }

            value = null;
            return false;
        }


        public bool Inherit
        {
            get { return inherit; }
        }

        /// <summary>
        /// List of parents.
        /// </summary>
        public AutoList<IResource, Instance> Parents
        {
            get { return parents; }
        }

        /// <summary>
        /// Store responsible for creating and keeping the resource.
        /// </summary>
        public IStore Store
        {
            get { return store; }
        }
        
        /// <summary>
        /// List of children.
        /// </summary>
        public AutoList<IResource, Instance> Children
        {
            get { return children; }
        }

        /// <summary>
        /// The unique and permanent link to the resource.
        /// </summary>
        public string Link
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

        /// <summary>
        /// Instance name.
        /// </summary>
        public string Name
        {
            get { return name; }
        }


        /// <summary>
        /// Resource managed by this instance.
        /// </summary>
        public IResource Resource
        {
            get { return resource; }
        }

        /// <summary>
        /// Resource template describes the properties, functions and events of the resource.
        /// </summary>
        public ResourceTemplate Template
        {
            get { return template; }
        }

       /// <summary>
       /// Create new instance.
       /// </summary>
       /// <param name="id">Instance Id.</param>
       /// <param name="name">Name of the instance.</param>
       /// <param name="resource">Resource to manage.</param>
       /// <param name="store">Store responsible for the resource.</param>
        public Instance(uint id, string name, IResource resource, IStore store)
        {
            this.store = store;
            this.resource = resource;
            this.id = id;
            this.name = name;

            children = new AutoList<IResource, Instance>(this);
            parents = new AutoList<IResource, Instance>(this);
            managers = new AutoList<IPermissionManager, Instance>(this);
            children.OnAdd += Children_OnAdd;
            children.OnRemoved += Children_OnRemoved;

            resource.OnDestroy += Resource_OnDestroy;

            template = Warehouse.GetTemplate(resource.GetType());


            // set ages
            for (byte i = 0; i < template.Properties.Length; i++)
                ages.Add(0);


            // connect events
            Type t = resource.GetType();

#if NETSTANDARD1_5
            var events = t.GetTypeInfo().GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

#else
            var events = t.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
#endif

            foreach (var evt in events)
            {
                if (evt.EventHandlerType != typeof(ResourceEventHanlder))
                    continue;

                var ca = (ResourceEvent[])evt.GetCustomAttributes(typeof(ResourceEvent), true);

                if (ca.Length == 0)
                    continue;

                ResourceEventHanlder proxyDelegate = (receivers, args) => EmitResourceEvent(evt.Name, receivers, args);
                evt.AddEventHandler(resource, proxyDelegate);
            }
        }

        private void Children_OnRemoved(Instance parent, IResource value)
        {
            value.Instance.parents.Remove(resource);
        }

        private void Children_OnAdd(Instance parent, IResource value)
        {
            value.Instance.parents.Add(resource);
        }

        private void Resource_OnDestroy(object sender)
        {
            ResourceDestroyed?.Invoke((IResource)sender);
        }
    }
}
