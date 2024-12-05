using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;

namespace Esiur.Resource;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Event | AttributeTargets.Class | AttributeTargets.Enum)]

public class ExportAttribute : Attribute
{
    public string Name { get; private set; } = null;
    public Type ReturnType { get; private set; } = null;

    public ExportAttribute()
    {

    }

    public ExportAttribute(string name)
    {
        Name = name;
    }

    public ExportAttribute(Type returnType)
    {
        ReturnType = returnType;
    }

    public ExportAttribute(string name, Type returnType)
    {
        Name = name;
        ReturnType = returnType;
    }


}
