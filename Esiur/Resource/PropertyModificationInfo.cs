using System;
using System.Collections.Generic;
using System.Text;
using Esiur.Data.Types;
using Esiur.Resource;

namespace Esiur.Resource;

public struct PropertyModificationInfo
{
    public readonly IResource Resource;
    public readonly PropertyDef PropertyDef;
    public string Name => PropertyDef.Name;
    public readonly ulong Age;
    public object Value;

    public PropertyModificationInfo(IResource resource, PropertyDef propertyDef, object value, ulong age)
    {
        Resource = resource;
        PropertyDef = propertyDef;
        Age = age;
        Value = value;
    }
    
}

