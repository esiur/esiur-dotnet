using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Tests.Serialization;

public static class IntArrayGenerator
{
    private static readonly Random rng = new Random(24241564);


    public static long[] GenerateInt32Run(int length)
    {
        var data = new long[length];

        int i = 0;
        var inSmallRange = true;
        var inShortRange = false;
        var inLargeRange = false;
        var inLongRange = false;

        long range = 30;

        while (i < length)
        {
            // stay same range
            if (rng.NextDouble() < 0.9)
            {
                if (inSmallRange)
                    data[i++] = rng.Next(-64, 65);
                else if (inShortRange)
                    data[i++] = rng.NextInt64(range - 100, range + 100);
                else if (inLargeRange)
                    data[i++] = rng.NextInt64(range - 1000, range + 1000);
                else if (inLongRange)
                    data[i++] = rng.NextInt64(range - 10000, range + 10000);
            }
            else
            {
                // switch range
                var rand = rng.NextDouble();
                if (rand < 0.25)
                {
                    inSmallRange = true;
                    inShortRange = false;
                    inLargeRange = false;
                    inLongRange = false;
                    data[i++] = rng.Next(-64, 65);
                }
                else if (rand < 0.50)
                {
                    inSmallRange = false;
                    inShortRange = true;
                    inLargeRange = false;
                    inLongRange = false;
                    range = rng.NextInt64(1000, short.MaxValue);
                    data[i++] = rng.NextInt64(range - 100, range + 100);
                }
                else if (rand < 0.75)
                {
                    inSmallRange = false;
                    inShortRange = false;
                    inLargeRange = true;
                    inLongRange = false;
                    range = rng.NextInt64(1000, int.MaxValue);
                    data[i++] = rng.NextInt64(range - 1000, range + 1000);
                }
                else
                {
                    inSmallRange = false;
                    inShortRange = false;
                    inLargeRange = false;
                    inLongRange = true;
                    range = rng.NextInt64(10000, long.MaxValue);
                    data[i++] = rng.NextInt64(range - 10000, range + 10000);

                }
            }

        }

        return data;
    }

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


    // Generate random int array of given length and distribution
    public static uint[] GenerateUInt32(int length, string pattern = "uniform",
        uint range = uint.MaxValue)
    {

        var data = new uint[length];

        switch (pattern.ToLower())
        {
            case "uniform":
                // Random values in [-range, range]
                for (int i = 0; i < length; i++)
                    data[i] = (uint)rng.NextInt64(0, (long)range);
                break;

            case "small":
                // Focused on small magnitudes to test ZigZag fast path
                for (int i = 0; i < length; i++)
                    data[i] = (uint)rng.Next(0, 127);
                break;


            case "ascending":
                {
                    uint start = (uint)rng.NextInt64(0, (long)range);
                    for (uint i = 0; i < length; i++)
                        data[i] = start + i;
                }
                break;

            default:
                throw new ArgumentException($"Unknown pattern: {pattern}");
        }

        return data;
    }

    // Generate random int array of given length and distribution
    public static ulong[] GenerateUInt64(int length, string pattern = "uniform")
    {
        var data = new ulong[length];

        switch (pattern.ToLower())
        {
            case "uniform":
                // Random values in [-range, range]
                for (int i = 0; i < length; i++)
                    data[i] = (ulong)rng.NextInt64();
                break;

            case "small":
                // Focused on small magnitudes to test ZigZag fast path
                for (int i = 0; i < length; i++)
                    data[i] = (uint)rng.Next(0, 127);
                break;


            case "ascending":
                {
                    uint start = (uint)rng.NextInt64();
                    for (uint i = 0; i < length; i++)
                        data[i] = start + i;
                }
                break;

            default:
                throw new ArgumentException($"Unknown pattern: {pattern}");
        }

        return data;
    }

    public static uint[] GenerateUInt16(int length, string pattern = "uniform",
    ushort range = ushort.MaxValue)
    {
        var data = new uint[length];

        switch (pattern.ToLower())
        {
            case "uniform":
                // Random values in [-range, range]
                for (int i = 0; i < length; i++)
                    data[i] = (ushort)rng.Next(0, range);
                break;

            case "small":
                // Focused on small magnitudes to test ZigZag fast path
                for (int i = 0; i < length; i++)
                    data[i] = (uint)rng.Next(0, 127);
                break;


            case "ascending":
                {
                    var start = (ushort)rng.Next(0, range);
                    for (uint i = 0; i < length; i++)
                        data[i] = start + i;
                }
                break;

            default:
                throw new ArgumentException($"Unknown pattern: {pattern}");
        }

        return data;
    }

    // Generate random int array of given length and distribution
    public static long[] GenerateInt64(int length, string pattern = "uniform", 
        long range = long.MaxValue)
    {
        var data = new long[length];

        switch (pattern.ToLower())
        {
            case "uniform":
                // Random values in [-range, range]
                for (int i = 0; i < length; i++)
                    data[i] = rng.NextInt64(-range, range);
                break;

            case "positive":
                for (int i = 0; i < length; i++)
                    data[i] = rng.NextInt64(0, range);
                break;

            case "negative":
                for (int i = 0; i < length; i++)
                    data[i] = -rng.NextInt64(0, range);
                break;

            case "alternating":
                for (int i = 0; i < length; i++)
                {
                    var val = rng.NextInt64(0, range);
                    data[i] = (i % 2 == 0) ? val : -val;
                }
                break;

            case "small":
                // Focused on small magnitudes to test ZigZag fast path
                for (int i = 0; i < length; i++)
                    data[i] = rng.NextInt64(-64, 65);
                break;


            case "ascending":
                {
                    var start = rng.NextInt64(-range, range);
                    for (int i = 0; i < length; i++)
                        data[i] = start + i;
                }
                break;

            default:
                throw new ArgumentException($"Unknown pattern: {pattern}");
        }

        return data;
    }

    public static short[] GenerateInt16(int length, string pattern = "uniform", 
        short range = short.MaxValue)
    {
        var data = new short[length];

        switch (pattern.ToLower())
        {
            case "uniform":
                for (int i = 0; i < length; i++)
                    data[i] = (short)rng.Next(-range, range + 1);
                break;

            case "positive":
                for (int i = 0; i < length; i++)
                    data[i] = (short)rng.Next(0, range + 1);
                break;

            case "negative":
                for (int i = 0; i < length; i++)
                    data[i] = (short)(-rng.Next(0, range + 1));
                break;

            case "alternating":
                for (int i = 0; i < length; i++)
                {
                    short val = (short)rng.Next(0, range + 1);
                    data[i] = (i % 2 == 0) ? val : (short)-val;
                }
                break;

            case "small":
                for (int i = 0; i < length; i++)
                    data[i] = (short)rng.Next(-64, 65);
                break;


            case "ascending":
                {
                    short start = (short)rng.Next(-range, range);
                    for (int i = 0; i < length; i++)
                        data[i] = (short)(start + i);
                }
                break;

            default:
                throw new ArgumentException($"Unknown pattern: {pattern}");
        }

        return data;
    }
}