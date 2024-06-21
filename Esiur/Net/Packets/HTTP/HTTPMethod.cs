using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Net.Packets.HTTP
{
    public enum HTTPMethod : byte
    {
        GET,
        POST,
        HEAD,
        PUT,
        DELETE,
        OPTIONS,
        TRACE,
        CONNECT,
        UNKNOWN
    }
}
