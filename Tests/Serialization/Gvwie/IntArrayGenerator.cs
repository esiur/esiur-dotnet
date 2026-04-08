using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Tests.Gvwie;

public static class IntArrayGenerator
{



    private static readonly Random rng = new Random(24241564);

    /// <summary>
    /// Generate an array composed of ascending runs (consecutive integers).
    /// Example output: [1,2,3,4,5, 5001,5002,5003, 10000001,10000002,...]
    /// Parameters:
    ///  - length: total array length
    ///  - minRunSize / maxRunSize: inclusive bounds for run lengths
    ///  - minValue / maxValue: allowed value range for run starts
    ///  - allowNegative: if false, generated values will be non-negative
    ///  - minGap / maxGap: approximate gap between runs (large gaps produce the jump examples)
    /// </summary>
    public static long[] GenerateRuns(int length,
        int minRunSize = 3,
        int maxRunSize = 8,
        long minValue = -10_000_000L,
        long maxValue = 10_000_000L,
        bool allowNegative = true,
        long minGap = 1_000L,
        long maxGap = 10_000_000L)
    {
        if (length <= 0)
            return Array.Empty<long>();

        if (minRunSize < 1) minRunSize = 1;
        if (maxRunSize < minRunSize) maxRunSize = minRunSize;

        // If negative runs not allowed, clamp minValue to 0
        if (!allowNegative && minValue < 0) minValue = 0;

        var data = new long[length];
        int idx = 0;
        long prevEnd = long.MinValue;

        while (idx < length)
        {
            // choose run size
            int runSize = rng.Next(minRunSize, maxRunSize + 1);
            if (idx + runSize > length)
                runSize = length - idx;

            // pick a start. Aim for gaps between runs by either taking a random value
            // or basing on previous end + gap. Try a few times to avoid accidental small gaps.
            long start = 0;
            long attemptUpper = maxValue - runSize; // inclusive exclusive handled by NextInt64
            if (attemptUpper < minValue) attemptUpper = minValue;

            bool picked = false;
            for (int attempt = 0; attempt < 10 && !picked; attempt++)
            {
                // decide whether to use a jump based on minGap/maxGap or pick random
                if (prevEnd != long.MinValue && rng.NextDouble() < 0.7)
                {
                    // generate a gap and place start after prevEnd + gap
                    long gap = rng.NextInt64(minGap, Math.Max(minGap + 1, maxGap));
                    long candidate = prevEnd + gap;
                    // if candidate within allowed bounds adjust to fit
                    if (candidate >= minValue && candidate <= attemptUpper)
                    {
                        start = candidate;
                        picked = true;
                        break;
                    }
                }

                // fallback: pick random start in allowed bounds
                start = rng.NextInt64(minValue, attemptUpper + 1);
                // avoid being too close to previous run end if present
                if (prevEnd == long.MinValue || Math.Abs(start - prevEnd) >= minGap)
                {
                    picked = true;
                    break;
                }
            }

            if (!picked)
            {
                // final fallback: clamp to bounds
                start = Math.Max(minValue, Math.Min(attemptUpper, prevEnd + minGap));
            }

            // fill the run with consecutive values, careful with overflow
            for (int j = 0; j < runSize; j++)
            {
                long val;
                try
                {
                    checked
                    {
                        val = start + j;
                    }
                }
                catch (OverflowException)
                {
                    // clamp if overflow occurs
                    val = (start >= 0) ? long.MaxValue - (runSize - j - 1) : long.MinValue + (runSize - j - 1);
                }

                data[idx++] = val;
            }

            prevEnd = data[idx - 1];
        }

        return data;
    }


    // Generate random int array of given length and distribution
    public static int[] GenerateInt32(int length, GeneratorPattern pattern = GeneratorPattern.Uniform)
    {
        var data = new int[length];

        switch (pattern)
        {
            case GeneratorPattern.Uniform:
                // Random values in [-range, range]
                for (int i = 0; i < length; i++)
                    data[i] = rng.Next(int.MinValue, int.MaxValue);
                break;

            case GeneratorPattern.Positive:
                for (int i = 0; i < length; i++)
                    data[i] = rng.Next(0, int.MaxValue);
                break;

            case GeneratorPattern.Negative:
                for (int i = 0; i < length; i++)
                    data[i] = -rng.Next(int.MinValue, 0);
                break;

            case GeneratorPattern.Alternating:
                for (int i = 0; i < length; i++)
                {
                    int val = rng.Next(0, int.MaxValue);
                    data[i] = (i % 2 == 0) ? val : -val;
                }
                break;

            case GeneratorPattern.Small:
                // Focused on small magnitudes to test ZigZag fast path
                for (int i = 0; i < length; i++)
                    data[i] = rng.Next(-64, 65);
                break;


            case GeneratorPattern.Ascending:
                {
                    int start = rng.Next(int.MinValue, int.MaxValue);
                    for (int i = 0; i < length; i++)
                        data[i] = start + i;
                }
                break;

            case GeneratorPattern.Clustering:
                {
                    // Build ascending runs and cast to int, clamping to int bounds
                    var runs = GenerateRuns(length, 3, 50, ((long)int.MinValue), (long)int.MaxValue, true);
                    for (int i = 0; i < length; i++)
                    {
                        long v = runs[i];
                        if (v > int.MaxValue) data[i] = int.MaxValue;
                        else if (v < int.MinValue) data[i] = int.MinValue;
                        else data[i] = (int)v;
                    }
                }
                break;


            default:
                throw new ArgumentException($"Unknown pattern: {pattern}");
        }

        return data;
    }


    // Generate random int array of given length and distribution
    public static uint[] GenerateUInt32(int length, GeneratorPattern pattern = GeneratorPattern.Uniform,
        uint range = uint.MaxValue)
    {

        var data = new uint[length];

        switch (pattern)
        {
            case GeneratorPattern.Uniform:
                // Random values in [-range, range]
                for (int i = 0; i < length; i++)
                    data[i] = (uint)rng.NextInt64(0, (long)range);
                break;

            case GeneratorPattern.Small:
                // Focused on small magnitudes to test ZigZag fast path
                for (int i = 0; i < length; i++)
                    data[i] = (uint)rng.Next(0, 127);
                break;

            case GeneratorPattern.Clustering:
                {
                    // Generate runs in a non-negative range and cast to uint
                    var runs = GenerateRuns(length, 3, 50, 0, (long)range, false);
                    for (int i = 0; i < length; i++)
                    {
                        long v = runs[i];
                        if (v < 0) data[i] = 0u;
                        else if ((ulong)v > uint.MaxValue) data[i] = uint.MaxValue;
                        else data[i] = (uint)v;
                    }
                }
                break;


            case GeneratorPattern.Ascending:
                uint start = (uint)rng.NextInt64(0, (long)range);
                for (uint i = 0; i < length; i++)
                    data[i] = start + i;

                break;

            default:
                throw new ArgumentException($"Unknown pattern: {pattern}");
        }

        return data;
    }

    // Generate random int array of given length and distribution
    public static ulong[] GenerateUInt64(int length, GeneratorPattern pattern = GeneratorPattern.Uniform)
    {
        var data = new ulong[length];

        switch (pattern)
        {
            case GeneratorPattern.Uniform:
                // Random values in [-range, range]
                for (int i = 0; i < length; i++)
                    data[i] = (ulong)rng.NextInt64();
                break;

            case GeneratorPattern.Small:
                // Focused on small magnitudes to test ZigZag fast path
                for (int i = 0; i < length; i++)
                    data[i] = (uint)rng.Next(0, 127);
                break;


            case GeneratorPattern.Ascending:

                uint start = (uint)rng.NextInt64();
                for (uint i = 0; i < length; i++)
                    data[i] = start + i;

                break;

            case GeneratorPattern.Clustering:
                {
                    var runs = GenerateRuns(length, 3, 50, 0, long.MaxValue, false);
                    for (int i = 0; i < length; i++)
                    {
                        long v = runs[i];
                        if (v < 0) data[i] = 0UL;
                        else data[i] = (ulong)v;
                    }
                }
                break;

            default:
                throw new ArgumentException($"Unknown pattern: {pattern}");
        }

        return data;
    }

    public static uint[] GenerateUInt16(int length, GeneratorPattern pattern = GeneratorPattern.Uniform)
    {
        var data = new uint[length];

        switch (pattern)
        {
            case GeneratorPattern.Uniform:
                // Random values in [-range, range]
                for (int i = 0; i < length; i++)
                    data[i] = (ushort)rng.Next(0, ushort.MaxValue);
                break;

            case GeneratorPattern.Small:
                // Focused on small magnitudes to test ZigZag fast path
                for (int i = 0; i < length; i++)
                    data[i] = (uint)rng.Next(0, 127);
                break;


            case GeneratorPattern.Ascending:

                var start = (ushort)rng.Next(0, ushort.MaxValue);
                for (uint i = 0; i < length; i++)
                    data[i] = start + i;

                break;

            case GeneratorPattern.Clustering:
                {
                    var runs = GenerateRuns(length, 3, 50, 0, ushort.MaxValue, false);
                    for (int i = 0; i < length; i++)
                    {
                        long v = runs[i];
                        if (v < 0) data[i] = 0u;
                        else if (v > ushort.MaxValue) data[i] = ushort.MaxValue;
                        else data[i] = (uint)v;
                    }
                }
                break;

            default:
                throw new ArgumentException($"Unknown pattern: {pattern}");
        }

        return data;
    }

    // Generate random int array of given length and distribution
    public static long[] GenerateInt64(int length, GeneratorPattern pattern = GeneratorPattern.Uniform,
        long range = long.MaxValue)
    {
        var data = new long[length];

        switch (pattern)
        {
            case GeneratorPattern.Uniform:
                // Random values in [-range, range]
                for (int i = 0; i < length; i++)
                    data[i] = rng.NextInt64(-range, range);
                break;

            case GeneratorPattern.Positive:
                for (int i = 0; i < length; i++)
                    data[i] = rng.NextInt64(0, range);
                break;

            case GeneratorPattern.Negative:
                for (int i = 0; i < length; i++)
                    data[i] = -rng.NextInt64(0, range);
                break;

            case GeneratorPattern.Alternating:
                for (int i = 0; i < length; i++)
                {
                    var val = rng.NextInt64(0, range);
                    data[i] = (i % 2 == 0) ? val : -val;
                }
                break;

            case GeneratorPattern.Small:
                // Focused on small magnitudes to test ZigZag fast path
                for (int i = 0; i < length; i++)
                    data[i] = rng.NextInt64(-64, 65);
                break;


            case GeneratorPattern.Ascending:
                {
                    var start = rng.NextInt64(-range, range);
                    for (int i = 0; i < length; i++)
                        data[i] = start + i;
                }
                break;

            case GeneratorPattern.Clustering:
                {
                    var runs = GenerateRuns(length, 3, 50, -range, range, true);
                    for (int i = 0; i < length; i++)
                        data[i] = runs[i];
                }
                break;

            default:
                throw new ArgumentException($"Unknown pattern: {pattern}");
        }

        return data;
    }

    public static short[] GenerateInt16(int length, GeneratorPattern pattern = GeneratorPattern.Uniform,
        short range = short.MaxValue)
    {
        var data = new short[length];

        switch (pattern)
        {
            case GeneratorPattern.Uniform:
                for (int i = 0; i < length; i++)
                    data[i] = (short)rng.Next(-range, range + 1);
                break;

            case GeneratorPattern.Positive:
                for (int i = 0; i < length; i++)
                    data[i] = (short)rng.Next(0, range + 1);
                break;

            case GeneratorPattern.Negative:
                for (int i = 0; i < length; i++)
                    data[i] = (short)(-rng.Next(0, range + 1));
                break;

            case GeneratorPattern.Alternating:
                for (int i = 0; i < length; i++)
                {
                    short val = (short)rng.Next(0, range + 1);
                    data[i] = (i % 2 == 0) ? val : (short)-val;
                }
                break;

            case GeneratorPattern.Small:
                for (int i = 0; i < length; i++)
                    data[i] = (short)rng.Next(-64, 65);
                break;


            case GeneratorPattern.Ascending:
                {
                    short start = (short)rng.Next(-range, range);
                    for (int i = 0; i < length; i++)
                        data[i] = (short)(start + i);
                }
                break;

            case GeneratorPattern.Clustering:
                {
                    var runs = GenerateRuns(length, 3, 50, -range, range, true);
                    for (int i = 0; i < length; i++)
                    {
                        long v = runs[i];
                        if (v > short.MaxValue) data[i] = short.MaxValue;
                        else if (v < short.MinValue) data[i] = short.MinValue;
                        else data[i] = (short)v;
                    }
                }
                break;

            default:
                throw new ArgumentException($"Unknown pattern: {pattern}");
        }

        return data;
    }
}