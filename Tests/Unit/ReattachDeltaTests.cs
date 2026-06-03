using System;
using Esiur.Data;
using Esiur.Resource;

namespace Esiur.Tests.Unit;

/// <summary>
/// Verifies the reattach property-delta wire format round-trips: the sparse
/// (index -> value/age/date) map composed by <c>PropertyValueMapComposer</c> is parsed back
/// identically by <c>PropertyValueMapParserAsync</c>. This is the format the age-based reattach
/// reply uses to send only the properties modified after the client's last-known age.
/// </summary>
public class ReattachDeltaTests
{
    static Map<byte, PropertyValue> RoundTrip(Map<byte, PropertyValue> delta)
    {
        // Compose -> RawData TDU; parse the TDU back to its raw payload; run the delta parser.
        var tdu = Codec.Compose(delta, Warehouse.Default, null);
        var (_, payloadObj) = Codec.ParseSync(tdu, 0, Warehouse.Default);
        var payload = (byte[])payloadObj;

        return DataDeserializer
            .PropertyValueMapParserAsync(payload, 0, (uint)payload.Length, null, new uint[] { 1 })
            .Wait();
    }

    [Fact]
    public void Delta_RoundTrips_PreservingIndexValueAndAge()
    {
        var date0 = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var date3 = new DateTime(2026, 2, 2, 8, 30, 0, DateTimeKind.Utc);

        var delta = new Map<byte, PropertyValue>
        {
            [0] = new PropertyValue(42, 5UL, date0),
            [3] = new PropertyValue("hello", 9UL, date3),
        };

        var parsed = RoundTrip(delta);

        Assert.Equal(2, parsed.Count);

        Assert.Equal(42L, Convert.ToInt64(parsed[0].Value));
        Assert.Equal(5UL, parsed[0].Age);
        Assert.Equal(date0.Ticks, ((DateTime)parsed[0].Date).ToUniversalTime().Ticks);

        Assert.Equal("hello", (string)parsed[3].Value);
        Assert.Equal(9UL, parsed[3].Age);
        Assert.Equal(date3.Ticks, ((DateTime)parsed[3].Date).ToUniversalTime().Ticks);
    }

    [Fact]
    public void EmptyDelta_RoundTrips_ToEmptyMap()
    {
        var parsed = RoundTrip(new Map<byte, PropertyValue>());
        Assert.Empty(parsed);
    }

    [Fact]
    public void Delta_PreservesOnlyProvidedIndices()
    {
        // A sparse delta (only index 7 changed) must not introduce entries for other indices.
        var delta = new Map<byte, PropertyValue>
        {
            [7] = new PropertyValue(true, 100UL, new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc)),
        };

        var parsed = RoundTrip(delta);

        Assert.Single(parsed);
        Assert.True(parsed.ContainsKey(7));
        Assert.True(Convert.ToBoolean(parsed[7].Value));
        Assert.Equal(100UL, parsed[7].Age);
    }
}
