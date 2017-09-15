using System;
using System.Collections.Generic;
using System.Text;
using Esiur.Net.Sockets;
using Esiur.Security.Authority;

namespace Esiur.Net.IIP
{
    public class DistributedSession : NetworkSession
    {
        Source Source { get; }
        Authentication Authentication;
    }
}
