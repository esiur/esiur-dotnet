using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Net.Packets.Http
{
    public enum HttpMethod : byte
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
