using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Resource
{
    /// <summary>
    /// Indicates that repeating the same invocation should have the same
    /// externally observable effect as invoking it once.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Method,
        AllowMultiple = false,
        Inherited = true)]
    public sealed class IdempotentAttribute : Attribute
    {
    }

}
