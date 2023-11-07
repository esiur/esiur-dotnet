using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Resource;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Event | AttributeTargets.Class | AttributeTargets.Enum)]

public class ExportAttribute : Attribute
{
    public string Name { get; set; }

    public ExportAttribute(string name = null)
    {
        Name = name;
    }
}
