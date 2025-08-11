using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Net.Packets
{
    public enum IIPPacketNotification : byte
    {
        // Event Manage
        ResourceDestroyed = 0,
        ResourceReassigned,
        ResourceMoved,
        SystemFailure,
        // Event Invoke
        PropertyModified = 0x8,
        EventOccurred,
    }
}
