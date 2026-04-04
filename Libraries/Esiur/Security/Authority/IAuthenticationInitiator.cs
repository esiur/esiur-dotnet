using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Security.Authority
{
    public interface IAuthenticationInitiator
    {
        public AuthenticationResult Initiate(Session session);

        public AuthenticationResult Process(object handshakePayload);
    }
}
