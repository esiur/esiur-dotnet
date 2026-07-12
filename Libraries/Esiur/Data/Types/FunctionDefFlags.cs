using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Data.Types
{
    [Flags]
    public enum FunctionDefFlags : byte
    {
        None = 0x00,
        Inherited = (byte)MemberDefFlags.Inherited,
        Deprecated = (byte)MemberDefFlags.Deprecated,

        Static = 0x04,
        ReadOnly = 0x08,
        Idempotent = 0x10,
        Cancellable = 0x20,
    }
}