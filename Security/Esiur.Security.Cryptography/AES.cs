using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Esiur.Security.Cryptography
{
    public class AES : ISymetricCipher
    {
        Aes aes = Aes.Create();

        public ushort Identifier => 1;
        
        public byte[] Decrypt(byte[] data)
        {
            throw new NotImplementedException();
        }

        public byte[] Encrypt(byte[] data)
        {
            throw new NotImplementedException();
        }

        public byte[] SetKey(byte[] key)
        {
            //aes.Key = key;
            //aes.IV = key;
            
            throw new NotImplementedException();
        }
    }
}
