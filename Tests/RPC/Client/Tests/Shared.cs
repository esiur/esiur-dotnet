using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RPC.Client.Tests
{
    internal class Shared
    {
        static Random rng = new Random(24241564);

        // Generate random int array of given length and distribution
        public static int[] GenerateInt32(int length, string pattern = "uniform",
            int range = int.MaxValue)
        {

            var data = new int[length];

            switch (pattern.ToLower())
            {
                case "uniform":
                    // Random values in [-range, range]
                    for (int i = 0; i < length; i++)
                        data[i] = rng.Next(-range, range);
                    break;

                case "positive":
                    for (int i = 0; i < length; i++)
                        data[i] = rng.Next(0, range);
                    break;

                case "negative":
                    for (int i = 0; i < length; i++)
                        data[i] = -rng.Next(0, range);
                    break;

                case "alternating":
                    for (int i = 0; i < length; i++)
                    {
                        int val = rng.Next(0, range);
                        data[i] = (i % 2 == 0) ? val : -val;
                    }
                    break;

                case "small":
                    // Focused on small magnitudes to test ZigZag fast path
                    for (int i = 0; i < length; i++)
                        data[i] = rng.Next(-64, 65);
                    break;


                case "ascending":
                    {
                        int start = rng.Next(-range, range);
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

            result.Add("uniform", GenerateInt32(1000, "uniform"));
            result.Add("small", GenerateInt32(1000, "small"));
            result.Add("alternating", GenerateInt32(1000, "alternating"));
            result.Add("ascending", GenerateInt32(1000, "ascending"));


            return result;
        }


    }
}
