using Esiur.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Security.Authority.Providers
{
    public class PasswordAuthenticationProvider : IAuthenticationProvider
    {
        public string DefaultName => "hash";

        public IAuthenticationHandler CreateAuthenticationHandler(AuthenticationContext context)
        {
            var authHandler = new PasswordAuthenticationHandler(context.Mode, 
                context.Direction,
                context.InitiatorIdentity,
                context.ResponderIdentity,
                context.HostName, 
                context.Domain,
                this);

            return authHandler;
        }

        public virtual (byte[], byte[]) GetHostedAccountCredential(string identity, string domain)
        {
            return (null, null);
        }

        public virtual (string, byte[]) GetSelfIdentityAndCredential(string domain, string hostname)
        {
            return (null, null);
        }

        public virtual byte[] GetSelfCredential(string identity, string domain, string hostname)
        {
            return null;
        }

        public virtual AsyncReply<bool> Login(Session session)
        {
            return new AsyncReply<bool>(false);
        }

        public virtual AsyncReply<bool> Logout(Session session)
        {
            return new AsyncReply<bool>(false);
        }
    }
}
