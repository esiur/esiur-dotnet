using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Net.Sockets
{
    public enum SocketState
    {
        Initial,
        Listening,
        Connecting,
        Established,
        Closed,
        Terminated
    }
}
