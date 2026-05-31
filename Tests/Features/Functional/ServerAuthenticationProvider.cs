using Esiur.Core;
using Esiur.Security.Authority;
using Esiur.Security.Authority.Providers;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Tests.Functional
{
    internal class ServerAuthenticationProvider : PasswordAuthenticationProvider
    {
        public override PasswordHash GetHostedAccountCredential(string identity, string domain)
        {
            if (identity == "tester" && domain == "test")
                return new PasswordHash(PasswordAuthenticationHandler.ComputeSha3(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }),
                        new byte[] { 6, 7, 8, 9, 10 });
            else
                return new PasswordHash(null, null);
        }

        public override byte[] GetSelfCredential(string identity, string domain, string hostname)
        {
            throw new NotImplementedException();
        }

        public override IdentityPassword GetSelfIdentityAndCredential(string domain, string hostname)
        {
            throw new NotImplementedException();
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
