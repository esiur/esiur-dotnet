using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Net.Packets
{
    public enum IIPPacketEvent : byte
    {
        // Event Manage
        ResourceReassigned = 0,
        ResourceDestroyed,
        ChildAdded,
        ChildRemoved,
        Renamed,
        // Event Invoke
        PropertyUpdated = 0x10,
        EventOccurred,

        // Attribute
        AttributesUpdated = 0x18
    }
}
