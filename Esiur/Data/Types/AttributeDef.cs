using Esiur.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Data.Types;

public class AttributeDef : MemberDef
{

    public PropertyInfo PropertyInfo
    {
        get;
        set;
    }


    public static AttributeDef MakeAttributeDef(Type type, PropertyInfo pi, byte index, string name, TypeDef typeDef)
    {
        return new AttributeDef()
        {
            Index = index,
            Inherited = pi.DeclaringType != type,
            Name = name,
            PropertyInfo = pi,
            Definition = typeDef
        };
    }
}
