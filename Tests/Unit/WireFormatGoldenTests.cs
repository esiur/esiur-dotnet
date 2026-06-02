using System;
using Esiur.Data;
using Esiur.Resource;

namespace Esiur.Tests.Unit;

/// <summary>
/// Pins the exact on-wire bytes produced by Codec.Compose for a representative value of
/// every TDU family. Esiur is a multi-language protocol (C#/JS/Dart) whose binary format
/// must stay byte-compatible, so these golden vectors are a guard rail: any later
/// refactor (e.g. serializer performance work) that changes a single byte fails here.
/// Values were captured from the current implementation.
/// </summary>
public class WireFormatGoldenTests
{
    static string Hex(object value) =>
        BitConverter.ToString(Codec.Compose(value, Warehouse.Default, null)).Replace("-", "").ToLowerInvariant();

    [Theory]
    // fixed, zero-payload tokens
    [InlineData("null", null, "00")]
    [InlineData("bool_false", false, "01")]
    [InlineData("bool_true", true, "02")]
    // signed integers, narrowed to the smallest width
    [InlineData("int_0", 0, "0900")]
    [InlineData("int_1", 1, "0901")]
    [InlineData("int_minus1", -1, "09ff")]
    [InlineData("int_127", 127, "097f")]
    [InlineData("int_128", 128, "118000")]
    [InlineData("int_200", 200, "11c800")]
    [InlineData("int_40000", 40000, "19409c0000")]
    [InlineData("int_70000", 70000, "1970110100")]
    [InlineData("long_5e9", 5000000000L, "2100f2052a01000000")]
    // unsigned integers
    [InlineData("uint_255", (uint)255, "08ff")]
    [InlineData("uint_256", (uint)256, "100001")]
    [InlineData("ulong_max", ulong.MaxValue, "20ffffffffffffffff")]
    // floating point
    [InlineData("float_1p5", 1.5f, "1a0000c03f")]
    [InlineData("double_0p1", 0.1d, "229a9999999999b93f")]
    // char
    [InlineData("char_A", 'A', "124100")]
    // strings (length-prefixed UTF-8)
    [InlineData("string_Hi", "Hi", "49024869")]
    [InlineData("string_empty", "", "41")]
    public void Compose_Matches_Golden(string name, object value, string expectedHex)
    {
        _ = name; // identifies the case in test output
        Assert.Equal(expectedHex, Hex(value));
    }

    [Fact]
    public void Decimal_HighPrecision_Golden()
    {
        Assert.Equal("2a00001c00321be4271581396eb1c9be46", Hex(1.2345678901234567890123456789m));
    }

    [Fact]
    public void DateTime_Golden()
    {
        Assert.Equal("230039f035adc0de08", Hex(new DateTime(2026, 6, 2, 13, 45, 30, DateTimeKind.Utc)));
    }

    [Fact]
    public void Uuid_Golden()
    {
        var uuid = new Uuid(Guid.Parse("12345678-90ab-cdef-1234-567890abcdef").ToByteArray());
        Assert.Equal("2b78563412ab90efcd1234567890abcdef", Hex(uuid));
    }

    [Fact]
    public void IntArray_TypedList_Golden()
    {
        // Typed list with Gvwie group-encoded payload for { 1, 2, 3 }.
        Assert.Equal("88054809020406", Hex(new int[] { 1, 2, 3 }));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NaN_And_Infinity_Encode_To_Single_Token(double value)
    {
        Assert.Equal("04", Hex(value));
    }
}
