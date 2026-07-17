using System.Text.Json;
using Esiur.CLI;
using Esiur.CLI.Client;
using Esiur.CLI.Rendering;

namespace Esiur.CLI.Tests;

public sealed class RenderingTests
{
    [Fact]
    public async Task JsonOutputUsesStableCamelCaseShape()
    {
        var writer = new StringWriter();
        await new OutputRenderer(writer).RenderAsync(
            new[] { new PropertyResult("sys/service", "Name", 0, "Main") },
            OutputFormat.Json, default);
        using var document = JsonDocument.Parse(writer.ToString());
        var value = document.RootElement[0];
        Assert.Equal("sys/service", value.GetProperty("resource").GetString());
        Assert.Equal("Main", value.GetProperty("value").GetString());
    }

    [Fact]
    public async Task RawOutputPrintsOnlyPropertyValues()
    {
        var writer = new StringWriter();
        await new OutputRenderer(writer).RenderAsync(
            new[]
            {
                new PropertyResult("sys/service", "Name", 0, "Main"),
                new PropertyResult("sys/service", "Running", 1, true),
            }, OutputFormat.Raw, default);
        Assert.Equal($"Main{Environment.NewLine}true{Environment.NewLine}", writer.ToString());
    }

    [Fact]
    public async Task TableOutputContainsHeadersAndRows()
    {
        var writer = new StringWriter();
        await new OutputRenderer(writer).RenderAsync(
            new[] { new ResourceSummary("service", "sys/service", 3, "Demo.Service", "0x01", 0) },
            OutputFormat.Table, default);
        Assert.Contains("Session Id", writer.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sys/service", writer.ToString());
    }

    [Theory]
    [InlineData("table", OutputFormat.Table)]
    [InlineData("json", OutputFormat.Json)]
    [InlineData("jsonl", OutputFormat.JsonLines)]
    [InlineData("raw", OutputFormat.Raw)]
    public void OutputFormatsParse(string value, OutputFormat expected) =>
        Assert.Equal(expected, OutputFormatExtensions.Parse(value));
}
