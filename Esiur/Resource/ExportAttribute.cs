using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;
using static Esiur.Resource.Template.PropertyTemplate;

namespace Esiur.Resource;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Event | AttributeTargets.Class | AttributeTargets.Enum)]

public class ExportAttribute : Attribute
{
    public string Name { get; private set; } = null;
    public PropertyPermission? Permission { get; private set; }

    public ExportAttribute()
    {

    }

    public ExportAttribute(string name)
    {
        Name = name;
    }

    public ExportAttribute(PropertyPermission permission)
    {
        Permission = permission;
    }

    public ExportAttribute(string name, PropertyPermission permission)
    {
        Name = name;
        Permission = permission;
    }



}
