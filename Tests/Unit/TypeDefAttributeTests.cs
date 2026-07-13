using Esiur.Core;
using Esiur.Data;
using Esiur.Data.Types;
using Esiur.Resource;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Esiur.Tests.Unit;

public class TypeDefAttributeTests
{
    [Fact]
    public void FunctionStreamMode_IsInferredOrReadFromAttribute()
    {
        var pull = MakeFunction(nameof(StreamFixture.Pull));
        var push = MakeFunction(nameof(StreamFixture.Push));
        var explicitStream = MakeFunction(nameof(StreamFixture.Explicit));

        Assert.Equal(StreamMode.Pull, pull.StreamMode);
        Assert.Equal(TruIdentifier.Int32, pull.ReturnType.Identifier);
        Assert.Equal(StreamMode.Push, push.StreamMode);
        Assert.Equal(TruIdentifier.Int32, push.ReturnType.Identifier);
        Assert.Equal(StreamMode.Push, explicitStream.StreamMode);
        Assert.True(explicitStream.Pausable);
    }

    [Theory]
    [InlineData(nameof(StreamFixture.InvalidReturn))]
    [InlineData(nameof(StreamFixture.InvalidPausablePull))]
    [InlineData(nameof(StreamFixture.ConflictingMode))]
    public void FunctionStreamMode_RejectsInvalidDeclarations(string methodName)
    {
        var exception = Assert.Throws<Exception>(() => MakeFunction(methodName));
        Assert.Contains("stream", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Attributes_AreIncludedInSerializedTypeDef()
    {
        var warehouse = new Warehouse();
        var definition = new LocalTypeDef(typeof(AttributedResource), warehouse);
        var bytes = Codec.Compose(TypeDefInfo.FromTypeDef(definition), warehouse, null);
        var (_, info) = Codec.ParseIndexedType<TypeDefInfo>(bytes, 0, warehouse);

        Assert.Equal("type usage", info.Usage);
        Assert.Equal("type description", info.Description);
        Assert.Equal("type example", info.Example);
        Assert.Equal("tests", info.Category);
        Assert.Equal("3.1", info.Since);
        Assert.Equal("type", info.Annotations!["scope"]);

        var property = Assert.Single(info.Properties!, x => x.Name == "Value");
        var propertyFlags = (PropertyDefFlags)property.Flags;
        Assert.True(propertyFlags.HasFlag(PropertyDefFlags.ReadOnly));
        Assert.True(propertyFlags.HasFlag(PropertyDefFlags.Volatile));
        Assert.True(propertyFlags.HasFlag(PropertyDefFlags.Historical));
        Assert.True(propertyFlags.HasFlag(PropertyDefFlags.Deprecated));
        Assert.Equal(OrderingControl.LatestOnly, property.OrderingControl);
        Assert.Equal(5, Convert.ToInt32(property.DefaultValue));
        Assert.Equal("property description", property.Description);
        Assert.Equal("property usage", property.Usage);
        Assert.Equal(2, property.Examples!.Count);
        Assert.Equal(new[] { "state", "sample" }, property.Tags);
        Assert.Equal("units", property.Unit);
        Assert.Equal(0, Convert.ToInt32(property.Minimum));
        Assert.Equal(10, Convert.ToInt32(property.Maximum));
        Assert.Equal(new[] { 1, 5 }, property.AllowedValues!.Select(Convert.ToInt32));
        Assert.Equal("^[0-9]+$", property.Pattern);
        Assert.Equal("integer", property.Format);
        Assert.Equal("property warning", Assert.Single(property.Warnings!));
        Assert.Equal((byte)7, Assert.Single(property.RelatedMembers!));
        Assert.Equal("use NewValue", property.DeprecationMessage);
        Assert.Equal("property", property.Annotations!["scope"]);

        var function = Assert.Single(info.Functions!, x => x.Name == nameof(AttributedResource.Watch));
        var functionFlags = (FunctionDefFlags)function.Flags;
        Assert.True(functionFlags.HasFlag(FunctionDefFlags.ReadOnly));
        Assert.True(functionFlags.HasFlag(FunctionDefFlags.Idempotent));
        Assert.True(functionFlags.HasFlag(FunctionDefFlags.Cancellable));
        Assert.True(functionFlags.HasFlag(FunctionDefFlags.Pausable));
        Assert.Equal(StreamMode.Push, function.StreamMode);
        Assert.Equal("ready", Assert.Single(function.Preconditions!));
        Assert.Equal("complete", Assert.Single(function.Postconditions!));
        Assert.Equal(OperationEffects.EmitsEvents, function.Effects);

        var eventInfo = Assert.Single(info.Events!, x => x.Name == nameof(AttributedResource.Changed));
        var eventFlags = (EventDefFlags)eventInfo.Flags;
        Assert.True(eventFlags.HasFlag(EventDefFlags.AutoDelivered));
        Assert.True(eventFlags.HasFlag(EventDefFlags.Historical));
        Assert.Equal(OrderingControl.Relaxed, eventInfo.OrderingControl);

        var constant = Assert.Single(info.Constants!, x => x.Name == nameof(AttributedResource.Limit));
        Assert.Equal("constant description", constant.Description);
        Assert.Equal("constant usage", constant.Usage);
        Assert.Equal(10, Convert.ToInt32(constant.Value));
    }

    private static FunctionDef MakeFunction(string name)
    {
        var method = typeof(StreamFixture).GetMethod(name, BindingFlags.Public | BindingFlags.Instance)!;
        return FunctionDef.MakeFunctionDef(
            Warehouse.Default, typeof(StreamFixture), method, 0, name, new TypeDef());
    }

    private sealed class StreamFixture
    {
        public IAsyncEnumerable<int> Pull() => throw new NotSupportedException();

        public IEnumerable<int> Push() => Array.Empty<int>();

        [Stream(StreamMode.Push, Pausable = true)]
        public AsyncReply<int> Explicit() => new(1);

        [Stream]
        public int InvalidReturn() => 1;

        [Stream(StreamMode.Pull, Pausable = true)]
        public AsyncReply<int> InvalidPausablePull() => new(1);

        [Stream(StreamMode.Push)]
        public IAsyncEnumerable<int> ConflictingMode() => throw new NotSupportedException();
    }

    [Export]
    [Usage("type usage")]
    [Description("type description")]
    [Example("type example")]
    [Category("tests")]
    [Since("3.1")]
    [Annotation("scope", "type")]
    private sealed class AttributedResource : Esiur.Resource.Resource
    {
        [Description("constant description")]
        [Usage("constant usage")]
        [Example(10)]
        [Annotation("scope", "constant")]
        public const int Limit = 10;

        [ReadOnly]
        [Volatile]
        [Historical]
        [Ordering(OrderingControl.LatestOnly)]
        [System.ComponentModel.DefaultValue(5)]
        [Description("property description")]
        [Usage("property usage")]
        [Example(1)]
        [Example(5)]
        [Tags("state", "sample")]
        [Unit("units")]
        [Minimum(0)]
        [Maximum(10)]
        [AllowedValue(1)]
        [AllowedValue(5)]
        [Pattern("^[0-9]+$")]
        [Format("integer")]
        [Warning("property warning")]
        [RelatedMembers(7)]
        [Obsolete("use NewValue")]
        [Annotation("scope", "property")]
        public int Value { get; set; }

        [Stream(StreamMode.Push, Pausable = true)]
        [ReadOnly]
        [Idempotent]
        [Cancellable]
        [Precondition("ready")]
        [Postcondition("complete")]
        [Effects(OperationEffects.EmitsEvents)]
        public AsyncReply<int> Watch() => new(1);

        [AutoDelivery]
        [Historical]
        [Ordering(OrderingControl.Relaxed)]
        public event ResourceEventHandler<int>? Changed;
    }
}
