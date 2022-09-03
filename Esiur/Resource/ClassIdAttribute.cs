using Esiur.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Resource
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Enum)]
    public class ClassIdAttribute : Attribute
    {
        public Guid ClassId { get; private set; }

        public ClassIdAttribute(string classId)
        {
            var data = DC.FromHex(classId, null);
            ClassId = new Guid(data);
        }
    }
}
