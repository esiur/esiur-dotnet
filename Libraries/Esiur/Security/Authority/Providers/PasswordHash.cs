using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Security.Authority.Providers
{
    public struct PasswordHash
    {
        public byte[] Hash { get; set; }
        public byte[] Salt { get; set; }

        public PasswordHash(byte[] hash, byte[] salt)
        {
            Hash = hash;
            Salt = salt;
        }
    }
}
