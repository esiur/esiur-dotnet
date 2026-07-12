using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Resource
{
    /// <summary>
    /// Indicates that previous values of an exported property are retained
    /// and may be fetched remotely.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Property |
        AttributeTargets.Field,
        AllowMultiple = false,
        Inherited = true)]
    public sealed class HistoricalAttribute : Attribute
    {

    }
}
