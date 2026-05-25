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
        public override (byte[], byte[]) GetHostedAccountCredential(string identity, string domain)
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

        public override (string, byte[]) GetSelfIdentityAndCredential(string domain, string hostname)
        {
            if (domain == "test" && hostname == "localhost")
                return ("tester", new byte[] { 1, 2, 3, 4, 5 });
            else
                return (null, null);
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
