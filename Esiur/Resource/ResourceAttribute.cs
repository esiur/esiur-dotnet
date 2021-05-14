using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Resource
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class ResourceAttribute : Attribute
    {
        public ResourceAttribute()
        {

        }
    }
}
