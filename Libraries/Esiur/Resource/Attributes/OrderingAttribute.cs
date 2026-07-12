using Esiur.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Resource
{
    /// <summary>
    /// Specifies non-default notification ordering for a property or event.
    /// Absence of this attribute means strict ordering.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Property |
        AttributeTargets.Field |
        AttributeTargets.Event,
        AllowMultiple = false,
        Inherited = true)]
    public sealed class OrderingAttribute : Attribute
    {
        public OrderingControl Control { get; }

        public OrderingAttribute(OrderingControl control)
        {
            Control = control;
        }
    }

}
