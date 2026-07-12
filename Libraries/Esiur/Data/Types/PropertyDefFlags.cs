using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Data.Types
{
    [Flags]
    public enum PropertyDefFlags : byte
    {
        None = 0x00,
        Inherited = (byte)MemberDefFlags.Inherited,
        Deprecated = (byte)MemberDefFlags.Deprecated,

        ReadOnly = 0x04, // Property cannot be changed by remote Set.
        Constant = 0x08, // Property has a constant valu
        Volatile = 0x10, // Value may change but is not necessarily synchronized.
        Historical = 0x20, // Previous values are retained and may be fetched.
    }
}
