using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Resource
{
    /// <summary>
    /// Indicates that an exported property has volatile synchronization
    /// semantics. This is unrelated to the C# volatile memory modifier.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Property |
        AttributeTargets.Field,
        AllowMultiple = false,
        Inherited = true)]
    public sealed class VolatileAttribute : Attribute
    {

    }
}
