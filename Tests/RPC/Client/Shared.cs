using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Tests.RPC.Client
{
    internal class Shared
    {
        static Random rng = new Random(24241564);

        // Generate random int array of given length and distribution
        public static int[] GenerateInt32(int length, string pattern = "uniform",
            int range = int.MaxValue, Random? random = null)
        {

            var data = new int[length];
            var source = random ?? rng;

            switch (pattern.ToLower())
            {
                case "uniform":
                    // Random values in [-range, range]
                    for (int i = 0; i < length; i++)
                        data[i] = source.Next(-range, range);
                    break;

                case "positive":
                    for (int i = 0; i < length; i++)
                        data[i] = source.Next(0, range);
                    break;

                case "negative":
                    for (int i = 0; i < length; i++)
                        data[i] = -source.Next(0, range);
                    break;

                case "alternating":
                    for (int i = 0; i < length; i++)
                    {
                        int val = source.Next(0, range);
                        data[i] = (i % 2 == 0) ? val : -val;
                    }
                    break;

                case "small":
                    // Focused on small magnitudes to test ZigZag fast path
                    for (int i = 0; i < length; i++)
                        data[i] = source.Next(-64, 65);
                    break;


                case "ascending":
                    {
                        int start = source.Next(-range, range);
                        for (int i = 0; i < length; i++)
                            data[i] = start + i;
                    }
                    break;

                default:
                    throw new ArgumentException($"Unknown pattern: {pattern}");
            }

            return data;
        }

        public static Dictionary<string, byte[]> BuildBytesWorkLoads(int seed = 1000)
        {
            var result = new Dictionary<string, byte[]>();

            var r = new Random(seed);


            // Small 
            {
                var small = new byte[100];
                r.NextBytes(small);
                result.Add("Small", small);
            }

            // Medium 
            {
                var medium = new byte[1000];
                r.NextBytes(medium);
                result.Add("Medium", medium);
            }

            // Large 
            {
                var large = new byte[1000000];
                r.NextBytes(large);
                result.Add("Large", large);
            }

            return result;
        }



        public static Dictionary<string, int[]> BuildIntWorkloads(int seed = 1000)
        {
            var result = new Dictionary<string, int[]>();
            var r = new Random(seed);

            result.Add("uniform", GenerateInt32(1000, "uniform", random: r));
            result.Add("small", GenerateInt32(1000, "small", random: r));
            result.Add("alternating", GenerateInt32(1000, "alternating", random: r));
            result.Add("ascending", GenerateInt32(1000, "ascending", random: r));


            return result;
        }


    }
}
