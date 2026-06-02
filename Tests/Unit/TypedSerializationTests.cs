using System;
using Esiur.Data;
using Esiur.Resource;

namespace Esiur.Tests.Unit;

[Export]
public class PersonRecord : IRecord
{
    public string Name { get; set; }
    public int Age { get; set; }
    public double Score { get; set; }
}

public enum Color
{
    Red,
    Green,
    Blue,
}

/// <summary>
/// Covers the "typed" serialization paths (records, tuples, enums, typed maps/lists) that
/// rely on Tru.FromType. Uses a decode-then-re-encode stability check: re-composing a parsed
/// value must reproduce the exact original wire bytes. This is type-agnostic (no need to know
/// the parsed CLR type) and proves both the encode and decode halves agree on the format.
/// </summary>
public class TypedSerializationTests
{
    static void AssertReencodeStable(object value)
    {
        var bytes1 = Codec.Compose(value, Warehouse.Default, null);
        var (consumed, parsed) = Codec.ParseSync(bytes1, 0, Warehouse.Default);
        Assert.Equal((uint)bytes1.Length, consumed);

        var bytes2 = Codec.Compose(parsed, Warehouse.Default, null);
        Assert.Equal(bytes1, bytes2);
    }

    [Fact]
    public void Record_ReencodeIsStable()
    {
        AssertReencodeStable(new PersonRecord { Name = "Ada", Age = 36, Score = 99.5 });
    }

    [Fact]
    public void Enum_Encodes_Typed_And_Decodes_To_Underlying_Int()
    {
        // An enum is sent as a Typed TDU carrying the constant index; the decoder (without
        // CLR enum reconstruction) yields the underlying integer value. This is the existing
        // protocol behaviour, asserted here so it stays stable.
        var bytes = Codec.Compose(Color.Green, Warehouse.Default, null);
        Assert.Equal(0x88, bytes[0]); // Typed class token

        var (_, parsed) = Codec.ParseSync(bytes, 0, Warehouse.Default);
        Assert.Equal((int)Color.Green, Convert.ToInt32(parsed));
    }

    [Theory]
    [InlineData(1, "a")]
    [InlineData(-5, "hello")]
    public void Tuple2_ReencodeIsStable(int a, string b)
    {
        AssertReencodeStable((a, b));
    }

    [Fact]
    public void Tuple3_ReencodeIsStable()
    {
        AssertReencodeStable((1, "two", 3.0));
    }

    [Fact]
    public void TypedMap_ReencodeIsStable()
    {
        AssertReencodeStable(new Map<string, int> { ["one"] = 1, ["two"] = 2, ["three"] = 3 });
    }

    [Fact]
    public void TypedIntList_ReencodeIsStable()
    {
        AssertReencodeStable(new int[] { 5, 4, 3, 2, 1, 0, -1 });
    }

    [Fact]
    public void RecordList_ReencodeIsStable()
    {
        AssertReencodeStable(new[]
        {
            new PersonRecord { Name = "A", Age = 1, Score = 1.1 },
            new PersonRecord { Name = "B", Age = 2, Score = 2.2 },
        });
    }
}
