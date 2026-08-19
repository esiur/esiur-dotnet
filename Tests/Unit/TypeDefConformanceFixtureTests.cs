using System.Text.Json;
using Esiur.Data;
using Esiur.Data.Types;
using Esiur.Resource;

namespace Esiur.Tests.Unit;

public class TypeDefConformanceFixtureTests
{
    [Fact]
    public void DotnetEncoder_MatchesCanonicalTypeScriptFixture()
    {
        var root = ReadFixture();
        var info = new TypeDefInfo
        {
            Version = 3,
            Id = 17,
            Name = "Pump",
            Namespace = "Building.Automation",
            Kind = TypeDefKind.Resource,
            Usage = "Controls a circulation pump.",
            Description = "Pump controller",
            Example = "enabled=true",
            Category = "HVAC",
            Since = "3.1",
            Annotations = new Map<string, string> { ["owner"] = "operations" },
        };

        var encoded = Codec.Compose(info, Warehouse.Default, null);
        Assert.Equal(root.GetProperty("payloadBase64").GetString(), Convert.ToBase64String(encoded));
    }

    [Fact]
    public void TypeScriptIndexedTypeDefInfoFixture_DecodesWithExpectedSemantics()
    {
        var root = ReadFixture();
        var expected = root.GetProperty("expected");
        var payload = Convert.FromBase64String(root.GetProperty("payloadBase64").GetString()!);

        var (size, info) = Codec.ParseIndexedType<TypeDefInfo>(payload, 0, Warehouse.Default);

        Assert.Equal((uint)payload.Length, size);
        Assert.Equal(expected.GetProperty("id").GetUInt64(), info.Id);
        Assert.Equal(expected.GetProperty("version").GetInt32(), info.Version);
        Assert.Equal("Pump", info.Name);
        Assert.Equal(expected.GetProperty("namespace").GetString(), info.Namespace);
        Assert.Equal(expected.GetProperty("usage").GetString(), info.Usage);
        Assert.Equal(expected.GetProperty("description").GetString(), info.Description);
        Assert.Equal(expected.GetProperty("example").GetString(), info.Example);
        Assert.Equal(expected.GetProperty("category").GetString(), info.Category);
        Assert.Equal(expected.GetProperty("since").GetString(), info.Since);
        Assert.Equal(
            expected.GetProperty("annotations").GetProperty("owner").GetString(),
            info.Annotations!["owner"]);
    }

    private static JsonElement ReadFixture()
    {
        var location = Path.Combine(
            AppContext.BaseDirectory,
            "ConformanceFixtures",
            "typedef-info-v3-flat.json");
        using var document = JsonDocument.Parse(File.ReadAllText(location));
        return document.RootElement.Clone();
    }
}
