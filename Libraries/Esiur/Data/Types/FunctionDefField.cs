using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Data.Types
{
    public enum FunctionDefField : byte
    {
        Arguments = 0x03, // List<Map<byte, object>>
        ReturnType = 0x04, // TRU
        StreamMode = 0x05, // StreamMode
    }
}
