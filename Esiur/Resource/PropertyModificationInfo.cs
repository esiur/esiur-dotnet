using System;
using System.Collections.Generic;
using System.Text;
using Esiur.Data.Types;
using Esiur.Resource;

namespace Esiur.Resource;

public struct PropertyModificationInfo
{
    public readonly IResource Resource;
    public readonly PropertyDef PropertyTemplate;
    public string Name => PropertyTemplate.Name;
    public readonly ulong Age;
    public object Value;

    public PropertyModificationInfo(IResource resource, PropertyDef propertyTemplate, object value, ulong age)
    {
        Resource = resource;
        PropertyTemplate = propertyTemplate;
        Age = age;
        Value = value;
    }
    
}

