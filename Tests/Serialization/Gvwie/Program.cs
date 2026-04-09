
using Esiur.Data.Gvwie;
using Esiur.Tests.Gvwie;
using MessagePack;

var e = GroupInt32Codec.Encode(new int[] {-12000, 15000, -1, 32760 });

MessagePack.MessagePackSerializer.DefaultOptions = MessagePackSerializerOptions.Standard
    .WithCompression(MessagePackCompression.None); // optional; remove if you want raw size


var ints = new IntArrayRunner();
IntArrayGenerator.InitRng();
ints.Run();
IntArrayGenerator.InitRng();
ints.RunChart();

