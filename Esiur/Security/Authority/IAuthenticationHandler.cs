using Esiur.Net.Packets;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Security.Authority
{
    public interface IAuthenticationHandler
    {

        public AuthenticationMode Mode { get; }
        public AuthenticationResult Initialize(Session session, object authenticationData);

        public AuthenticationResult Process(object authenticationData);

        public void Terminate(Session session);

        public void Update(Session session, object authData);
    }
}
