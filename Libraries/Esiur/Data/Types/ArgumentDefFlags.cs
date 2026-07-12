using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Data.Types
{
    [Flags]
    public enum ArgumentDefFlags : byte
    {
        None = 0,
        Optional = 0x01, // Caller may omit the argument.
        Variadic = 0x02, // Last argument accepts multiple values.
    }
}
