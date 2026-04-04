
using Esiur.Tests.ComplexModel;

using MessagePack;

MessagePack.MessagePackSerializer.DefaultOptions = MessagePackSerializerOptions.Standard
    .WithCompression(MessagePackCompression.None); // optional; remove if you want raw size



var models = new ModelRunner();
models.Run();


