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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Esiur.Proxy;
using Esiur.Resource;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace Esiur.Stores.EntityCore
{
    public class EsiurProxyRewrite : IModelFinalizingConvention
    {
        private static readonly MethodInfo _createInstance
    = typeof(EsiurProxyRewrite).GetTypeInfo().GetDeclaredMethod(nameof(EsiurProxyRewrite.CreateInstance));

        private readonly ConstructorBindingConvention _directBindingConvention;

        //public static object CreateInstance(IDbContextOptions dbContextOptions, IEntityType entityType,
        //                                    object[] constructorArguments, DbContext context, long id)
        //{
        //    return CreateInstance(dbContextOptions, entityType,
        //                                     constructorArguments,  context,  id);
        //}

        public static object CreateInstance(
                                            IDbContextOptions dbContextOptions,
                                            IEntityType entityType,
                                            object[] properties

        // ILazyLoader loader,
        // object[] constructorArguments,
        //DbContext context,
        )
        {
            ///var id = constructorArguments.Last();
            var id = properties.First();

            var options = dbContextOptions.FindExtension<EsiurExtensionOptions>();
            var manager = options.Store.Instance.Managers.Count > 0 ? options.Store.Instance.Managers.First() : null;

            var cache = options.Store.GetById(entityType.ClrType, id);

            if (cache != null)
                return cache;

            // check if the object exists
            var obj = Warehouse.New(entityType.ClrType).Wait() as EntityResource;//, "", options.Store, null, manager);
            //obj._PrimaryId = id;
            options.Store.TypesByType[entityType.ClrType].PrimaryKey.SetValue(obj, id);
            Warehouse.Put(obj, id.ToString(), options.Store, null, null, 0, manager).Wait();

            //            obj.Instance.IntVal = id;//.Variables.Add("eid", id);

            return obj;
        }


        public EsiurProxyRewrite(EsiurExtensionOptions ext, ProviderConventionSetBuilderDependencies conventionSetBuilderDependencies)
        {
            _directBindingConvention = new ConstructorBindingConvention(conventionSetBuilderDependencies);

        }


        public void ProcessModelFinalizing(IConventionModelBuilder modelBuilder, IConventionContext<IConventionModelBuilder> context)
        {
            foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
            {
                var proxyType = ResourceProxy.GetProxy(entityType.ClrType);

                // var ann = entityType.GetAnnotation(CoreAnnotationNames.ConstructorBinding);

#pragma warning disable EF1001 // Internal EF Core API usage.
                var binding = (InstantiationBinding)entityType[CoreAnnotationNames.ConstructorBinding];
#pragma warning restore EF1001 // Internal EF Core API usage.
                if (binding == null)
                    _directBindingConvention.ProcessModelFinalizing(modelBuilder, context);

#pragma warning disable EF1001 // Internal EF Core API usage.
                binding = (InstantiationBinding)entityType[CoreAnnotationNames.ConstructorBinding];
#pragma warning restore EF1001 // Internal EF Core API usage.


                try

                {
                    entityType.SetAnnotation(
#pragma warning disable EF1001 // Internal EF Core API usage.
                        CoreAnnotationNames.ConstructorBinding,
#pragma warning restore EF1001 // Internal EF Core API usage.
                        new FactoryMethodBinding(
                            _createInstance,
                            new List<ParameterBinding>
                                {
                                new DependencyInjectionParameterBinding(typeof(IDbContextOptions), typeof(IDbContextOptions)),
                                new EntityTypeParameterBinding(),
                                // constructor arguments 
                                //new ObjectArrayParameterBinding(binding.ParameterBindings),
                                 //new ContextParameterBinding(typeof(DbContext)),
                                 new ObjectArrayParameterBinding(new ParameterBinding[]{ 
                                            new PropertyParameterBinding(entityType.FindPrimaryKey().Properties.FirstOrDefault())
                                 })
                                // new Microsoft.EntityFrameworkCore.Metadata.ObjectArrayParameterBinding(),
                                 //new ObjectArrayParameterBinding() 

                                },
                            proxyType));

                }
                catch
                {
                }

            }
        }

 
    }
}
