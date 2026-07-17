using Esiur.CLI;
using Esiur.CLI.Authentication;
using Esiur.CLI.Client;
using Esiur.CLI.Configuration;

namespace Esiur.CLI.Tests;

public sealed class CommandTests
{
    [Fact]
    public async Task ProfileUseListAndRemoveShareConfigurationService()
    {
        var directory = Directory.CreateTempSubdirectory("esiur-cli-command-");
        try
        {
            var store = new ConfigurationStore(Path.Combine(directory.FullName, "config.json"));
            await store.SaveAsync(new CliConfiguration
            {
                Profiles = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["production"] = new ConnectionProfile
                    {
                        Name = "production", Endpoint = "ep://host",
                    },
                },
            }, default);
            var credentials = new PromptCredentialService();
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            var app = new CliApplication(
                store, credentials, new EsiurSessionFactory(credentials),
                new ResourceInspectionService(), TextReader.Null, stdout, stderr);

            Assert.Equal(ExitCodes.Success,
                await app.RunAsync(new[] { "profile", "use", "production" }, default));
            Assert.Equal("production", (await store.LoadAsync(default)).DefaultProfile);

            Assert.Equal(ExitCodes.Success,
                await app.RunAsync(new[] { "profile", "list", "--output", "json" }, default));
            Assert.Contains("production", stdout.ToString());

            Assert.Equal(ExitCodes.Success,
                await app.RunAsync(new[] { "profile", "remove", "production" }, default));
            var final = await store.LoadAsync(default);
            Assert.Empty(final.Profiles);
            Assert.Null(final.DefaultProfile);
            Assert.Empty(stderr.ToString());
        }
        finally { directory.Delete(true); }
    }

    [Fact]
    public async Task UnknownCommandReturnsStableInvalidArgumentsCode()
    {
        var directory = Directory.CreateTempSubdirectory("esiur-cli-command-");
        try
        {
            var credentials = new PromptCredentialService();
            var app = new CliApplication(
                new ConfigurationStore(Path.Combine(directory.FullName, "config.json")),
                credentials, new EsiurSessionFactory(credentials), new ResourceInspectionService(),
                TextReader.Null, new StringWriter(), new StringWriter());
            Assert.Equal(ExitCodes.InvalidArguments,
                await app.RunAsync(new[] { "does-not-exist" }, default));
        }
        finally { directory.Delete(true); }
    }
}
