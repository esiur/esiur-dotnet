using Esiur.Security.Authority;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Protocol.Authentication
{
    public class HashAnonymousAuthenticator : IAuthenticationHandler
    {
        public AuthenticationResult Initiate(Session session)
        {
            throw new NotImplementedException();
        }

        public AuthenticationResult Process(object handshakePayload)
        {
            throw new NotImplementedException();
        }
    }
}
