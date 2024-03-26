using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Esiur.Security.Cryptography;
using Esiur.Data;
using System.Linq;

namespace Esiur.Security.Cryptography
{
    public class ECDH : IKeyExchanger
    {
        public ushort Identifier => 1;

        ECDiffieHellman ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.brainpoolP256r1);

        public byte[] ComputeSharedKey(byte[] key)
        {
            var x = key.Clip(0, (uint)key.Length / 2);
            var y = key.Clip((uint)key.Length / 2, (uint)key.Length / 2);

            ECParameters parameters = new ECParameters
            {
                Curve = ECCurve.NamedCurves.brainpoolP256r1,
                Q = {
                    X = x,
                    Y = y, 
                }
            };

            byte[] derivedKey;
            using (ECDiffieHellman peer = ECDiffieHellman.Create(parameters))
            using (ECDiffieHellmanPublicKey peerPublic = peer.PublicKey)
            {
                return derivedKey = ecdh.DeriveKeyMaterial(peerPublic);
            }

        }

        public byte[] GetPublicKey()
        {
            var kp = ecdh.PublicKey.ExportParameters();

            var key = DC.Combine(kp.Q.X, 0, (uint)kp.Q.X.Length, kp.Q.Y, 0, (uint)kp.Q.Y.Length);

            return key;
        }
    }
}
