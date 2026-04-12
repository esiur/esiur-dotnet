
using Esiur.Data.Gvwie;
using Esiur.Tests.Gvwie;
using MessagePack;

//var e = GroupInt32Codec.Encode(new int[] {-12000, 15000, -1, 32760 });

var s = new int[] { 1, -1, 2, 300, 301, 200,1, 302 };

var e = GroupInt32Codec.Encode(s);

var d = GroupInt32Codec.Decode(e);

if (d.SequenceEqual(s))
    Console.WriteLine("Example passed.");

var test = IntArrayGenerator.GenerateInt32(5000, GeneratorPattern.Uniform);

MessagePack.MessagePackSerializer.DefaultOptions = MessagePackSerializerOptions.Standard
    .WithCompression(MessagePackCompression.None); // optional; remove if you want raw size


var ints = new IntArrayRunner();
IntArrayGenerator.InitRng();
ints.Run();
IntArrayGenerator.InitRng();
ints.RunChart();

