using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Proxy
{
    public struct ResourceGeneratorFieldInfo
    {
        public IFieldSymbol FieldSymbol { get; set; }
        public string[] Attributes { get; set; }
    }
}
