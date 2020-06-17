using System;
using System.Collections.Generic;
using System.Text;

namespace Esyur.Security.Authority
{
    public enum AuthenticationMethod : byte
    {
        None,
        Certificate,
        Credentials,
        Token
    }
}
