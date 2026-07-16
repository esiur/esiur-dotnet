using Esiur.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Security.Authority.Providers
{
    public class PasswordAuthenticationProvider : IAuthenticationProvider
    {
        /// <summary>
        /// Canonical protocol name used for registration and EP negotiation.
        /// </summary>
        public const string ProtocolName = "password-sha3-v1";

        /// <summary>
        /// Previous protocol name, retained only to make explicit migration aliases possible.
        /// New connections should use <see cref="ProtocolName"/>.
        /// </summary>
        [Obsolete("Use ProtocolName (`password-sha3-v1`) for new connections.")]
        public const string LegacyProtocolName = "hash";

        public string DefaultName => ProtocolName;

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

        public virtual PasswordHash GetHostedAccountCredential(string identity, string domain)
        {
            return new PasswordHash();
        }

        public virtual IdentityPassword GetSelfIdentityAndCredential(string domain, string hostname)
        {
            return new IdentityPassword();
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
