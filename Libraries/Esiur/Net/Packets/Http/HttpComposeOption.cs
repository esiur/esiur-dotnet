using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Net.Packets.Http
{
    public enum HttpComposeOption : int
    {
        AllCalculateLength,
        AllDontCalculateLength,
        SpecifiedHeadersOnly,
        DataOnly
    }
}
