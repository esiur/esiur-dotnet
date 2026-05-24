using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Security.Authority
{
    public interface IAuthenticationProvider
    {
        public IAuthenticationHandler 
            CreateAuthenticationHandler(AuthenticationContext context);

        public string DefaultName { get; }
    }
}
