using System;
using System.Collections.Generic;
using System.Text;
using Esiur.Resource;
using Esiur.Resource.Template;

namespace Esiur.Resource;

public struct PropertyModificationInfo
{
    public readonly IResource Resource;
    public readonly PropertyTemplate PropertyTemplate;
    public string Name => PropertyTemplate.Name;
    public readonly ulong Age;
    public object Value;

    public PropertyModificationInfo(IResource resource, PropertyTemplate propertyTemplate, object value, ulong age)
    {
        Resource = resource;
        PropertyTemplate = propertyTemplate;
        Age = age;
        Value = value;
    }
    
}

