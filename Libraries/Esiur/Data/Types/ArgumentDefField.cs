using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Data.Types
{
    public enum ArgumentDefField : byte
    {
        ValueType = 0x03, // TypeRepresentation
        DefaultValue = 0x04, // object
    }
}
