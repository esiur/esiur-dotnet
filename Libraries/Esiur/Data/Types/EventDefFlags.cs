using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Data.Types
{
    [Flags]
    public enum EventDefFlags : byte
    {
        None = 0x00,
        Inherited = (byte)MemberDefFlags.Inherited,
        Deprecated = (byte)MemberDefFlags.Deprecated,

        AutoDelivered = 0x04, // Delivered to attached clients without explicit subscription.
        Historical = 0x08,
    }
}
