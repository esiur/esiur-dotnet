using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Security.Authority;

public enum AuthenticationMethod : byte
{
    None,
    PpapCredentialsAnonymous,
    PpapCredentialsCredentials,
    PpapCredentialsHec,
    PpapHecAnonymous,
    PpapHecCredentials,
    PpapHecHec,
    HashAnonymous,

}
