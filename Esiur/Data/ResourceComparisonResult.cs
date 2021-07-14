using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Data
{
    public enum ResourceComparisonResult
    {
        Null, // null
        Distributed, // resource is distributed
        Local, // resource is local
        Same, // Same as previous
    }
}
