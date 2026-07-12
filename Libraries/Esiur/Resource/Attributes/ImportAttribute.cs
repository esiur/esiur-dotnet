using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Resource;

[AttributeUsage(AttributeTargets.Class)]
public class ImportAttribute : Attribute
{
    public ImportAttribute(params string[] urls)
    {

    }
}
