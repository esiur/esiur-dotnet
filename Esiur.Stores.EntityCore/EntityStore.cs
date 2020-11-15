/*
 
Copyright (c) 2020 Ahmed Kh. Zamil

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
using Esiur.Resource;
using Esiur.Resource.Template;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using Esiur.Proxy;
using System.Linq;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Reflection;

namespace Esiur.Stores.EntityCore
{
    public class EntityStore : IStore
    {
        public Instance Instance { get; set; }

        public event DestroyedEvent OnDestroy;

        Dictionary<Type, Dictionary<object, WeakReference>> DB = new Dictionary<Type, Dictionary<object, WeakReference>>();
        object DBLock = new object();

        internal struct TypeInfo
        {
            public string Name;
            public IEntityType Type;
            public PropertyInfo PrimaryKey;
        }

        Dictionary<string, TypeInfo> TypesByName = new Dictionary<string, TypeInfo>();
        internal Dictionary<Type, TypeInfo> TypesByType = new Dictionary<Type, TypeInfo>();



        bool Loaded;

        public AsyncReply<IResource> Get(string path)
        {
            var p = path.Split('/');
            var ti = TypesByName[p[0]];
            var id = Convert.ChangeType(p[1], ti.PrimaryKey.PropertyType);// Convert.ToInt32();


            var db = DbContextProvider();
            var res = db.Find(ti.Type.ClrType, id);
            var ent = db.Entry(res);

            foreach (var rf in ent.References)
                rf.Load();

            return new AsyncReply<IResource>(res as IResource);
        }

        public AsyncReply<bool> Put(IResource resource)
        {
            if (resource is EntityStore)
                return new AsyncReply<bool>(false);

            var type = ResourceProxy.GetBaseType(resource);//.GetType().;

            //var eid = (resource as EntityResource)._PrimaryId;// (int)resource.Instance.Variables["eid"];

            var eid = TypesByType[type].PrimaryKey.GetValue(resource);

            lock (DBLock)
            {
                if (DB[type].ContainsKey(eid))
                    DB[type].Remove(eid);

                DB[type].Add(eid, new WeakReference(resource));
            }

            return new AsyncReply<bool>(true);
        }

        public IResource GetById(Type type, object id)
        {
            lock (DBLock)
            {
                if (!DB[type].ContainsKey(id))
                    return null;

                if (!DB[type][id].IsAlive)
                    return null;

                return DB[type][id].Target as IResource;
            }
        }

        [Attribute]
        public Func<DbContext> DbContextProvider { get; set; }

        [Attribute]
        public DbContextOptionsBuilder Options { get; set; }

        //DbContext dbContext;
        //[Attribute]
        //public DbContext DbContext { get; set; }

        public string Link(IResource resource)
        {
            var type = ResourceProxy.GetBaseType(resource.GetType());

            var id = TypesByType[type].PrimaryKey.GetValue(resource);
            //DbContext.Model.FindEntityType(type).DisplayName();


            //            DbContext.Model.FindEntityType(type).DisplayName
            //var entityType = DbContext.Model.FindEntityType(type);
            //var id = entityType.FindPrimaryKey().Properties
            //            .FirstOrDefault()?.PropertyInfo
            //            .GetValue(resource);
            //        var id = Types

            if (id != null)
                return this.Instance.Name + "/" + type.Name + "/" + id.ToString();
            else
                return this.Instance.Name + "/" + type.Name;
        }

        public bool Record(IResource resource, string propertyName, object value, ulong age, DateTime dateTime)
        {
            return true;
            //throw new NotImplementedException();
        }

        public bool Modify(IResource resource, string propertyName, object value, ulong age, DateTime dateTime)
        {
            return true;
            //throw new NotImplementedException();
        }

        public bool Remove(IResource resource)
        {
            throw new NotImplementedException();
        }

        public AsyncReply<bool> AddChild(IResource parent, IResource child)
        {
            throw new NotImplementedException();
        }

        public AsyncReply<bool> RemoveChild(IResource parent, IResource child)
        {
            throw new NotImplementedException();
        }

        public AsyncReply<bool> AddParent(IResource child, IResource parent)
        {
            throw new NotImplementedException();
        }

        public AsyncReply<bool> RemoveParent(IResource child, IResource parent)
        {
            throw new NotImplementedException();
        }

        public AsyncBag<T> Children<T>(IResource resource, string name) where T : IResource
        {
            throw new NotImplementedException();
        }

        public AsyncBag<T> Parents<T>(IResource resource, string name) where T : IResource
        {
            throw new NotImplementedException();
        }

        public AsyncReply<KeyList<PropertyTemplate, PropertyValue[]>> GetRecord(IResource resource, DateTime fromDate, DateTime toDate)
        {
            throw new NotImplementedException();
        }

        public AsyncReply<bool> Trigger(ResourceTrigger trigger)
        {
            if (trigger == ResourceTrigger.Initialize)// SystemInitialized && DbContext != null)
            {

                if (DbContextProvider == null)
                    DbContextProvider = () => Activator.CreateInstance(Options.Options.ContextType, Options.Options) as DbContext;

                ReloadModel();
            }

            return new AsyncReply<bool>(true);
        }

        public void ReloadModel()
        {
            TypesByName.Clear();
            TypesByType.Clear();

            var context = DbContextProvider();// Activator.CreateInstance(Options.Options.ContextType, Options.Options) as DbContext;

            var types = context.Model.GetEntityTypes();
            foreach (var t in types)
            {
                var ti = new TypeInfo()
                {
                    Name = t.ClrType.Name,
                    PrimaryKey = t.FindPrimaryKey().Properties.FirstOrDefault()?.PropertyInfo,
                    Type = t
                };

                TypesByName.Add(t.ClrType.Name, ti);
                TypesByType.Add(t.ClrType, ti);

                if (!DB.ContainsKey(t.ClrType))
                    DB.Add(t.ClrType, new Dictionary<object, WeakReference>());
            }
        }

        public void Destroy()
        {
            //throw new NotImplementedException();
        }
    }
}
