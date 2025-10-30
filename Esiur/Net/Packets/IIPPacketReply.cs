using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Net.Packets
{
    public enum IIPPacketReply : byte
    {
        // Success
        Completed = 0x0,
        Propagated = 0x1,
        Stream = 0x2,

        // Error
        PermissionError = 0x81,
        ExecutionError = 0x82,

        // Partial
        Progress = 0x10,
        Chunk = 0x11,
        Warning = 0x12
    }
}
