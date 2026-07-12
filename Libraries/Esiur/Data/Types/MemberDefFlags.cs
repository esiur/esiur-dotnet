using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Data.Types
{
    [Flags]
    public enum MemberDefFlags : byte
    {
        None = 0x00,
        Inherited = 0x01, // Member is inherited from a parent TypeDef.
        Deprecated = 0x02, // Member is retained for compatibility but should be avoided.
    }
}
