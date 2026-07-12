using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Data
{
    public enum OrderingControl : byte
    {
        Strict = 0,

        // Can be reordered with unrelated notifications,
        // but updates are still delivered.
        Relaxed = 1,

        // Runtime may keep only the newest pending value
        // for this property.
        LatestOnly = 2,
    }
}
