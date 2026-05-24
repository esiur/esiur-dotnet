using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Security.Authority
{
    internal class PpapAuthenticationProvider : IAuthenticationProvider
    {
        public string DefaultName => "PPAP";

        public IAuthenticationHandler CreateAuthenticationHandler(AuthenticationContext context)
        {
            throw new NotImplementedException();
        }
    }
}
