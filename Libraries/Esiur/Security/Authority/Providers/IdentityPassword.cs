using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Security.Authority.Providers
{
    public struct IdentityPassword
    {
        public string Identity { get; set; }
        public byte[] Password { get; set; }

        public IdentityPassword(string identity, byte[] password)
        {
            Identity = identity;
            Password = password;  
        }
    }
}
