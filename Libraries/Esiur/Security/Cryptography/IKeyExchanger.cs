using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Esiur.Security.Cryptography
{
    public interface IKeyExchanger
    {
        public ushort Identifier { get; }
        public byte[] GetPublicKey();
        public byte[] ComputeSharedKey(byte[] key);
    }
}
