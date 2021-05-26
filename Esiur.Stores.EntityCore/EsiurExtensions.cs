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
using Esiur.Misc;
using Esiur.Proxy;
using Esiur.Resource;
using Esiur.Security.Permissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Esiur.Stores.EntityCore
{
    public static class EsiurExtensions
    {
        //public static T CreateResource<T>(this DbContext dbContext, object properties = null) where T:class,IResource
        //{
        //    return dbContext.GetInfrastructure().CreateResource<T>(properties);

        //}

        public static T AddResource<T>(this DbSet<T> dbSet, T resource) where T : class, IResource
            => AddResourceAsync(dbSet, resource).Wait();

        public static async AsyncReply<T> AddResourceAsync<T>(this DbSet<T> dbSet, T resource) where T : class, IResource
        {
            var store = dbSet.GetInfrastructure().GetService<IDbContextOptions>().FindExtension<EsiurExtensionOptions>().Store;


            var manager = store.Instance.Managers.FirstOrDefault();// > 0 ? store.Instance.Managers.First() : null;

            //var db = dbSet.GetService<ICurrentDbContext>().Context;

            //var resource = dbSet.GetInfrastructure().CreateResource<T>(properties);
            //var resource = Warehouse.New<T>("", options.Store, null, null, null, properties);

            var resType = typeof(T);
            var proxyType = ResourceProxy.GetProxy(resType);


            IResource res;

            if (proxyType == resType)
            {
                res = resource;
            }
            else
            {
                res = Activator.CreateInstance(proxyType) as IResource;
                var ps = Structure.FromObject(resource);

                foreach (var p in ps)
                {
                    
                    var mi = resType.GetMember(p.Key, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                                    .FirstOrDefault();

                    if (mi != null)
                    {
                        if (mi is PropertyInfo)
                        {
                            var pi = mi as PropertyInfo;
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
                        else if (mi is FieldInfo)
                        {
                            try
                            {
                                (mi as FieldInfo).SetValue(res, p.Value);
                            }
                            catch (Exception ex)
                            {
                                Global.Log(ex);
                            }
                        }
                    }
                }
            }

            //await Warehouse.Put<T>("", null, null, null, null, properties);
            var entity = dbSet.Add((T)res);
            await entity.Context.SaveChangesAsync();

            var id = store.TypesByType[typeof(T)].PrimaryKey.GetValue(resource);

            await Warehouse.Put(id.ToString(), res, store, null, null, 0, manager);

            return (T)res;
        }

        //public static async AsyncReply<T> CreateResourceAsync<T>(this IServiceProvider serviceProvider, T properties = null) where T : class, IResource
        //{
        //    var options = serviceProvider.GetService<IDbContextOptions>().FindExtension<EsiurExtensionOptions<T>>();

        //    var resource = await Warehouse.New<T>("", options.Store, null, null, null, properties);

        //    resource.Instance.Managers.AddRange(options.Store.Instance.Managers.ToArray());

        //    return resource;
        //}

        //public static T CreateResource<T>(this IServiceProvider serviceProvider, object properties = null) where T : class, IResource
        //    => CreateResourceAsync<T>(serviceProvider, properties).Wait();

        public static DbContextOptionsBuilder UseEsiur(this DbContextOptionsBuilder optionsBuilder,
                                                        EntityStore store,
                                                        Func<DbContext> getter = null

            //IServiceCollection services = null
            //string name = null,
            //IResource parent = null,
            //IPermissionsManager manager = null,
            //Func<DbContext> dbContextProvider = null
            )
        {
            var extension = optionsBuilder.Options.FindExtension<EsiurExtensionOptions>();

            if (extension == null)
            {

                //var store = Warehouse.New<EntityStore>(name, null, parent, manager, new { Options = optionsBuilder, DbContextProvider = dbContextProvider }).Wait();                
                store.Options = optionsBuilder.Options;
                extension = new EsiurExtensionOptions(store);
            }

            ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);

            return optionsBuilder;

        }

        //public static DbContextOptionsBuilder<TContext> UseEsiur<TContext>(
        //                                     this DbContextOptionsBuilder<TContext> optionsBuilder,
        //                                    //DbContext context,
        //                                    string name = null,
        //                                    IResource parent = null,
        //                                    IPermissionsManager manager = null,
        //                                    Func<DbContext> dbContextProvider = null)
        //                                    where TContext : DbContext
        //{


        //    var extension = optionsBuilder.Options.FindExtension<EsiurExtensionOptions>();

        //    if (extension == null)
        //    {
        //        var store = Warehouse.New<EntityStore>(name, null, parent, manager, new { Options = optionsBuilder, DbContextProvider = dbContextProvider }).Wait();
        //        extension = new EsiurExtensionOptions(store);
        //        //store.Options = optionsBuilder;
        //        //store.Options = extension;
        //        //store.DbContext = context;
        //    }


        //    ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);

        //    return optionsBuilder;

        //}

    }
}
