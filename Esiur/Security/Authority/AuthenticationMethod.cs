using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Security.Authority;

public enum AuthenticationMethod : byte
{
    None,
    Credentials,
    Token,
    Certificate
}
