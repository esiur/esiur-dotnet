using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Data
{
    public class ResourceLink<T>
    {
        readonly string value;

        public ResourceLink(string value)
        {
            this.value = value;
        }
        public static implicit operator string(ResourceLink<T> d)
        {
            return d.value;
        }
        public static implicit operator ResourceLink<T>(string d)
        {
            return new ResourceLink<T>(d);
        }

        public override string ToString() => value;

    }
}
