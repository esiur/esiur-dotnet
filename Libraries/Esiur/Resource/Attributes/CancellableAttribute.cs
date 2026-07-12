using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Resource
{
    /// <summary>
    /// Indicates that a running remote invocation may be cancelled.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Method,
        AllowMultiple = false,
        Inherited = true)]
    public sealed class CancellableAttribute : Attribute
    {
    }
}
