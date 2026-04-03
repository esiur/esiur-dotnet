
using Esiur.Tests.Serialization;
using MessagePack;

MessagePack.MessagePackSerializer.DefaultOptions = MessagePackSerializerOptions.Standard
    .WithCompression(MessagePackCompression.None); // optional; remove if you want raw size


var ints = new IntArrayRunner();
ints.Run();

var models = new ModelRunner();
models.Run();


