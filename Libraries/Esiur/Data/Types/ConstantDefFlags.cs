using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Data.Types
{
    [Flags]
    public enum ConstantDefFlags : byte
    {
        None = 0x00,
        Inherited = (byte)MemberDefFlags.Inherited,
        Deprecated = (byte)MemberDefFlags.Deprecated,
    }
}
