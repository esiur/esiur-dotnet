using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Data.Types
{
    public enum ConstantDefField : byte
    {
        ValueType = 0x03, // TypeRepresentation
        Value = 0x04, // object
    }
}
