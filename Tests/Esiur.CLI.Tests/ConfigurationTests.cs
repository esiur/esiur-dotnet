using System.Text.Json;
using Esiur.CLI;
using Esiur.CLI.Client;
using Esiur.CLI.Configuration;

namespace Esiur.CLI.Tests;

public sealed class ConfigurationTests
{
    [Theory]
    [InlineData("ep://localhost", "ep://localhost")]
    [InlineData("ep://example.test:9000/sys/service", "ep://example.test:9000")]
    public void EndpointParserExtractsConnectionEndpoint(string value, string expected) =>
        Assert.Equal(expected, EndpointParser.ConnectionEndpoint(value));

    [Theory]
    [InlineData("http://localhost")]
    [InlineData("ep:///missing-host")]
    [InlineData("not-an-endpoint")]
    public void EndpointParserRejectsInvalidEndpoints(string value) =>
        Assert.Throws<CliException>(() => ConfigurationResolver.ValidateEndpoint(value));

    [Theory]
    [InlineData("500ms", 500)]
    [InlineData("30s", 30_000)]
    [InlineData("2m", 120_000)]
    public void DurationParserSupportsDocumentedSuffixes(string value, double milliseconds) =>
        Assert.Equal(milliseconds, DurationParser.Parse(value).TotalMilliseconds);

    [Fact]
    public async Task ConfigurationRoundTripsProfilesWithoutSecrets()
    {
        var directory = Directory.CreateTempSubdirectory("esiur-cli-test-");
        try
        {
            var path = Path.Combine(directory.FullName, "config.json");
            var store = new ConfigurationStore(path);
            await store.SaveAsync(new CliConfiguration
            {
                DefaultProfile = "production",
                Profiles = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["production"] = new ConnectionProfile
                    {
                        Name = "production",
                        Endpoint = "ep://host",
                        Provider = "password",
                        Identity = "ahmed",
                    },
                },
            }, default);

            var text = await File.ReadAllTextAsync(path);
            Assert.DoesNotContain("\"password\":", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret", text, StringComparison.OrdinalIgnoreCase);
            var loaded = await store.LoadAsync(default);
            Assert.Equal("production", loaded.DefaultProfile);
            Assert.Equal("ep://host", loaded.Profiles["PRODUCTION"].Endpoint);
        }
        finally { directory.Delete(true); }
    }

    [Fact]
    public void ExplicitOptionsOverrideSelectedProfileAndGlobals()
    {
        var configuration = new CliConfiguration
        {
            DefaultProfile = "saved",
            OutputFormat = "table",
            Profiles = new(StringComparer.OrdinalIgnoreCase)
            {
                ["saved"] = new ConnectionProfile
                {
                    Name = "saved", Endpoint = "ep://saved", OutputFormat = "raw",
                    Identity = "stored",
                },
            },
        };
        var result = ConfigurationResolver.Resolve(configuration,
            new GlobalOptions("saved", "ep://explicit", null, "explicit", "json", TimeSpan.FromSeconds(4), false, false));
        Assert.Equal("ep://explicit", result.Endpoint);
        Assert.Equal("explicit", result.Identity);
        Assert.Equal("json", result.OutputFormat);
        Assert.Equal(TimeSpan.FromSeconds(4), result.Timeout);
    }

    [Theory]
    [InlineData("sys/service", "sys/service")]
    [InlineData("/sys/service/", "sys/service")]
    public void ResourcePathsAreNormalized(string value, string expected) =>
        Assert.Equal(expected, ResourceInspectionService.NormalizePath(value));

    [Fact]
    public void SecretsAreRedactedFromDiagnostics()
    {
        var value = CliApplication.RedactSecrets("password=secret token: abc123");
        Assert.DoesNotContain("secret", value);
        Assert.DoesNotContain("abc123", value);
        Assert.Equal("password=*** token: ***", value);
    }
}
