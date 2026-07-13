using System;
using System.Collections.Generic;
using Esiur.Data;
using Esiur.Resource;

namespace Esiur.Tests.Unit;

public class IndexedStructureTests
{
    private sealed class ExampleInfo : IndexedStructure
    {
        [Index(1)]
        public int Count { get; set; }

        [Index(2)]
        public string? Note { get; set; }
    }

    private sealed class MemberDefInfo : IndexedStructure
    {
        [Index(1)]
        public string? Name { get; set; }

        [Index(2)]
        public ExampleInfo? Example { get; set; }

        [Index(3)]
        public List<ExampleInfo>? Examples { get; set; }
    }

    [Fact]
    public void NestedStructure_RoundTripsDirectly()
    {
        var source = new MemberDefInfo
        {
            Name = "length",
            Example = new ExampleInfo { Count = 3, Note = "nested" },
            Examples = new List<ExampleInfo>
            {
                new() { Count = 4, Note = "first" },
                new() { Count = 5, Note = "second" },
            },
        };

        var bytes = Codec.ComposeIndexedType(source, Warehouse.Default, null);
        var (_, raw) = Codec.ParseSync(bytes, 0, Warehouse.Default);
        var (size, parsed) = Codec.ParseIndexedType<MemberDefInfo>(bytes, 0, Warehouse.Default);

        Assert.Equal((uint)bytes.Length, size);
        Assert.IsType<Map<byte, object>>(raw);
        Assert.Equal("length", parsed.Name);
        Assert.NotNull(parsed.Example);
        Assert.Equal(3, parsed.Example.Count);
        Assert.Equal("nested", parsed.Example.Note);
        Assert.Equal(2, parsed.Examples!.Count);
        Assert.Equal(5, parsed.Examples[1].Count);
    }

    [Fact]
    public void UnknownIndexes_AreIgnored()
    {
        var map = new Map<byte, object>
        {
            [1] = "known",
            [99] = "future field",
        };

        var parsed = Codec.ParseIndexedType<MemberDefInfo>(map);

        Assert.Equal("known", parsed.Name);
        Assert.Null(parsed.Example);
    }

    [Fact]
    public void DuplicateIndexes_AreRejected()
    {
        var value = new InvalidStructure();
        var error = Assert.Throws<InvalidOperationException>(() =>
            Codec.ComposeIndexedType(value, Warehouse.Default, null));

        Assert.Contains("index 1", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class InvalidStructure : IndexedStructure
    {
        [Index(1)] public int First { get; set; }
        [Index(1)] public int Second { get; set; }
    }
}
