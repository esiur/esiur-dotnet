using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Security.Authority
{
    public interface IAuthenticationResponder
    {
        public AuthenticationResult Process(Session session);

    }
}
