using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Security.Authority
{
    public class AuthenticationMaterial
    {
        public AuthenticationMaterialType Type { get; set; }
        public object Value { get; set; }
    }
}
