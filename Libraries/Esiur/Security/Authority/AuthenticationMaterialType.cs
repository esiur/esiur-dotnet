using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Security.Authority
{
    public enum AuthenticationMaterialType: byte
    {
        Secret,
        Key,
        Identity,
        Data,
    }
}
