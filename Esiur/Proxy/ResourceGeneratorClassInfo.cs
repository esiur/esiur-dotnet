using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Proxy
{
    public struct ResourceGeneratorClassInfo
    {
        public string Name { get; set; }
        public bool ImplementInterface { get; set; }

        public bool ImplementTrigger { get; set; }
        public IFieldSymbol[] Fields { get; set; }
        public ITypeSymbol ClassSymbol { get; set; }

        public ClassDeclarationSyntax ClassDeclaration { get; set; }

    }
}
