using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Resource
{
    /// <summary>
    /// Indicates that an exported function does not change resource state,
    /// or that an exported property cannot be changed remotely.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Method |
        AttributeTargets.Property |
        AttributeTargets.Field,
        AllowMultiple = false,
        Inherited = true)]
    public sealed class ReadOnlyAttribute : Attribute
    {

    }
}
