using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Data.Types
{
    public enum EventDefField : byte
    {
        ArgumentType = 0x03, // TypeRepresentation
        ArgumentName = 0x04, // string
        OrderingControl = 0x05, // OrderingControl
        HistoryControl = 0x06, // HistoryControl
    }
}
