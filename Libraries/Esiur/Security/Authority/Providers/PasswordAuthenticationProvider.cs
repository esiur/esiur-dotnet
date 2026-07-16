using Esiur.Core;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Esiur.Security.Authority.Providers
{
    public class PasswordAuthenticationProvider : IAuthenticationProvider
    {
        /// <summary>
        /// Canonical protocol name used for registration and EP negotiation.
        /// </summary>
        public const string ProtocolName = "password-sha3-v1";

        public string DefaultName => ProtocolName;

        /// <summary>
        /// Creates the salted credential stored by a server for the
        /// <c>password-sha3-v1</c> protocol. Call this once during account enrollment,
        /// persist the returned hash and salt, and discard the plaintext password.
        /// The hash is a password-equivalent protocol verifier and must be protected
        /// from disclosure, logs, and client-visible data.
        /// </summary>
        public static PasswordHash CreateCredential(byte[] password)
        {
            if (password == null)
                throw new ArgumentNullException(nameof(password));
            if (password.Length == 0)
                throw new ArgumentException("A password cannot be empty.", nameof(password));

            var salt = new byte[32];
            using (var random = RandomNumberGenerator.Create())
                random.GetBytes(salt);

            var material = new byte[password.Length + salt.Length];
            try
            {
                Buffer.BlockCopy(password, 0, material, 0, password.Length);
                Buffer.BlockCopy(salt, 0, material, password.Length, salt.Length);
                return new PasswordHash(
                    PasswordAuthenticationHandler.ComputeSha3(material),
                    salt);
            }
            finally
            {
                Array.Clear(material, 0, material.Length);
            }
        }

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
