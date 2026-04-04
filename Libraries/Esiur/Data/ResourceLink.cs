using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Data
{
    public class ResourceLink
    {
        readonly string value;

        public ResourceLink(string value)
        {
            this.value = value;
        }
        public static implicit operator string(ResourceLink d)
        {
            return d.value;
        }
        public static implicit operator ResourceLink(string d)
        {
            return new ResourceLink(d);
        }

        public override string ToString() => value;

    }
}
