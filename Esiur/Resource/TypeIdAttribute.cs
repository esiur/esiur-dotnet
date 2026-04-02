using Esiur.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Resource
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Enum)]
    public class TypeIdAttribute : Attribute
    {
        public Uuid Id { get; private set; }

        public TypeIdAttribute(string id)
        {
            var data = DC.FromHex(id, null);
            Id = new Uuid(data);
        }
    }
}
