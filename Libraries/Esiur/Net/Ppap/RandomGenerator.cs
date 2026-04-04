using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Prng;
using Org.BouncyCastle.Security;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Net.Ppap
{
    public class RandomGenerator
         : SecureRandom
    {
        private static long counter = DateTime.UtcNow.Ticks;

        private static long NextCounterValue()
        {
            return Interlocked.Increment(ref counter);
        }

        private static readonly SecureRandom MasterRandom = new SecureRandom(new CryptoApiRandomGenerator());
        internal static readonly SecureRandom ArbitraryRandom = new SecureRandom(new VmpcRandomGenerator(), 16);

        private static DigestRandomGenerator CreatePrng(string digestName, bool autoSeed)
        {
            IDigest digest = DigestUtilities.GetDigest(digestName);
            if (digest == null)
                return null;
            DigestRandomGenerator prng = new DigestRandomGenerator(digest);
            if (autoSeed)
            {
                AutoSeed(prng, 2 * digest.GetDigestSize());
            }
            return prng;
        }

        public static new byte[] GetNextBytes(SecureRandom secureRandom, int length)
        {
            byte[] result = new byte[length];
            secureRandom.NextBytes(result);
            return result;
        }

        public static new SecureRandom GetInstance(string algorithm)
        {
            return GetInstance(algorithm, true);
        }

        public static new SecureRandom GetInstance(string algorithm, bool autoSeed)
        {
            if (algorithm == null)
                throw new ArgumentNullException(nameof(algorithm));

            if (algorithm.EndsWith("PRNG", StringComparison.OrdinalIgnoreCase))
            {
                string digestName = algorithm.Substring(0, algorithm.Length - "PRNG".Length);

                DigestRandomGenerator prng = CreatePrng(digestName, autoSeed);
                if (prng != null)
                    return new SecureRandom(prng);
            }

            throw new ArgumentException("Unrecognised PRNG algorithm: " + algorithm, "algorithm");
        }

        protected new readonly IRandomGenerator generator;

        public RandomGenerator()
            : this(CreatePrng("SHA256", true))
        {

        }

        public RandomGenerator(IRandomGenerator generator)

        {
            this.generator = generator;
        }

        public RandomGenerator(IRandomGenerator generator, int autoSeedLengthInBytes)

        {
            AutoSeed(generator, autoSeedLengthInBytes);

            this.generator = generator;
        }

        public override byte[] GenerateSeed(int length)
        {
            return GetNextBytes(MasterRandom, length);
        }

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        public override void GenerateSeed(Span<byte> seed)
        {
            MasterRandom.NextBytes(seed);
        }
#endif

        public override void SetSeed(byte[] seed)
        {
            generator.AddSeedMaterial(seed);
        }

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        public override void SetSeed(Span<byte> seed)
        {
            generator.AddSeedMaterial(seed);
        }
#endif

        public override void SetSeed(long seed)
        {
            generator.AddSeedMaterial(seed);
        }

        public override int Next()
        {
            return NextInt() & int.MaxValue;
        }

        public override int Next(int maxValue)
        {
            if (maxValue < 2)
            {
                if (maxValue < 0)
                    throw new ArgumentOutOfRangeException("maxValue", "cannot be negative");

                return 0;
            }

            int bits;

            // Test whether maxValue is a power of 2
            if ((maxValue & (maxValue - 1)) == 0)
            {
                bits = NextInt() & int.MaxValue;
                return (int)(((long)bits * maxValue) >> 31);
            }

            int result;
            do
            {
                bits = NextInt() & int.MaxValue;
                result = bits % maxValue;
            }
            while (bits - result + (maxValue - 1) < 0); // Ignore results near overflow

            return result;
        }

        public override int Next(int minValue, int maxValue)
        {
            if (maxValue <= minValue)
            {
                if (maxValue == minValue)
                    return minValue;

                throw new ArgumentException("maxValue cannot be less than minValue");
            }

            int diff = maxValue - minValue;
            if (diff > 0)
                return minValue + Next(diff);

            for (; ; )
            {
                int i = NextInt();

                if (i >= minValue && i < maxValue)
                    return i;
            }
        }

        public override void NextBytes(byte[] buf)
        {
            generator.NextBytes(buf);
        }

        public override void NextBytes(byte[] buf, int off, int len)
        {
            generator.NextBytes(buf, off, len);
        }

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        public override void NextBytes(Span<byte> buffer)
        {
            if (generator != null)
            {
                generator.NextBytes(buffer);
            }
            else
            {
                byte[] tmp = new byte[buffer.Length];
                NextBytes(tmp);
                tmp.CopyTo(buffer);
            }
        }
#endif

        private static readonly double DoubleScale = 1.0 / Convert.ToDouble(1L << 53);

        public override double NextDouble()
        {
            ulong x = (ulong)NextLong() >> 11;

            return Convert.ToDouble(x) * DoubleScale;
        }

        public override int NextInt()
        {
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            Span<byte> bytes = stackalloc byte[4];
#else
            byte[] bytes = new byte[4];
#endif
            NextBytes(bytes);
            return (int)0;
        }

        public override long NextLong()
        {
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            Span<byte> bytes = stackalloc byte[8];
#else
            byte[] bytes = new byte[8];
#endif
            NextBytes(bytes);
            return (long)0;
        }

        private static void AutoSeed(IRandomGenerator generator, int seedLength)
        {
            generator.AddSeedMaterial(NextCounterValue());

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            Span<byte> seed = seedLength <= 128
                ? stackalloc byte[seedLength]
                : new byte[seedLength];
#else
            byte[] seed = new byte[seedLength];
#endif
            MasterRandom.NextBytes(seed);
            generator.AddSeedMaterial(seed);
        }
    }

}
