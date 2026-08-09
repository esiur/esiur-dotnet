using Esiur.Data;
using Esiur.Resource;

namespace Esiur.Tests.Unit;

public sealed class RuntimeCasterRecordTests
{
    [Fact]
    public void DynamicRecordMaterializesAsDeclaredRecordType()
    {
        var warehouse = new Warehouse();
        var nestedDefinition = warehouse.GetLocalTypeDefByType(typeof(NestedRecord));
        var targetDefinition = warehouse.GetLocalTypeDefByType(typeof(TargetRecord));
        var nested = new Esiur.Data.Record(nestedDefinition)
        {
            [nameof(NestedRecord.Name)] = "edge-1",
        };
        var source = new Esiur.Data.Record(targetDefinition)
        {
            [nameof(TargetRecord.Enabled)] = true,
            [nameof(TargetRecord.Count)] = (short)7,
            [nameof(TargetRecord.Nested)] = nested,
        };

        var converted = Assert.IsType<TargetRecord>(
            RuntimeCaster.Cast(source, typeof(TargetRecord)));

        Assert.True(converted.Enabled);
        Assert.Equal(7, converted.Count);
        Assert.Equal("edge-1", converted.Nested.Name);
    }

    private sealed class TargetRecord : IRecord
    {
        public bool Enabled { get; set; }
        public int Count { get; set; }
        public NestedRecord Nested { get; set; } = new();
    }

    private sealed class NestedRecord : IRecord
    {
        public string Name { get; set; } = string.Empty;
    }
}
