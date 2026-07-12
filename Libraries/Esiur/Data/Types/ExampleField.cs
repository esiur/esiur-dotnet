using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Data.Types
{
    public enum ExampleField : byte
    {
        Title = 0x00,       // string
        Description = 0x01, // string
        Arguments = 0x02,   // Map<byte, object>
        Result = 0x03,      // object
    }
}
