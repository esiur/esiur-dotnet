using System.Net;
using System.Net.Sockets;
using Esiur.CLI.Authentication;
using Esiur.CLI.Client;
using Esiur.CLI.Configuration;
using Esiur.Protocol;
using Esiur.Resource;
using Esiur.Stores;

namespace Esiur.CLI.Tests;

[Resource]
public partial class CliTestResource
{
    [Export] public string Name { get; set; } = "Main";
    [Export, ReadOnly] public bool Running { get; set; } = true;
}

public sealed class ReadOnlyIntegrationTests
{
    [Fact]
    public async Task QueryDescribeAndGetWorkAgainstInProcessServer()
    {
        var server = new Warehouse();
        EsiurSession? session = null;
        try
        {
            var port = FindAvailablePort();
            await server.Put("sys", new MemoryStore());
            await server.Put("sys/server", new EpServer { Port = port, AllowUnauthorizedAccess = true });
            await server.Put("sys/service", new CliTestResource());
            await server.Open();

            var credentials = new PromptCredentialService();
            var factory = new EsiurSessionFactory(credentials);
            var settings = new ResolvedConnection(
                "test", $"ep://localhost:{port}", null, null, null,
                "json", TimeSpan.FromSeconds(10), null);
            session = await factory.ConnectAsync(
                settings, false, TextReader.Null, TextWriter.Null, default);
            var service = new ResourceInspectionService();

            var query = await service.QueryAsync(session, "sys", 1, null, default);
            Assert.Contains(query, item => item.Path == "sys/service" && item.Type.Contains(nameof(CliTestResource)));

            var description = await service.DescribeAsync(session, "sys/service", true, default);
            Assert.Equal("sys/service", description.Path);
            Assert.Contains(description.Properties, item => item.Name == nameof(CliTestResource.Name) && Equals(item.Value, "Main"));

            var values = await service.GetAsync(session, "sys/service", new[] { "Name", "1" }, default);
            Assert.Equal("Main", values[0].Value);
            Assert.Equal(true, values[1].Value);
        }
        finally
        {
            if (session is not null) await session.DisposeAsync();
            try { await server.Close(); } catch { }
        }
    }

    static ushort FindAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = (ushort)((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
