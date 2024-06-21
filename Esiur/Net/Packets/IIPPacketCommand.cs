using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Net.Packets
{
    public enum IIPPacketCommand : byte
    {
        Event = 0,
        Request,
        Reply,
        Report,
    }
}
