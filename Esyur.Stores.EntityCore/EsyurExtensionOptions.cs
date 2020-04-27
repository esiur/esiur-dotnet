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

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Internal;
using Microsoft.EntityFrameworkCore.Utilities;
using Microsoft.EntityFrameworkCore.Proxies.Internal;
using System.Linq;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Reflection;
using Esyur.Proxy;

namespace Esyur.Stores.EntityCore
{
    public class EsyurExtensionOptions : IDbContextOptionsExtension
    {

        //public Dictionary<Type, PropertyInfo> Cache { get; } = new Dictionary<Type, PropertyInfo>();
        //public void AddType(IEntityType type)
        //{
        //    if (!Cache.ContainsKey(type.ClrType))
        //        Cache.Add(type.ClrType, type.FindPrimaryKey().Properties[0].PropertyInfo);
        //}

        

        private DbContextOptionsExtensionInfo _info;
        EntityStore _store;

        public DbContextOptionsExtensionInfo Info => _info;

        public EntityStore Store => _store;


        public void ApplyServices(IServiceCollection services)
        {
            services.AddEntityFrameworkProxies();

            new EntityFrameworkServicesBuilder(services)
                .TryAdd<IConventionSetPlugin, EsyurPlugin>();
        }

        public void Validate(IDbContextOptions options)
        {
            var internalServiceProvider = options.FindExtension<CoreOptionsExtension>()?.InternalServiceProvider;
            if (internalServiceProvider != null)
            {
                var scope = internalServiceProvider.CreateScope();
                var conventionPlugins = scope.ServiceProvider.GetService<IEnumerable<IConventionSetPlugin>>();
                if (conventionPlugins?.Any(s => s is EsyurPlugin) == false)
                {
                    throw new InvalidOperationException("");
                }
            }
        }

        public EsyurExtensionOptions(EntityStore store)
        {
            _info = new ExtensionInfo(this);
            _store = store;
        }


        private sealed class ExtensionInfo : DbContextOptionsExtensionInfo
        {

            public ExtensionInfo(IDbContextOptionsExtension extension)
                : base(extension)
            {
            }

            private new EsyurExtensionOptions Extension
                => (EsyurExtensionOptions)base.Extension;

            public override bool IsDatabaseProvider => false;

            public override string LogFragment => "Esyur";

            public override long GetServiceProviderHashCode() => 2312;

            public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
            {

            }
        }

    }
}
