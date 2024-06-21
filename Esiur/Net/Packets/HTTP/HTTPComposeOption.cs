using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Net.Packets.HTTP
{
    public enum HTTPComposeOption : int
    {
        AllCalculateLength,
        AllDontCalculateLength,
        SpecifiedHeadersOnly,
        DataOnly
    }
}
