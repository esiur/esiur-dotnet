using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Security.Authority
{
    public enum AuthenticationProtocol
    {
        Password = 0,

        [Obsolete("Use Password instead.")]
        Hash = Password,

        PPAP = 1,
    }
}
