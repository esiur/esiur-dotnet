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


    public static AttributeTemplate MakeAttributeTemplate(Type type, PropertyInfo pi, byte index = 0, string customName = null, TypeTemplate typeTemplate = null)
    {
        return new AttributeTemplate()
        {
            Index = index,
            Inherited = pi.DeclaringType != type,
            Name = customName,
            PropertyInfo = pi
        };
    }
}
