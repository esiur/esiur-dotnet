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
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
//using Microsoft.EntityFrameworkCore.Proxies.Internal;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Stores.EntityCore
{
    public class EsiurPlugin : IConventionSetPlugin
    {
        private readonly IDbContextOptions _options;
        private readonly ProviderConventionSetBuilderDependencies _conventionSetBuilderDependencies;

        public EsiurPlugin(
             IDbContextOptions options,
            ProviderConventionSetBuilderDependencies conventionSetBuilderDependencies)
        {
            _options = options;
            _conventionSetBuilderDependencies = conventionSetBuilderDependencies;
        }

     
        public ConventionSet ModifyConventions(ConventionSet conventionSet)
        {
            var extension = _options.FindExtension<EsiurExtensionOptions>();
            conventionSet.ModelFinalizedConventions.Add(new EsiurProxyRewrite(
                    extension,
                    _conventionSetBuilderDependencies));
            return conventionSet;

        }
    }

}
