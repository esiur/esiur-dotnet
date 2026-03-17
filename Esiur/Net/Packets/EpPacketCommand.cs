using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Net.Packets
{
    public enum EpPacketMethod : byte
    {
        Notification = 0,
        Request,
        Reply,
        Extension,
    }
}
