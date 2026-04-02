using Esiur.Net.Packets;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Security.Authority
{
    public interface IAuthenticationHandler
    {
        public AuthenticationResult Initialize(Session session);

        public AuthenticationResult Process(object handshakePayload);

        public void Terminate(Session session);

        public void Update(Session session, object authData);
    }
}
