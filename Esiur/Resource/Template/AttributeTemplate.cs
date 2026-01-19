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


    public AttributeTemplate(TypeTemplate template, byte index, string name, bool inherited)
        : base(template, index, name, inherited)
    {

    }

    public static AttributeTemplate MakeAttributeTemplate(Type type, PropertyInfo pi, byte index = 0, string customName = null, TypeTemplate typeTemplate = null)
    {
        var at = new AttributeTemplate(typeTemplate, index, customName, pi.DeclaringType != type);
        at.PropertyInfo = pi;
        return at;
    }
}
