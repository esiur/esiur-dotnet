using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Security.Cryptography
{
    public interface ISymetricCipher
    {
        public ushort Identifier { get; }
        public byte[] Encrypt(byte[] data);
        public byte[] Decrypt(byte[] data);
        public byte[] SetKey(byte[] key);
    }
}
