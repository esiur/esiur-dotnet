using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Net.Ppap
{
    internal class KeyGenerator
    {
        public static (byte[], byte[]) Gen(MLKemParameters parameters = null)
        {
            var random = new RandomGenerator();
            var keyGenParameters = new MLKemKeyGenerationParameters(random, parameters ?? MLKemParameters.ml_kem_768);

            var kyberKeyPairGenerator = new MLKemKeyPairGenerator();
            kyberKeyPairGenerator.Init(keyGenParameters);

            var keys = kyberKeyPairGenerator.GenerateKeyPair();
            return ((keys.Private as MLKemPrivateKeyParameters).GetEncoded(),
                (keys.Public as MLKemPublicKeyParameters).GetEncoded());
        }


        public static (byte[], byte[]) GenS(byte[] username, byte[] password, byte[] registrationNonce, Argon2Parameters argon2parameters = null, MLKemParameters mlkemParameters = null)
        {

            var secret = new byte[username.Length + password.Length + registrationNonce.Length];
            Buffer.BlockCopy(username, 0, secret, 0, username.Length);
            Buffer.BlockCopy(password, 0, secret, username.Length, password.Length);
            Buffer.BlockCopy(registrationNonce, 0, secret, username.Length + password.Length, registrationNonce.Length);

            var output = new byte[64];
            //Argon2id.DeriveKey(output, secret, registrationNonce, ArgonIterations, ArgonMemory * 1024);

            var argon2 = new Argon2BytesGenerator();
            var argon2params = argon2parameters ?? new Argon2Parameters.Builder(Argon2Parameters.Argon2id)
                .WithSalt(registrationNonce)
                .WithMemoryAsKB(1024 * 10)
                .WithIterations(3)
                .WithParallelism(1)
                .Build();

            argon2.Init(argon2params);
            var output2 = new byte[64];
            argon2.GenerateBytes(secret, output);


            var random = new DeterministicGenerator(output);
            var keyGenParameters = new MLKemKeyGenerationParameters(random, mlkemParameters ?? MLKemParameters.ml_kem_768);

            var kyberKeyPairGenerator = new MLKemKeyPairGenerator();
            kyberKeyPairGenerator.Init(keyGenParameters);

            var keys = kyberKeyPairGenerator.GenerateKeyPair();
            return ((keys.Private as MLKemPrivateKeyParameters).GetEncoded(),
                (keys.Public as MLKemPublicKeyParameters).GetEncoded());

        }
    }
}
