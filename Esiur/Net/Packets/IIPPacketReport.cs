using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Net.Packets
{
    public enum IIPPacketReport : byte
    {
        ManagementError,
        ExecutionError,
        ProgressReport = 0x8,
        ChunkStream = 0x9
    }

}
