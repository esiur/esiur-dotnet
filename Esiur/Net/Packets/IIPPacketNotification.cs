using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Net.Packets
{
    public enum IIPPacketNotification : byte
    {
        // Notification Invoke
        PropertyModified = 0x0,
        EventOccurred = 0x1,

        // Notification Manage
        ResourceDestroyed = 0x8,
        ResourceReassigned = 0x9,
        ResourceMoved = 0xA,
        SystemFailure = 0xB,
    }
}
