using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Resource
{
    [AttributeUsage(AttributeTargets.Property)]
    public class StorageAttribute:Attribute
    {
        public StorageMode Mode { get; set; }
        public StorageAttribute(StorageMode mode)
        {
            Mode = mode;
        }
    }
}
