using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Net.Packets
{
    public enum IIPAuthPacketCommand : byte
    {
        Initialize = 0x0,
        Acknowledge = 0x1,
        Action = 0x2,
        Event = 0x3,
    }

}
