using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Security.Authority
{
    public enum AuthenticationState : int
    {
        Denied = 0x1,
        Succeeded = 0x2,
        Blocked = 0x4,
        Rejected = 0x8,
        NeedsUpdate = 0x10,
        NotFound = 0x20
    }
}
