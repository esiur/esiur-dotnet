using Esyur.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Esyur.Resource.Template
{
    public class AttributeTemplate : MemberTemplate
    {
        public PropertyInfo Info
        {
            get;
            set;
        }


        public AttributeTemplate(ResourceTemplate template, byte index, string name)
            : base(template, MemberType.Attribute, index, name)
        {

        }
    }
}
