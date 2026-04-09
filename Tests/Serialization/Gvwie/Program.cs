
using Esiur.Data.Gvwie;
using Esiur.Tests.Gvwie;
using MessagePack;

var e = GroupInt32Codec.Encode(new int[] {-12000, 15000, -1, 32760 });

var test = IntArrayGenerator.GenerateInt32(5000, GeneratorPattern.Uniform);

var aligned = GroupInt32Codec.Encode(test, true);
var nonAligned  = GroupInt32Codec.Encode(test, false);   

var result1 = GroupInt32Codec.Decode(aligned);
var result2 = GroupInt32Codec.Decode(nonAligned);

if (result1.SequenceEqual(result2))
    Console.WriteLine($"Passed {aligned.Length}");

if (result1.SequenceEqual(test))
    Console.WriteLine($"Passed {nonAligned.Length}");

MessagePack.MessagePackSerializer.DefaultOptions = MessagePackSerializerOptions.Standard
    .WithCompression(MessagePackCompression.None); // optional; remove if you want raw size


var ints = new IntArrayRunner();
//IntArrayGenerator.InitRng();
//ints.Run();
IntArrayGenerator.InitRng();
ints.RunChart();

