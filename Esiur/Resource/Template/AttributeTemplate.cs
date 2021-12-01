using Esiur.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Resource.Template;
public class AttributeTemplate : MemberTemplate
{
    public PropertyInfo PropertyInfo
    {
        get;
        set;
    }


    public AttributeTemplate(TypeTemplate template, byte index, string name)
        : base(template, MemberType.Attribute, index, name)
    {

    }
}
