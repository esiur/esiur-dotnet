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

using Esyur.Core;
using Esyur.Data;
using Esyur.Resource;
using Esyur.Resource.Template;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore.Proxies;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Esyur.Proxy;
using System.Linq;

namespace Esyur.Stores.EntityCore
{
    public class EntityStore : IStore
    {
        public Instance Instance { get; set; }

        public event DestroyedEvent OnDestroy;

        /*
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var extension = optionsBuilder.Options.FindExtension<EsyurExtension>()
                ?? new EsyurExtension();

            
            ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);
            //optionsBuilder.UseLazyLoadingProxies();
            base.OnConfiguring(optionsBuilder);
        }
        */

            /*
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            //modelBuilder.Entity<Series>().ToTable("Series");
            //modelBuilder.Entity<Episode>().ToTable("Episodes").;
            //modelBuilder.Ignore<Entit>
            // modelBuilder.Entity<Series>(x=>x.Property(p=>p.Instance).HasConversion(v=>v.Managers.)
            Console.WriteLine("OnModelCreating");
            //modelBuilder.Entity()


            base.OnModelCreating(modelBuilder);
        }*/


        public async AsyncReply<IResource> Get(string path)
        {
            var p = path.Split('/');
            var type = Options.Cache.Keys.Where(x => x.Name.ToLower() == p[0].ToLower()).FirstOrDefault();
            var id = Convert.ToInt32(p[1]);
            return DbContext.Find(type, id) as IResource;
        }

        public async AsyncReply<bool> Put(IResource resource)
        {
            return true;
        }

        [Attribute]
        public EsyurExtensionOptions Options { get; set; }

        [Attribute]
        public DbContext DbContext { get; set; }

        public string Link(IResource resource)
        {
            var type = ResourceProxy.GetBaseType(resource.GetType());

            var id = Options.Cache[type].GetValue(resource);

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

        public new bool Remove(IResource resource)
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
            return new AsyncReply<bool>(true);
        }

        public void Destroy()
        {
            //throw new NotImplementedException();
        }
    }
}
