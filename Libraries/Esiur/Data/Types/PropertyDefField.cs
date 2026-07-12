using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Data.Types
{
    public enum PropertyDefField : byte
    {
        ValueType = 0x03, // TypeRepresentation
        OrderingControl = 0x04, // OrderingControl
        HistoryControl = 0x05, // HistoryControl
        DefaultValue = 0x06, // object
    }
}
