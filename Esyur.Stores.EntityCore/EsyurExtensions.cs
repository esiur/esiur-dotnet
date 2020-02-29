﻿/*
 
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

using Esyur.Resource;
using Esyur.Security.Permissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esyur.Stores.EntityCore
{
    public static class EsyurExtensions
    {
        public static T CreateResource<T>(this DbContext dbContext, object properties = null) where T:class,IResource
        {
            return dbContext.GetInfrastructure().CreateResource<T>(properties);

        }
        
        public static T CreateResource<T>(this DbSet<T> dbSet, object properties = null) where T:class,IResource
        {
            var resource = dbSet.GetInfrastructure().CreateResource<T>(properties);
            dbSet.Add(resource);
            return resource;
        }

        public static T CreateResource<T>(this IServiceProvider serviceProvider, object properties = null) where T:class,IResource
        {
            var options = serviceProvider.GetService<IDbContextOptions>().FindExtension<EsyurExtensionOptions>();

            var resource = Warehouse.New<T>("", options.Store, null, null, null, properties);

            resource.Instance.Managers.AddRange(options.Store.Instance.Managers.ToArray());

            return resource;
        }

        public static DbContextOptionsBuilder UseEsyur(this DbContextOptionsBuilder optionsBuilder,
                                                        DbContext context,
                                                        string name = null,
                                            IResource parent = null,
                                            IPermissionsManager manager = null

            )
        {
            var extension = optionsBuilder.Options.FindExtension<EsyurExtensionOptions>();
           
            if (extension == null)
            {

                var store = Warehouse.New<EntityStore>(name, null, parent, manager);
                extension = new EsyurExtensionOptions(store);
                store.Options = extension;
                store.DbContext = context;
            }

            ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);

            return optionsBuilder;

        }

        public static DbContextOptionsBuilder<TContext> UseEsyur<TContext>(
                                             this DbContextOptionsBuilder<TContext> optionsBuilder,
                                             DbContext context,
                                            string name = null,
                                            IResource parent = null,
                                            IPermissionsManager manager = null)
                                            where TContext : DbContext
        {


            var extension = optionsBuilder.Options.FindExtension<EsyurExtensionOptions>();

            if (extension == null)
            {
                var store = Warehouse.New<EntityStore>(name, null, parent, manager);
                extension = new EsyurExtensionOptions(store);
                store.Options = extension;
                store.DbContext = context;
            }


            ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);

            return optionsBuilder;

        }

    }
}
