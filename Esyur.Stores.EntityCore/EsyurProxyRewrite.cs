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
using System.Reflection;
using System.Text;
using Esyur.Proxy;
using Esyur.Resource;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace Esyur.Stores.EntityCore
{
    public class EsyurProxyRewrite : IModelFinalizedConvention
    {
        private static readonly MethodInfo _createInstance
    = typeof(EsyurProxyRewrite).GetTypeInfo().GetDeclaredMethod(nameof(EsyurProxyRewrite.CreateInstance));

        private readonly ConstructorBindingConvention _directBindingConvention;

        public static object CreateInstance(
    IDbContextOptions dbContextOptions,
    IEntityType entityType,
    ILazyLoader loader,
    object[] constructorArguments)
        {
            var options = dbContextOptions.FindExtension<EsyurExtensionOptions>();

            return CreateInstance2(
                options,
                entityType,
                loader,
                constructorArguments);
        }


        public static object CreateInstance2(
    EsyurExtensionOptions options,
    IEntityType entityType,
    ILazyLoader loader,
    object[] constructorArguments)
        {
            //var key = entityType.FindPrimaryKey();
            //options.AddType(entityType);

             var manager = options.Store.Instance.Managers.Count > 0 ? options.Store.Instance.Managers.First() : null;
            return Warehouse.New(entityType.ClrType, "", options.Store, null, manager);
        }

 

        public EsyurProxyRewrite(EsyurExtensionOptions ext, ProviderConventionSetBuilderDependencies conventionSetBuilderDependencies)
        {
            _directBindingConvention = new ConstructorBindingConvention(conventionSetBuilderDependencies);

        }

        public void ProcessModelFinalized(IConventionModelBuilder modelBuilder, IConventionContext<IConventionModelBuilder> context)
        {
            foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
            {
                var proxyType = ResourceProxy.GetProxy(entityType.ClrType);

                var ann = entityType.GetAnnotation(CoreAnnotationNames.ConstructorBinding);

                var binding = (InstantiationBinding)entityType[CoreAnnotationNames.ConstructorBinding];
                if (binding == null)
                    _directBindingConvention.ProcessModelFinalized(modelBuilder, context);

                binding = (InstantiationBinding)entityType[CoreAnnotationNames.ConstructorBinding];


                try

                {
                    entityType.SetAnnotation(
                        CoreAnnotationNames.ConstructorBinding,
                        new FactoryMethodBinding(
                            _createInstance,
                            new List<ParameterBinding>
                                {
                                new DependencyInjectionParameterBinding(typeof(IDbContextOptions), typeof(IDbContextOptions)),
                                new EntityTypeParameterBinding(),
                                new DependencyInjectionParameterBinding(typeof(ILazyLoader), typeof(ILazyLoader)),
                                 new ObjectArrayParameterBinding(binding.ParameterBindings)
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
