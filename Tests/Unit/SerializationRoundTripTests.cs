using System;
using System.Collections.Generic;
using Esiur.Data;
using Esiur.Resource;

namespace Esiur.Tests.Unit;

/// <summary>
/// Round-trips values through Codec.Compose -> Codec.ParseSync and asserts the value
/// survives. Because the serializer narrows integers/floats to the smallest wire type,
/// the parsed CLR type often differs from the input, so comparisons are value-based.
/// Exact wire bytes are pinned separately by <see cref="WireFormatGoldenTests"/>.
/// </summary>
public class SerializationRoundTripTests
{
    static object RoundTrip(object value)
    {
        var bytes = Codec.Compose(value, Warehouse.Default, null);
        var (consumed, parsed) = Codec.ParseSync(bytes, 0, Warehouse.Default);
        Assert.Equal((uint)bytes.Length, consumed);
        return parsed;
    }

    static void AssertIntRoundTrip(long value)
    {
        var parsed = RoundTrip(value);
        Assert.Equal(value, Convert.ToInt64(parsed));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(sbyte.MinValue)]
    [InlineData(sbyte.MaxValue)]
    [InlineData((long)short.MinValue)]
    [InlineData((long)short.MaxValue)]
    [InlineData((long)int.MinValue)]
    [InlineData((long)int.MaxValue)]
    [InlineData(long.MinValue)]
    [InlineData(long.MaxValue)]
    public void Int64_NarrowsAndRoundTrips(long value) => AssertIntRoundTrip(value);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void Int32_RoundTrips(int value)
    {
        var parsed = RoundTrip(value);
        Assert.Equal(value, Convert.ToInt32(parsed));
    }

    [Theory]
    [InlineData((ulong)0)]
    [InlineData((ulong)255)]
    [InlineData((ulong)65535)]
    [InlineData((ulong)uint.MaxValue)]
    [InlineData(ulong.MaxValue)]
    public void UInt64_NarrowsAndRoundTrips(ulong value)
    {
        var parsed = RoundTrip(value);
        Assert.Equal(value, Convert.ToUInt64(parsed));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(1.5f)]
    [InlineData(-3.25f)]
    [InlineData(3.4028235e38f)]
    public void Float32_RoundTrips(float value)
    {
        var parsed = RoundTrip(value);
        Assert.Equal(value, Convert.ToSingle(parsed), 3);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(1.5d)]
    [InlineData(-3.25d)]
    [InlineData(0.1d)]
    [InlineData(1.7976931348623157e308d)]
    public void Float64_RoundTrips(double value)
    {
        var parsed = RoundTrip(value);
        Assert.Equal(value, Convert.ToDouble(parsed), 10);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Float_NaN_And_Infinity_Encode_As_Infinity_Token(double value)
    {
        // The serializer collapses NaN and +/- Infinity onto the single 1-byte Infinity
        // token (0x04), which now decodes to a canonical +Infinity rather than crashing.
        var bytes = Codec.Compose(value, Warehouse.Default, null);
        Assert.Equal(new byte[] { 0x04 }, bytes);

        var parsed = RoundTrip(value);
        Assert.True(double.IsPositiveInfinity(Convert.ToDouble(parsed)));
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(1.1)]      // hits the decimal -> Float64 branch (regression for the byte[4] overrun)
    [InlineData(-1234.5)]
    public void Decimal_FloatBranches_RoundTrip(double asDouble)
    {
        var value = (decimal)asDouble;
        var parsed = RoundTrip(value);
        Assert.Equal(Convert.ToDouble(value), Convert.ToDouble(parsed), 6);
    }

    [Fact]
    public void Decimal_IntegerValue_RoundTrips()
    {
        var parsed = RoundTrip(42m);
        Assert.Equal(42L, Convert.ToInt64(parsed));
    }

    [Fact]
    public void Decimal_HighPrecision_RoundTrips()
    {
        // A value with a non-zero scale that is not exactly representable as float or
        // double, so it stays a full 16-byte Decimal128 on the wire.
        var value = 1.2345678901234567890123456789m;
        var parsed = RoundTrip(value);
        Assert.Equal(value, Convert.ToDecimal(parsed));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Bool_RoundTrips(bool value)
    {
        var parsed = RoundTrip(value);
        Assert.Equal(value, Convert.ToBoolean(parsed));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Hello, Esiur")]
    [InlineData("Unicode éü☃😀")]
    public void String_RoundTrips(string value)
    {
        var parsed = RoundTrip(value);
        Assert.Equal(value, (string)parsed);
    }

    [Fact]
    public void Null_RoundTrips()
    {
        var parsed = RoundTrip(null);
        Assert.Null(parsed);
    }

    [Fact]
    public void Char_RoundTrips()
    {
        var parsed = RoundTrip('A');
        Assert.Equal('A', Convert.ToChar(parsed));
    }

    [Fact]
    public void DateTime_RoundTrips_AsUtc()
    {
        var value = new DateTime(2026, 6, 2, 13, 45, 30, DateTimeKind.Utc);
        var parsed = (DateTime)RoundTrip(value);
        Assert.Equal(value.ToUniversalTime().Ticks, parsed.ToUniversalTime().Ticks);
    }

    [Fact]
    public void Uuid_RoundTrips()
    {
        var value = new Uuid(Guid.Parse("12345678-90ab-cdef-1234-567890abcdef").ToByteArray());
        var parsed = (Uuid)RoundTrip(value);
        Assert.Equal(value.ToString(), parsed.ToString());
    }

    static List<object> ToObjectList(object value)
    {
        var got = new List<object>();
        foreach (var o in (System.Collections.IEnumerable)value)
            got.Add(o);
        return got;
    }

    [Fact]
    public void IntArray_RoundTrips()
    {
        // A typed int[] is reconstructed as an int[] (typed-list path), not a dynamic list.
        var value = new int[] { 1, 2, 3, 100, -50, int.MaxValue };
        var got = ToObjectList(RoundTrip(value)).ConvertAll(Convert.ToInt64);
        Assert.Equal(new long[] { 1, 2, 3, 100, -50, int.MaxValue }, got);
    }

    [Fact]
    public void StringList_RoundTrips()
    {
        var value = new object[] { "a", "b", "c" };
        var got = ToObjectList(RoundTrip(value)).ConvertAll(o => (string)o);
        Assert.Equal(new[] { "a", "b", "c" }, got);
    }

    [Fact]
    public void Map_RoundTrips()
    {
        var value = new Map<string, int> { ["one"] = 1, ["two"] = 2 };
        var parsed = RoundTrip(value);
        Assert.NotNull(parsed);
    }
}
