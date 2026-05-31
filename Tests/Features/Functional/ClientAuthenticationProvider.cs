using Esiur.Core;
using Esiur.Security.Authority;
using Esiur.Security.Authority.Providers;
using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Text;

namespace Esiur.Tests.Functional
{
    internal class ClientAuthenticationProvider : PasswordAuthenticationProvider
    {
        public override PasswordHash GetHostedAccountCredential(string identity, string domain)
        {
            throw new NotImplementedException();
        }

        public override byte[] GetSelfCredential(string identity, string domain, string hostname)
        {
            if (identity == "tester" && domain == "test" && hostname == "localhost")
                return new byte[] { 1, 2, 3, 4, 5 };
            else
                return null;
        }

        public override IdentityPassword GetSelfIdentityAndCredential(string domain, string hostname)
        {
            if (domain == "test" && hostname == "localhost")
                return new IdentityPassword { Identity = "tester", Password = new byte[] { 1, 2, 3, 4, 5 } };
            else
                return new IdentityPassword { Identity = null, Password = null };
        }

        public override AsyncReply<bool> Login(Session session)
        {
            return base.Login(session);
        }

        public override AsyncReply<bool> Logout(Session session)
        {
            return base.Logout(session);
        }
    }
}
