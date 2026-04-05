using Esiur.Data.Gvwie;
using FlatSharp;
using FlatSharp.Attributes;
using MessagePack;
using MongoDB.Bson;
using PeterO.Cbor;
using ProtoBuf;
using SolTechnology.Avro;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

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
            Console.WriteLine(";Esiur;FlatBuffer;ProtoBuffer;MessagePack;BSON;CBOR;Avro,Optimal");



            //Console.Write("Cluster (Int32);");
            ////CompareInt(int32cluster);
            //Average(() => CompareInt(IntArrayGenerator.GenerateInt32Run(1000)), 1000);


            Console.Write("Positive (Int32);");
            Average(() => CompareInt(IntArrayGenerator.GenerateInt32(1000, "positive")), 1000);

            Console.Write("Negative (Int32);");
            Average(() => CompareInt(IntArrayGenerator.GenerateInt32(1000, "negative")), 1000);


            Console.Write("Small (Int32);");
            Average(() => CompareInt(IntArrayGenerator.GenerateInt32(1000, "small")), 1000);

            // CompareInt(int32small);

            Console.Write("Alternating (Int32);");
            //CompareInt(int32alter);

            Average(() => CompareInt(IntArrayGenerator.GenerateInt32(1000, "alternating")), 1000);


            Console.Write("Ascending (Int32);");
            //CompareInt(int32asc);
            Average(() => CompareInt(IntArrayGenerator.GenerateInt32(1000, "ascending")), 1000);


            Console.Write("Int64;");
            Average(() => CompareInt(IntArrayGenerator.GenerateInt64(1000, "uniform")), 1000);
            //CompareInt(int64Uni);

            Console.Write("Int32;");
            //CompareInt(int32Uni);
            Average(() => CompareInt(IntArrayGenerator.GenerateInt32(1000, "uniform")), 1000);

            Console.Write("Int16;");
            //CompareInt(int16Uni);

            Average(() => CompareInt(IntArrayGenerator.GenerateInt16(1000, "uniform")), 1000);


            Console.Write("UInt64;");
            //CompareInt(uint64Uni);
            Average(() => CompareInt(IntArrayGenerator.GenerateUInt64(1000, "uniform")), 1000);


            Console.Write("UInt32;");
            //CompareInt(uint32Uni);
            Average(() => CompareInt(IntArrayGenerator.GenerateUInt32(1000, "uniform")), 1000);

            Console.Write("UInt16;");
            //CompareInt(uint16Uni);
            Average(() => CompareInt(IntArrayGenerator.GenerateUInt16(1000, "uniform")), 1000);

        }

        public static (int, int, int, int, int, int, int, int) CompareInt(long[] sample)
        {
            var intRoot = new ArrayRoot<long>() { Values = sample };

            var esiur = GroupInt64Codec.Encode(sample);
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
            return (esiur.Length, flatBuffer.Length, protoBuffer.Length, messagePack.Length, bson.Length, cbor.Length, avro.Length, optimal);

        }

        public static (int, int, int, int, int, int, int, int) CompareInt(int[] sample)
        {
            var intRoot = new ArrayRoot<int>() { Values = sample };

            var esiur = GroupInt32Codec.Encode(sample);
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
            return (esiur.Length, flatBuffer.Length, protoBuffer.Length, messagePack.Length, bson.Length, cbor.Length, avro.Length, optimal);

        }


        public static (int, int, int, int, int, int, int, int) CompareInt(short[] sample)
        {
            var intRoot = new ArrayRoot<short>() { Values = sample };

            var esiur = GroupInt16Codec.Encode(sample);
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
            return (esiur.Length, flatBuffer.Length, protoBuffer.Length, messagePack.Length, bson.Length, cbor.Length, avro.Length, optimal);

        }

        public static (int, int, int, int, int, int, int, int) CompareInt(uint[] sample)
        {
            var intRoot = new ArrayRoot<uint>() { Values = sample };

            var esiur = GroupUInt32Codec.Encode(sample);
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

            return (esiur.Length, flatBuffer.Length, protoBuffer.Length, messagePack.Length, bson.Length, cbor.Length, avro.Length, optimal);

        }

        public static (int, int, int, int, int, int, int, int) CompareInt(ulong[] sample)
        {
            var intRoot = new ArrayRoot<ulong>() { Values = sample };

            var esiur = GroupUInt64Codec.Encode(sample);
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

            return (esiur.Length, flatBuffer.Length, protoBuffer.Length, messagePack.Length, bson.Length, cbor.Length, avro.Length, optimal);
        }

        public static (int, int, int, int, int, int, int, int) CompareInt(ushort[] sample)
        {
            var intRoot = new ArrayRoot<ushort>() { Values = sample };

            var esiur = GroupUInt16Codec.Encode(sample);
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
            return (esiur.Length, flatBuffer.Length, protoBuffer.Length, messagePack.Length, bson.Length, cbor.Length, avro.Length, optimal);

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


        static (double, double, double, double, double, double, double, double) Average(Func<(int, int, int, int, int, int, int, int)> call, int count)
        {
            var sum = new List<(int, int, int, int, int, int, int, int)>();

            for (var i = 0; i < count; i++)
                sum.Add(call());


            var rt = (sum.Average(x => x.Item1),
                    sum.Average(x => x.Item2),
                    sum.Average(x => x.Item3),
                    sum.Average(x => x.Item4),
                    sum.Average(x => x.Item5),
                    sum.Average(x => x.Item6),
                    sum.Average(x => x.Item7),
                    sum.Average(x => x.Item8)
                   );

            Console.WriteLine($"{rt.Item1};{rt.Item2};{rt.Item3};{rt.Item4};{rt.Item5};{rt.Item6};{rt.Item7};{rt.Item8}");

            return rt;
        }


        public static byte[] SerializeFlatBuffers<T>(ArrayRoot<T> array)
        {
            var buffer = new byte[1000000000];
            var len = FlatBufferSerializer.Default.Serialize(array, buffer);
            return buffer.Take(len).ToArray();
        }

    }
}
