using Esiur.Core;
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

        AsyncReply<bool> Login(Session session);
        AsyncReply<bool> Logout(Session session);

    }
}
