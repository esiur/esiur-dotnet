using System.Collections.Generic;
using Esiur.Data;
using Esiur.Data.Types;
using Esiur.Resource;

namespace Esiur.Tests.Unit;

public class TypeDefInfoSerializationTests
{
    [Fact]
    public void Tru_HasDedicatedTduAndRoundTrips()
    {
        Tru source = new TruPrimitive(TruIdentifier.String, true, typeof(string));

        var bytes = Codec.Compose(source, Warehouse.Default, null);
        var (size, value) = Codec.ParseSync(bytes, 0, Warehouse.Default);

        Assert.Equal((byte)TduIdentifier.TRU, (byte)(bytes[0] & 0xC7));
        Assert.Equal((uint)bytes.Length, size);
        var parsed = Assert.IsAssignableFrom<Tru>(value);
        Assert.Equal(TruIdentifier.String, parsed.Identifier);
        Assert.True(parsed.Nullable);
    }

    [Fact]
    public void ConsecutiveTrus_UseContinuationAndRoundTrip()
    {
        var source = new object[]
        {
            new TruPrimitive(TruIdentifier.String, false, typeof(string)),
            new TruPrimitive(TruIdentifier.Int32, false, typeof(int)),
        };

        var bytes = Codec.Compose(source, Warehouse.Default, null);
        var (_, value) = Codec.ParseSync(bytes, 0, Warehouse.Default);
        var parsed = Assert.IsType<object[]>(value);

        Assert.Equal(TruIdentifier.String, Assert.IsAssignableFrom<Tru>(parsed[0]).Identifier);
        Assert.Equal(TruIdentifier.Int32, Assert.IsAssignableFrom<Tru>(parsed[1]).Identifier);
    }

    [Fact]
    public void TypeDefInfo_WithNestedMembersAndTrus_RoundTrips()
    {
        var source = new TypeDefInfo
        {
            Version = 3,
            Id = 42,
            Name = "Widget",
            Namespace = "Example.Models",
            Kind = TypeDefKind.Resource,
            Parent = 7,
            Description = "A test resource",
            Annotations = new Map<string, string> { ["audience"] = "test" },
            Properties = new List<PropertyDefInfo>
            {
                new()
                {
                    Index = 1,
                    Name = "Title",
                    Flags = (byte)PropertyDefFlags.ReadOnly,
                    ValueType = new TruPrimitive(TruIdentifier.String, false, typeof(string)),
                    OrderingControl = OrderingControl.LatestOnly,
                },
            },
            Functions = new List<FunctionDefInfo>
            {
                new()
                {
                    Index = 2,
                    Name = "Find",
                    Flags = (byte)FunctionDefFlags.Idempotent,
                    ReturnType = new TruPrimitive(TruIdentifier.String, true, typeof(string)),
                    Arguments = new List<ArgumentDefInfo>
                    {
                        new()
                        {
                            Index = 0,
                            Name = "id",
                            ValueType = new TruPrimitive(TruIdentifier.UInt64, false, typeof(ulong)),
                        },
                    },
                },
            },
            Events = new List<EventDefInfo>
            {
                new()
                {
                    Index = 3,
                    Name = "Changed",
                    ArgumentName = "title",
                    ArgumentType = new TruPrimitive(TruIdentifier.String, false, typeof(string)),
                },
            },
            Constants = new List<ConstantDefInfo>
            {
                new()
                {
                    Index = 4,
                    Name = "Maximum",
                    ValueType = new TruPrimitive(TruIdentifier.Int32, false, typeof(int)),
                    Value = 100,
                },
            },
        };

        var bytes = Codec.Compose(source, Warehouse.Default, null);
        var (size, parsed) = Codec.ParseIndexedType<TypeDefInfo>(bytes, 0, Warehouse.Default);

        Assert.Equal((byte)TduIdentifier.TypeDef, (byte)(bytes[0] & 0xC7));
        Assert.Equal((uint)bytes.Length, size);
        Assert.Equal(source.Id, parsed.Id);
        Assert.Equal("Widget", parsed.Name);
        Assert.Equal(TypeDefKind.Resource, parsed.Kind);
        Assert.Equal("test", parsed.Annotations!["audience"]);

        var property = Assert.Single(parsed.Properties!);
        Assert.Equal("Title", property.Name);
        Assert.Equal(TruIdentifier.String, property.ValueType.Identifier);
        Assert.Equal(OrderingControl.LatestOnly, property.OrderingControl);

        var function = Assert.Single(parsed.Functions!);
        Assert.True(function.ReturnType.Nullable);
        Assert.Equal(TruIdentifier.String, function.ReturnType.Identifier);
        Assert.Equal(TruIdentifier.UInt64, Assert.Single(function.Arguments!).ValueType.Identifier);

        Assert.Equal(TruIdentifier.String, Assert.Single(parsed.Events!).ArgumentType.Identifier);
        Assert.Equal(100, Convert.ToInt32(Assert.Single(parsed.Constants!).Value));
    }
}
