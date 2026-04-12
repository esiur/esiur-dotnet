using Esiur.Data.Gvwie;
using FlatSharp;
using FlatSharp.Attributes;
using MessagePack;
using MongoDB.Bson;
using Org.BouncyCastle.Asn1.X509;
using PeterO.Cbor;
using ProtoBuf;
using SolTechnology.Avro;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Esiur.Tests.Gvwie
{
    [FlatBufferTable]
    public class ArrayRoot<T>
    {
        // Field index must be stable; start at 0
        [FlatBufferItem(0)]
        public virtual IList<T>? Values { get; set; }
    }


    internal class IntArrayRunner
    {
        public void Run()
        {

            const int TEST_ITERATIONS = 100;
            const int SAMPLE_SIZE = 100;

            Console.WriteLine(",Esiur,Aligned,FlatBuffer,ProtoBuffer,MessagePack,BSON,CBOR,Avro,Optimal");


            Console.Write("Cluster (Int32);");

            PrintAverage(
                Average(() => CompareInt(IntArrayGenerator.GenerateInt32(SAMPLE_SIZE, GeneratorPattern.Clustering)), TEST_ITERATIONS)
            );

            Console.Write("Positive (Int32);");

            PrintAverage(
                Average(() => CompareInt(IntArrayGenerator.GenerateInt32(SAMPLE_SIZE, GeneratorPattern.Uniform)), TEST_ITERATIONS)
            );

            Console.Write("Negative (Int32);");
            PrintAverage(
                Average(() => CompareInt(IntArrayGenerator.GenerateInt32(SAMPLE_SIZE, GeneratorPattern.Negative)), TEST_ITERATIONS)
            );

            Console.Write("Small (Int32);");
            PrintAverage(
                Average(() => CompareInt(IntArrayGenerator.GenerateInt32(SAMPLE_SIZE, GeneratorPattern.Small)), TEST_ITERATIONS)
            );

            Console.Write("Alternating (Int32);");
            PrintAverage(
                Average(() => CompareInt(IntArrayGenerator.GenerateInt32(SAMPLE_SIZE, GeneratorPattern.Alternating)), TEST_ITERATIONS)
            );

            Console.Write("Ascending (Int32);");

            PrintAverage(
                Average(() => CompareInt(IntArrayGenerator.GenerateInt32(SAMPLE_SIZE, GeneratorPattern.Ascending)), TEST_ITERATIONS)
            );

            Console.Write("Int64;");

            PrintAverage( 
                Average(() => CompareInt(IntArrayGenerator.GenerateInt64(SAMPLE_SIZE)), TEST_ITERATIONS)
            );

            Console.Write("Int32;");

            PrintAverage(
                Average(() => CompareInt(IntArrayGenerator.GenerateInt32(SAMPLE_SIZE)), TEST_ITERATIONS)
            );

            Console.Write("Int16;");

            PrintAverage(
                Average(() => CompareInt(IntArrayGenerator.GenerateInt16(SAMPLE_SIZE)), TEST_ITERATIONS)
            );

            Console.Write("UInt64;");

            PrintAverage(
                Average(() => CompareInt(IntArrayGenerator.GenerateUInt64(SAMPLE_SIZE)), TEST_ITERATIONS)
            );

            Console.Write("UInt32;");

            PrintAverage(
                Average(() => CompareInt(IntArrayGenerator.GenerateUInt32(SAMPLE_SIZE)), TEST_ITERATIONS)
            );

            Console.Write("UInt16;");

            PrintAverage(
                Average(() => CompareInt(IntArrayGenerator.GenerateUInt16(SAMPLE_SIZE)), TEST_ITERATIONS)
            );
        }

        // Generate CSV suitable for Office Word chart where the sample size varies.
        // Produces a CSV with header: SampleSize;Esiur;FlatBuffer;ProtoBuffer;MessagePack;BSON;CBOR;Avro;Optimal
        public void RunChart()
        {
            var sizes = Enumerable.Range(0, 21)
                              .Select(i => (int)Math.Pow(2, i))
                              .ToArray();


            // Define generators to evaluate. Each entry maps a name to a function that
            // given a sample size returns the averages (double[]) by calling Average(...).
            var generators = new List<(string name, Func<int, int, double[]> fn)>()
            {                             
                ("Int32_Clustering", (size, iterations) => Average(() => CompareInt(IntArrayGenerator.GenerateInt32(size, GeneratorPattern.Clustering)), iterations)),
                ("Int32_Positive", (size, iterations) => Average(() => CompareInt(IntArrayGenerator.GenerateInt32(size, GeneratorPattern.Positive)), iterations)),
                ("Int32_Negative", (size, iterations) => Average(() => CompareInt(IntArrayGenerator.GenerateInt32(size, GeneratorPattern.Negative)), iterations)),
                ("Int32_Small", (size, iterations) => Average(() => CompareInt(IntArrayGenerator.GenerateInt32(size, GeneratorPattern.Small)), iterations)),
                ("Int32_Alternating", (size, iterations) => Average(() => CompareInt(IntArrayGenerator.GenerateInt32(size, GeneratorPattern.Alternating)), iterations)),
                ("Int32_Ascending", (size, iterations) => Average(() => CompareInt(IntArrayGenerator.GenerateInt32(size, GeneratorPattern.Ascending)), iterations)),
                ("Int32", (size, iterations) => Average(() => CompareInt(IntArrayGenerator.GenerateInt32(size)), iterations)),
                ("UInt32", (size, iterations) => Average(() => CompareInt(IntArrayGenerator.GenerateUInt32(size)), iterations)),

                ("Int16", (size, iterations) => Average(() => CompareInt(IntArrayGenerator.GenerateInt16(size)), iterations)),
                ("UInt16", (size, iterations) => Average(() => CompareInt(IntArrayGenerator.GenerateUInt16(size)), iterations)),
                ("Int64", (size, iterations) => Average(() => CompareInt(IntArrayGenerator.GenerateInt64(size)), iterations)),
                ("UInt64", (size, iterations) => Average(() => CompareInt(IntArrayGenerator.GenerateUInt64(size)), iterations)),

            };

            foreach (var gen in generators)
            {
                var sb = new System.Text.StringBuilder();
                var sbr = new System.Text.StringBuilder();

                sb.AppendLine("SampleSize,Esiur,Aligned,FlatBuffer,ProtoBuffer,MessagePack,BSON,CBOR,Avro,Optimal");
                sbr.AppendLine("SampleSize,Esiur,Aligned,FlatBuffer,ProtoBuffer,MessagePack,BSON,CBOR,Avro,Optimal");

                foreach (var size in sizes)
                {
                    // Choose iterations depending on size to keep total runtime reasonable
                    int iterations = 100;
                    //if (size <= 100) iterations = 1000;
                    //else if (size <= 1000) iterations = 200;
                    //else if (size <= 10000) iterations = 50;
                    //else iterations = 10;

                    Console.WriteLine($"Running {gen.name} sample size={size}, iterations={iterations}...");

                    var averages = gen.fn(size, iterations);
                    PrintAverage(averages);

                    sb.Append(size);
                    sbr.Append(size);
                    for (int i = 0; i < averages.Length; i++)
                    {
                        sb.Append(',');
                        sb.Append(Math.Round(averages[i]));
                        sbr.Append(',');
                        sbr.Append(((averages[i] - averages.Last()) / averages.Last()) * 100.0);
                    }

                    sb.AppendLine();
                    sbr.AppendLine();
                }

                var file = $"run_chart_{gen.name}.csv";
                System.IO.File.WriteAllText(file, sb.ToString());
                var file2 = $"optimal_chart_{gen.name}.csv";
                System.IO.File.WriteAllText(file2, sbr.ToString());

                Console.WriteLine($"Chart CSV written to: {file} {file2}");
            }
        }

        public static (int, int, int, int, int, int, int, int, int) CompareInt(long[] sample)
        {
            var intRoot = new ArrayRoot<long>() { Values = sample };

            var esiur = GroupInt64Codec.Encode(sample);
            var esiurAligned = GroupInt64Codec.Encode(sample, true);
            var messagePack = MessagePackSerializer.Serialize(sample);
            var flatBuffer = SerializeFlatBuffers(intRoot);

            using var ms = new MemoryStream();
            Serializer.Serialize(ms, sample);
            var protoBuffer = ms.ToArray();

            var bson = intRoot.ToBson();

            var cbor = CBORObject.FromObject(intRoot).EncodeToBytes();
            //var seq = new DerSequence(sample.Select(v => new DerInteger(v)).ToArray());
            //var ans1 = seq.GetDerEncoded();


            var avro = AvroConvert.Serialize(sample);

            var optimal = OptimalSignedEnocding(sample);
            //Console.WriteLine($"{esiur.Length};{flatBuffer.Length};{protoBuffer.Length};{messagePack.Length};{bson.Length};{cbor.Length};{avro.Length};{optimal}");
            return (esiur.Length, esiurAligned.Length, flatBuffer.Length, protoBuffer.Length, messagePack.Length, bson.Length, cbor.Length, avro.Length, optimal);

        }

        public static (int, int, int, int, int, int, int, int, int) CompareInt(int[] sample)
        {
            var intRoot = new ArrayRoot<int>() { Values = sample };

            var esiur = GroupInt32Codec.Encode(sample);
            var esiurAligned = GroupInt32Codec.Encode(sample, true);
            var messagePack = MessagePackSerializer.Serialize(sample);
            var flatBuffer = SerializeFlatBuffers(intRoot);


            using var ms = new MemoryStream();
            Serializer.Serialize(ms, sample);
            var protoBuffer = ms.ToArray();

            var bson = intRoot.ToBson();

            var cbor = CBORObject.FromObject(intRoot).EncodeToBytes();
            //var seq = new DerSequence(sample.Select(v => new DerInteger(v)).ToArray());
            //var ans1 = seq.GetDerEncoded();

            var avro = AvroConvert.Serialize(sample);

            var optimal = OptimalSignedEnocding(sample.Select(x => (long)x).ToArray());
            //Console.WriteLine($"{esiur.Length};{flatBuffer.Length};{protoBuffer.Length};{messagePack.Length};{bson.Length};{cbor.Length};{avro.Length};{optimal}");
            return (esiur.Length, esiurAligned.Length, flatBuffer.Length, protoBuffer.Length, messagePack.Length, bson.Length, cbor.Length, avro.Length, optimal);

        }


        public static (int, int, int, int, int, int, int, int, int) CompareInt(short[] sample)
        {
            var intRoot = new ArrayRoot<short>() { Values = sample };

            var esiur = GroupInt16Codec.Encode(sample);
            var esiurAligned = esiur;// GroupInt16Codec.Encode(sample, true);
            var messagePack = MessagePackSerializer.Serialize(sample);
            var flatBuffer = SerializeFlatBuffers(intRoot);

            using var ms = new MemoryStream();
            Serializer.Serialize(ms, sample);
            var protoBuffer = ms.ToArray();

            var bson = intRoot.ToBson();

            var cbor = CBORObject.FromObject(intRoot).EncodeToBytes();
            //var seq = new DerSequence(sample.Select(v => new DerInteger(v)).ToArray());
            //var ans1 = seq.GetDerEncoded();

            var avro = AvroConvert.Serialize(sample);

            var optimal = OptimalSignedEnocding(sample.Select(x => (long)x).ToArray());
            //Console.WriteLine($"{esiur.Length};{flatBuffer.Length};{protoBuffer.Length};{messagePack.Length};{bson.Length};{cbor.Length};{avro.Length};{optimal}");
            return (esiur.Length, esiurAligned.Length, flatBuffer.Length, protoBuffer.Length, messagePack.Length, bson.Length, cbor.Length, avro.Length, optimal);

        }

        public static (int, int, int, int, int, int, int, int, int) CompareInt(uint[] sample)
        {
            var intRoot = new ArrayRoot<uint>() { Values = sample };

            var esiur = GroupUInt32Codec.Encode(sample);
            var esiurAligned = GroupUInt32Codec.Encode(sample, true);
            var messagePack = MessagePackSerializer.Serialize(sample);
            var flatBuffer = SerializeFlatBuffers(intRoot);

            using var ms = new MemoryStream();
            Serializer.Serialize(ms, sample);
            var protoBuffer = ms.ToArray();

            var intRoot2 = new ArrayRoot<int>() { Values = sample.Select(x => (int)x).ToArray() };

            var bson = intRoot2.ToBson();

            var cbor = CBORObject.FromObject(intRoot).EncodeToBytes();


            var avro = AvroConvert.Serialize(sample.Select(x => (int)x).ToArray());

            //var seq = new DerSequence(sample.Select(v => new DerInteger(v)).ToArray());
            //var avro = seq.GetDerEncoded();

            var optimal = OptimalUnsignedEnocding(sample.Select(x => (ulong)x).ToArray());
            //Console.WriteLine($"{esiur.Length};{flatBuffer.Length};{protoBuffer.Length};{messagePack.Length};{bson.Length};{cbor.Length};{avro.Length};{optimal}");

            return (esiur.Length, esiurAligned.Length, flatBuffer.Length, protoBuffer.Length, messagePack.Length, bson.Length, cbor.Length, avro.Length, optimal);

        }

        public static (int, int, int, int, int, int, int, int, int) CompareInt(ulong[] sample)
        {
            var intRoot = new ArrayRoot<ulong>() { Values = sample };

            var esiur = GroupUInt64Codec.Encode(sample);
            var esiurPadded = GroupUInt64Codec.Encode(sample, true);
            var messagePack = MessagePackSerializer.Serialize(sample);
            var flatBuffer = SerializeFlatBuffers(intRoot);

            using var ms = new MemoryStream();
            Serializer.Serialize(ms, sample);
            var protoBuffer = ms.ToArray();

            var bson = intRoot.ToBson();

            var cbor = CBORObject.FromObject(intRoot).EncodeToBytes();
            //var seq = new DerSequence(sample.Select(v => new DerInteger((long)v)).ToArray());
            //var ans1 = seq.GetDerEncoded();

            var avro = AvroConvert.Serialize(sample);


            var optimal = OptimalUnsignedEnocding(sample);
            //Console.WriteLine($"{esiur.Length};{flatBuffer.Length};{protoBuffer.Length};{messagePack.Length};{bson.Length};{cbor.Length};{avro.Length};{optimal}");

            return (esiur.Length, esiurPadded.Length, flatBuffer.Length, protoBuffer.Length, messagePack.Length, bson.Length, cbor.Length, avro.Length, optimal);
        }

        public static (int, int, int, int, int, int, int, int, int) CompareInt(ushort[] sample)
        {
            var intRoot = new ArrayRoot<ushort>() { Values = sample };

            var esiur = GroupUInt16Codec.Encode(sample);
            var esiurAligned = esiur;// GroupUInt16Codec.Encode(sample, true);
            var messagePack = MessagePackSerializer.Serialize(sample);
            var flatBuffer = SerializeFlatBuffers(intRoot);

            using var ms = new MemoryStream();
            Serializer.Serialize(ms, sample);
            var protoBuffer = ms.ToArray();

            var bson = intRoot.ToBson();

            var cbor = CBORObject.FromObject(intRoot).EncodeToBytes();
            //var seq = new DerSequence(sample.Select(v => new DerInteger(v)).ToArray());
            //var ans1 = seq.GetDerEncoded();

            var avro = AvroConvert.Serialize(sample);

            var optimal = OptimalUnsignedEnocding(sample.Select(x => (ulong)x).ToArray());
            //Console.WriteLine($"{esiur.Length};{flatBuffer.Length};{protoBuffer.Length};{messagePack.Length};{bson.Length};{cbor.Length};{avro.Length};{optimal}");
            return (esiur.Length, esiurAligned.Length, flatBuffer.Length, protoBuffer.Length, messagePack.Length, bson.Length, cbor.Length, avro.Length, optimal);

        }

        public static int OptimalSignedEnocding(long[] data)
        {
            var sum = 0;

            foreach (var i in data)
                if (i >= sbyte.MinValue && i <= sbyte.MaxValue)
                    sum += 1;
                else if (i >= short.MinValue && i <= short.MaxValue)
                    sum += 2;
                else if (i >= -8_388_608 && i <= 8_388_607)
                    sum += 3;
                else if (i >= int.MinValue && i <= int.MaxValue)
                    sum += 4;
                else if (i >= -549_755_813_888 && i <= 549_755_813_887)
                    sum += 5;
                else if (i >= -140_737_488_355_328 && i <= 140_737_488_355_327)
                    sum += 6;
                else if (i >= -36_028_797_018_963_968 && i <= 36_028_797_018_963_967)
                    sum += 7;
                else if (i >= long.MinValue && i <= long.MaxValue)
                    sum += 8;

            return sum;
        }

        public static int OptimalUnsignedEnocding(ulong[] data)
        {
            var sum = 0;

            foreach (var i in data)
                if (i <= byte.MaxValue)
                    sum += 1;
                else if (i <= ushort.MaxValue)
                    sum += 2;
                else if (i <= uint.MaxValue)
                    sum += 4;
                else if (i <= 0xFF_FF_FF_FF_FF)
                    sum += 5;
                else if (i <= 0xFF_FF_FF_FF_FF_FF)
                    sum += 6;
                else if (i <= 0xFF_FF_FF_FF_FF_FF_FF)
                    sum += 7;
                else if (i <= ulong.MaxValue)
                    sum += 8;

            return sum;
        }


        static double[] Average(Func<(int, int, int, int, int, int, int, int, int)> call, int count)
        {
            var sum = new List<(int, int, int, int, int, int, int, int, int)>();

            for (var i = 0; i < count; i++)
                sum.Add(call());


            var rt = new double[]{ 
                    sum.Average(x => x.Item1),
                    sum.Average(x => x.Item2),
                    sum.Average(x => x.Item3),
                    sum.Average(x => x.Item4),
                    sum.Average(x => x.Item5),
                    sum.Average(x => x.Item6),
                    sum.Average(x => x.Item7),
                    sum.Average(x => x.Item8),
                    sum.Average(x => x.Item9)
            };

            Console.WriteLine($"{rt[0]},{rt[1]},{rt[2]},{rt[3]},{rt[4]},{rt[5]},{rt[6]},{rt[7]},{rt[8]}");


            return rt;
        }

        static string PrintAverage(double[] values)
        {
            // Determine winner (lowest average size)
            var names = new string[] { "Esiur", "Aligned", "FlatBuffer", "ProtoBuffer", "MessagePack", "BSON", "CBOR", "Avro", "Optimal" };
            var min = values.SkipLast(1).Min();

            int[] indexes = values.Select((value, index) => new { value, index })
                     .Where(x => x.value == min)
                     .Select(x => x.index)
                     .ToArray();

            foreach(var index in indexes)
            {
                Console.ForegroundColor = index < 2 ? ConsoleColor.Green
                    : ConsoleColor.Red;
                Console.WriteLine($"Winner: {names[index]} ({min:F0})");
            }

            Console.ForegroundColor = ConsoleColor.White;

            return "Unknown";
        }

        public static byte[] SerializeFlatBuffers<T>(ArrayRoot<T> array)
        {
            var buffer = new byte[1000000000];
            var len = FlatBufferSerializer.Default.Serialize(array, buffer);
            return buffer.Take(len).ToArray();
        }

        public static T[] DeserializeFlatBuffers<T>(byte[] buffer)
        {
            var root = FlatBufferSerializer.Default.Parse<ArrayRoot<T>>( buffer);
            return root.Values.ToArray();
        }
    }
}
