using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Resource
{
    /// <summary>
    /// Indicates that previous property values or event occurrences are retained
    /// and may be fetched remotely.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Property |
        AttributeTargets.Field |
        AttributeTargets.Event,
        AllowMultiple = false,
        Inherited = true)]
    public sealed class HistoricalAttribute : Attribute
    {

    }
}
