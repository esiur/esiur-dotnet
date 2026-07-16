using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using Esiur.AspNetCore;
using Esiur.Core;
using Esiur.Net.Sockets;
using Esiur.Protocol;
using Esiur.Resource;
using Esiur.Security.Authority.Providers;
using Esiur.Security.Cryptography;
using Esiur.Stores;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Esiur.Tests.Unit;

public sealed class AspNetCoreIntegrationTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task AddEsiur_ValidatesConfigurationWhenHostStarts()
    {
        await using var application = BuildApplication(builder =>
            builder.AddMemoryStore("sys"));

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(
            () => application.StartAsync().WaitAsync(TestTimeout));

        Assert.Contains(
            "Authentication is required",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddEsiur_RejectsAnonymousOnlyRequiredEncryption()
    {
        await using var application = BuildApplication(builder => builder
            .AddMemoryStore("sys")
            .AllowAnonymous()
            .UseEncryption(new AesEncryptionProvider())
            .RequireEncryption());

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(
            () => application.StartAsync().WaitAsync(TestTimeout));

        Assert.Contains(
            "Encrypted EP sessions require authentication",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ProviderFacade_ExposesInstancesTypesAndFactoriesWithoutProtocolAliases()
    {
        var authenticationMethods = typeof(EsiurBuilder).GetMethods()
            .Where(method => method.Name == nameof(EsiurBuilder.UseAuthentication))
            .ToArray();
        var encryptionMethods = typeof(EsiurBuilder).GetMethods()
            .Where(method => method.Name == nameof(EsiurBuilder.UseEncryption))
            .ToArray();

        Assert.Equal(3, authenticationMethods.Length);
        Assert.Equal(3, encryptionMethods.Length);
        Assert.All(
            authenticationMethods.Concat(encryptionMethods),
            method => Assert.DoesNotContain(
                method.GetParameters(),
                parameter => parameter.ParameterType == typeof(string)));

        var services = new ServiceCollection();
        var esiur = services.AddEsiur();
        var authenticationProvider = new PasswordAuthenticationProvider();
        var encryptionProvider = new AesEncryptionProvider();

        esiur
            .UseAuthentication(authenticationProvider)
            .UseAuthentication<PasswordAuthenticationProvider>()
            .UseAuthentication(_ => authenticationProvider)
            .UseEncryption(encryptionProvider)
            .UseEncryption<AesEncryptionProvider>()
            .UseEncryption(_ => encryptionProvider);
    }

    [Fact]
    public async Task ProviderFacade_RegistersAndAllowsOnlyProviderDefaultNames()
    {
        var authenticationProvider = new PasswordAuthenticationProvider();
        var encryptionProvider = new AesEncryptionProvider();
        await using var application = BuildApplication(builder => builder
            .AddMemoryStore("sys")
            .UseAuthentication(authenticationProvider)
            .UseEncryption(encryptionProvider)
            .RequireEncryption());
        using var cancellation = new CancellationTokenSource(TestTimeout);

        await application.StartAsync(cancellation.Token);

        var warehouse = application.Services.GetRequiredService<Warehouse>();
        var server = application.Services.GetRequiredService<EpServer>();
        Assert.Same(
            authenticationProvider,
            warehouse.TryGetAuthenticationProvider(authenticationProvider.DefaultName));
        Assert.Same(
            encryptionProvider,
            warehouse.TryGetEncryptionProvider(encryptionProvider.DefaultName));
        Assert.Equal(
            new[] { authenticationProvider.DefaultName },
            server.AllowedAuthenticationProviders);
        Assert.Equal(
            new[] { encryptionProvider.DefaultName },
            server.AllowedEncryptionProviders);
    }

    [Theory]
    [InlineData(false, "Warehouse.Configuration.Connections is required")]
    [InlineData(true, "MaximumConnections cannot be negative")]
    public async Task AddEsiur_RejectsNullOrNegativeConnectionConfiguration(
        bool useNegativeLimit,
        string expectedFailure)
    {
        await using var application = BuildApplication(
            builder => builder.AddMemoryStore("sys").AllowAnonymous(),
            configureWarehouse: configuration =>
            {
                if (useNegativeLimit)
                    configuration.Connections.MaximumConnections = -1;
                else
                    configuration.Connections = null!;
            });

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(
            () => application.StartAsync().WaitAsync(TestTimeout));

        Assert.Contains(expectedFailure, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddEsiur_RejectsPerConnectionSendLimitAboveHostBudget()
    {
        await using var application = BuildApplication(builder => builder
            .AddMemoryStore("sys")
            .AllowAnonymous()
            .LimitPendingWebSocketSendBytes(2)
            .LimitTotalPendingWebSocketSendBytes(1));

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(
            () => application.StartAsync().WaitAsync(TestTimeout));

        Assert.Contains(
            "cannot exceed the host-wide send limit",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AddEsiur_UsesCodeOnlyExceptionsUnlessMessagesAreExplicitlyIncluded(
        bool includeMessages)
    {
        await using var application = BuildApplication(builder =>
        {
            builder.AddMemoryStore("sys").AllowAnonymous();
            if (includeMessages)
                builder.IncludeExceptionMessages();
        });

        var options = application.Services
            .GetRequiredService<IOptions<EsiurOptions>>()
            .Value;
        var expected = ExceptionLevel.Code;
        if (includeMessages)
            expected |= ExceptionLevel.Message;

        Assert.Equal(expected, options.Server.ExceptionLevel);
        Assert.Equal(includeMessages, options.Server.ExceptionLevel.HasFlag(ExceptionLevel.Message));
        Assert.False(options.Server.ExceptionLevel.HasFlag(ExceptionLevel.Source));
        Assert.False(options.Server.ExceptionLevel.HasFlag(ExceptionLevel.Trace));
    }

    [Fact]
    public async Task MappedEndpoint_DoesNotAffectOrdinaryHttp_AndRequiresWebSocketUpgrade()
    {
        await using var host = await StartApplicationAsync();
        using var client = new HttpClient { BaseAddress = host.HttpAddress };

        using var healthResponse = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);
        Assert.Equal("healthy", await healthResponse.Content.ReadAsStringAsync());

        using var endpointResponse = await client.GetAsync("/esiur");
        Assert.Equal(HttpStatusCode.UpgradeRequired, endpointResponse.StatusCode);
        Assert.Equal("websocket", endpointResponse.Headers.GetValues("Upgrade").Single());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-ep")]
    public async Task MappedEndpoint_RejectsWebSocketHandshakeWithoutEpSubProtocol(
        string? protocol)
    {
        await using var host = await StartApplicationAsync();
        using var client = new HttpClient { BaseAddress = host.HttpAddress };
        using var request = CreateWebSocketUpgradeRequest("/esiur", protocol);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(
            $"'{FrameworkWebSocket.SubProtocol}' WebSocket subprotocol is required",
            await response.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task MappedEndpoint_RejectsCrossOriginBrowserHandshakeByDefault()
    {
        await using var host = await StartApplicationAsync();
        using var client = new HttpClient { BaseAddress = host.HttpAddress };
        using var request = CreateWebSocketUpgradeRequest(
            "/esiur",
            FrameworkWebSocket.SubProtocol);
        request.Headers.TryAddWithoutValidation("Origin", "https://untrusted.example");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MappedEndpoint_ExplicitOriginAllowlist_ReplacesImplicitSameOriginPolicy()
    {
        const string trustedOrigin = "https://trusted.example";
        await using var host = await StartApplicationAsync(
            configureEsiur: builder => builder.AllowWebSocketOrigins(trustedOrigin));
        using var cancellation = new CancellationTokenSource(TestTimeout);
        using var client = new HttpClient { BaseAddress = host.HttpAddress };
        using var sameOriginRequest = CreateWebSocketUpgradeRequest(
            "/esiur",
            FrameworkWebSocket.SubProtocol);
        sameOriginRequest.Headers.TryAddWithoutValidation(
            "Origin",
            host.HttpAddress.GetLeftPart(UriPartial.Authority));

        using var sameOriginResponse = await client.SendAsync(
            sameOriginRequest,
            cancellation.Token);
        Assert.Equal(HttpStatusCode.Forbidden, sameOriginResponse.StatusCode);

        using var trustedSocket = new ClientWebSocket();
        trustedSocket.Options.AddSubProtocol(FrameworkWebSocket.SubProtocol);
        trustedSocket.Options.SetRequestHeader("Origin", trustedOrigin);

        await trustedSocket.ConnectAsync(host.WebSocketAddress, cancellation.Token);

        Assert.Equal(WebSocketState.Open, trustedSocket.State);
        Assert.Equal(FrameworkWebSocket.SubProtocol, trustedSocket.SubProtocol);
        await WaitUntilAsync(
            () => host.Server.Connections.Count == 1,
            cancellation.Token);

        trustedSocket.Abort();
        await WaitUntilAsync(
            () => host.Server.Connections.Count == 0,
            cancellation.Token);
    }

    [Fact]
    public async Task MappedEndpoint_AppliesConfiguredPendingSendLimitToAcceptedSocket()
    {
        const long pendingSendLimit = 4 * 1024;
        await using var host = await StartApplicationAsync(
            configureEsiur: builder =>
                builder.LimitPendingWebSocketSendBytes(pendingSendLimit));
        using var cancellation = new CancellationTokenSource(TestTimeout);
        var clientSocket = new FrameworkWebSocket(host.WebSocketAddress);

        try
        {
            Assert.True(await clientSocket.Connect(host.WebSocketAddress, cancellation.Token));
            Assert.True(clientSocket.Begin());
            await WaitUntilAsync(
                () => host.Server.Connections.Count == 1,
                cancellation.Token);

            var connection = Assert.Single(host.Server.Connections);
            var acceptedSocket = Assert.IsType<FrameworkWebSocket>(connection.Socket);
            Assert.Equal(pendingSendLimit, acceptedSocket.MaximumPendingSendBytes);
            Assert.NotNull(acceptedSocket.PendingSendBudget);
        }
        finally
        {
            clientSocket.Destroy();
        }

        await WaitUntilAsync(
            () => host.Server.Connections.Count == 0,
            cancellation.Token);
    }

    [Fact]
    public async Task FrameworkWebSocket_ConnectsThroughKestrel_WithRealPeerAdmission()
    {
        await using var host = await StartApplicationAsync(
            configureWarehouse: configuration =>
                configuration.Connections.MaximumConnectionsPerIpAddress = 1);
        using var cancellation = new CancellationTokenSource(TestTimeout);
        var socket = new FrameworkWebSocket(host.WebSocketAddress);

        try
        {
            Assert.True(await socket.Connect(host.WebSocketAddress, cancellation.Token));
            Assert.Equal(SocketState.Established, socket.State);
            Assert.True(socket.Begin());

            await WaitUntilAsync(
                () => host.Server.Connections.Count == 1,
                cancellation.Token);

            var connection = Assert.Single(host.Server.Connections);
            Assert.Equal(IPAddress.Loopback, connection.RemoteEndPoint.Address);
            Assert.Equal(1, host.Server.GetConnectionCount(IPAddress.Loopback));

            var rejectedSocket = new FrameworkWebSocket(host.WebSocketAddress);
            try
            {
                // The HTTP upgrade can complete before the EP admission decision closes the
                // second transport. What matters is that it is never added to the server.
                if (await rejectedSocket.Connect(host.WebSocketAddress, cancellation.Token))
                    rejectedSocket.Begin();

                await WaitUntilAsync(
                    () => rejectedSocket.State == SocketState.Closed,
                    cancellation.Token);

                Assert.Single(host.Server.Connections);
                Assert.Equal(1, host.Server.GetConnectionCount(IPAddress.Loopback));
            }
            finally
            {
                rejectedSocket.Destroy();
            }
        }
        finally
        {
            socket.Destroy();
        }

        using var cleanupCancellation = new CancellationTokenSource(TestTimeout);
        await WaitUntilAsync(
            () => host.Server.Connections.Count == 0
                && host.Server.GetConnectionCount(IPAddress.Loopback) == 0,
            cleanupCancellation.Token);
    }

    [Fact]
    public async Task HostShutdown_CancelsWebSocketAndCleansUpAdmission()
    {
        await using var host = await StartApplicationAsync();
        using var cancellation = new CancellationTokenSource(TestTimeout);
        var socket = new FrameworkWebSocket(host.WebSocketAddress);

        try
        {
            Assert.True(await socket.Connect(host.WebSocketAddress, cancellation.Token));
            Assert.True(socket.Begin());
            await WaitUntilAsync(
                () => host.Server.Connections.Count == 1,
                cancellation.Token);

            await host.Application.StopAsync(cancellation.Token);

            Assert.True(socket.Completion.IsCompleted);
            Assert.Equal(SocketState.Closed, socket.State);
            Assert.Empty(host.Server.Connections);
            Assert.Equal(0, host.Server.GetConnectionCount(IPAddress.Loopback));
        }
        finally
        {
            socket.Destroy();
        }
    }

    [Fact]
    public async Task AspNetHosting_DoesNotOpenTheNativeEpTcpListener()
    {
        var nativePort = GetUnusedTcpPort();
        await using var host = await StartApplicationAsync(
            configureServer: server => server.Port = checked((ushort)nativePort));

        Assert.False(host.Server.EnableTcpListener);
        Assert.False(host.Server.IsRunning);

        var probe = new TcpListener(IPAddress.Loopback, nativePort);
        try
        {
            probe.Start();
        }
        finally
        {
            probe.Stop();
        }
    }

    [Fact]
    public async Task ExternalWarehouse_MustBeOpenBeforeEsiurStarts()
    {
        var warehouse = new Warehouse();
        await warehouse.Put("sys", new MemoryStore());
        var server = await warehouse.Put("sys/server", new EpServer
        {
            EnableTcpListener = false,
            AuthenticationTimeout = TestTimeout,
        });

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options =>
            options.Listen(IPAddress.Loopback, 0));
        builder.Services
            .AddEsiur(warehouse, server, manageWarehouseLifecycle: false)
            .UsePasswordAuthentication((_, _) => null);

        await using var application = builder.Build();
        application.UseWebSockets();
        application.MapEsiur();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => application.StartAsync().WaitAsync(TestTimeout));

        Assert.Contains(
            "externally managed Warehouse must be open",
            exception.Message,
            StringComparison.Ordinal);
        Assert.Null(warehouse.TryGetAuthenticationProvider(
            PasswordAuthenticationProvider.ProtocolName));
        Assert.Empty(server.AllowedAuthenticationProviders);
    }

    [Fact]
    public async Task ExternalWarehouse_DoesNotRetainFacadeAuthenticationOnShutdown()
    {
        var warehouse = new Warehouse();
        await warehouse.Put("sys", new MemoryStore());
        var server = await warehouse.Put("sys/server", new EpServer
        {
            EnableTcpListener = false,
            AuthenticationTimeout = TestTimeout,
        });
        await warehouse.Open();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options =>
            options.Listen(IPAddress.Loopback, 0));
        builder.Services
            .AddEsiur(warehouse, server, manageWarehouseLifecycle: false)
            .UsePasswordAuthentication((_, _) => null);

        await using var application = builder.Build();
        application.UseWebSockets();
        application.MapEsiur();

        try
        {
            using var cancellation = new CancellationTokenSource(TestTimeout);
            await application.StartAsync(cancellation.Token);
            Assert.NotNull(warehouse.TryGetAuthenticationProvider(
                PasswordAuthenticationProvider.ProtocolName));
            Assert.Equal(
                new[] { PasswordAuthenticationProvider.ProtocolName },
                server.AllowedAuthenticationProviders);

            await application.StopAsync(cancellation.Token);

            Assert.Null(warehouse.TryGetAuthenticationProvider(
                PasswordAuthenticationProvider.ProtocolName));
            Assert.Empty(server.AllowedAuthenticationProviders);
            Assert.True(warehouse.IsOpen);
        }
        finally
        {
            if (warehouse.IsOpen)
                await warehouse.Close();
        }
    }

    [Fact]
    public async Task PasswordShortcut_UsesValidatedNonAliasedCredentialsAndUnlinkableDummies()
    {
        var knownHash = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
        var knownSalt = Enumerable.Range(33, 32).Select(value => (byte)value).ToArray();
        await using var application = BuildApplication(builder => builder
            .AddMemoryStore("sys")
            .UsePasswordAuthentication((identity, _) => identity switch
            {
                "known" => new PasswordHash(knownHash, knownSalt),
                "bad-hash" => new PasswordHash(new byte[31], new byte[32]),
                "bad-salt" => new PasswordHash(new byte[32], new byte[31]),
                "partial" => new PasswordHash(new byte[32], null!),
                _ => null,
            }));

        using var cancellation = new CancellationTokenSource(TestTimeout);
        await application.StartAsync(cancellation.Token);

        var warehouse = application.Services.GetRequiredService<Warehouse>();
        var provider = Assert.IsAssignableFrom<PasswordAuthenticationProvider>(
            warehouse.TryGetAuthenticationProvider(
                PasswordAuthenticationProvider.ProtocolName));

        var firstUnknown = provider.GetHostedAccountCredential("missing", "example");
        var secondUnknown = provider.GetHostedAccountCredential("missing", "example");
        var otherUnknown = provider.GetHostedAccountCredential("other", "example");
        var originalDummyHash = (byte[])firstUnknown.Hash.Clone();
        var originalDummySalt = (byte[])firstUnknown.Salt.Clone();

        Assert.Equal(32, firstUnknown.Hash.Length);
        Assert.Equal(32, firstUnknown.Salt.Length);
        Assert.Equal(originalDummyHash, secondUnknown.Hash);
        Assert.Equal(originalDummySalt, secondUnknown.Salt);
        Assert.NotSame(firstUnknown.Hash, secondUnknown.Hash);
        Assert.NotSame(firstUnknown.Salt, secondUnknown.Salt);
        Assert.NotEqual(originalDummyHash, otherUnknown.Hash);
        Assert.NotEqual(originalDummySalt, otherUnknown.Salt);
        Assert.NotEqual(knownHash, originalDummyHash);
        Assert.NotEqual(knownSalt, originalDummySalt);

        firstUnknown.Hash[0] ^= 0xff;
        firstUnknown.Salt[0] ^= 0xff;
        var thirdUnknown = provider.GetHostedAccountCredential("missing", "example");
        Assert.Equal(originalDummyHash, thirdUnknown.Hash);
        Assert.Equal(originalDummySalt, thirdUnknown.Salt);

        var firstKnown = provider.GetHostedAccountCredential("known", "example");
        Assert.Equal(knownHash, firstKnown.Hash);
        Assert.Equal(knownSalt, firstKnown.Salt);
        Assert.NotSame(knownHash, firstKnown.Hash);
        Assert.NotSame(knownSalt, firstKnown.Salt);

        firstKnown.Hash[0] ^= 0xff;
        firstKnown.Salt[0] ^= 0xff;
        var secondKnown = provider.GetHostedAccountCredential("known", "example");
        Assert.Equal(knownHash, secondKnown.Hash);
        Assert.Equal(knownSalt, secondKnown.Salt);

        Assert.Throws<InvalidOperationException>(() =>
            provider.GetHostedAccountCredential("bad-hash", "example"));
        Assert.Throws<InvalidOperationException>(() =>
            provider.GetHostedAccountCredential("bad-salt", "example"));
        Assert.Throws<InvalidOperationException>(() =>
            provider.GetHostedAccountCredential("partial", "example"));
        Assert.Null(provider.GetHostedAccountCredential(
            new string('i', 513),
            "example").Hash);
        Assert.Null(provider.GetHostedAccountCredential(
            "missing",
            new string('d', 513)).Hash);
    }

    [Fact]
    public async Task StartupFailure_RollsBackResourcesOwnedByTheFacade()
    {
        var attachedBeforeFailure = new LifecycleTestResource();
        var authenticationProvider = new PasswordAuthenticationProvider();
        await using var application = BuildApplication(builder => builder
            .AddMemoryStore("sys")
            .AddResource("sys/first", attachedBeforeFailure)
            .AddResource<LifecycleTestResource>(
                "sys/failure",
                _ => throw new InvalidOperationException("factory failed"))
            .UseAuthentication(authenticationProvider));
        var warehouse = application.Services.GetRequiredService<Warehouse>();
        var server = application.Services.GetRequiredService<EpServer>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => application.StartAsync().WaitAsync(TestTimeout));

        Assert.Contains("factory failed", exception.Message, StringComparison.Ordinal);
        Assert.Null(attachedBeforeFailure.Instance);
        Assert.Null(warehouse.GetStore("sys"));
        Assert.Null(server.Instance);
        Assert.False(warehouse.IsOpen);
        Assert.Equal(0, attachedBeforeFailure.TerminationCount);
        Assert.True(server.EnableTcpListener);
        Assert.Empty(server.AllowedAuthenticationProviders);
        Assert.Null(warehouse.TryGetAuthenticationProvider(
            PasswordAuthenticationProvider.ProtocolName));
    }

    [Fact]
    public async Task StartupCancellationDuringWarehouseOpen_CompletesRollback()
    {
        var resource = new BlockingStartupResource();
        await using var application = BuildApplication(builder => builder
            .AddMemoryStore("sys")
            .AddResource("sys/blocking", resource)
            .AllowAnonymous());
        using var cancellation = new CancellationTokenSource();

        var start = application.StartAsync(cancellation.Token);
        await resource.InitializeStarted.WaitAsync(TestTimeout);
        cancellation.Cancel();
        resource.ReleaseInitialize();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => start.WaitAsync(TestTimeout));
        Assert.Equal(1, resource.TerminationCount);
        Assert.Null(resource.Instance);
    }

    [Fact]
    public async Task HostShutdown_ObservesCancellationWhenResourceTerminationStalls()
    {
        var stalledResource = new LifecycleTestResource(stallTermination: true);
        await using var host = await StartApplicationAsync(
            configureEsiur: builder =>
                builder.AddResource("sys/stalled", stalledResource));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        var stop = host.Application.StopAsync(cancellation.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(200));
        stalledResource.ReleaseTermination();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => stop);

        Assert.False(host.Server.IsRunning);
        Assert.Empty(host.Server.Connections);
        Assert.Null(stalledResource.Instance);
        Assert.False(host.Application.Services.GetRequiredService<Warehouse>().IsOpen);
    }

    private static WebApplication BuildApplication(
        Action<EsiurBuilder>? configureEsiur = null,
        Action<EpServer>? configureServer = null,
        Action<WarehouseConfiguration>? configureWarehouse = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development,
        });

        builder.WebHost.ConfigureKestrel(options =>
            options.Listen(IPAddress.Loopback, 0));

        var warehouse = new Warehouse();
        var server = new EpServer
        {
            AuthenticationTimeout = TestTimeout,
        };

        var esiur = builder.Services.AddEsiur(warehouse, server);
        configureEsiur?.Invoke(esiur);
        if (configureServer is not null)
            esiur.ConfigureServer(configureServer);
        if (configureWarehouse is not null)
            esiur.ConfigureWarehouse(configureWarehouse);

        var application = builder.Build();
        application.UseWebSockets();
        application.MapGet("/health", () => Results.Text("healthy"));
        application.MapEsiur("/esiur");
        return application;
    }

    private static async Task<TestApplication> StartApplicationAsync(
        Action<EpServer>? configureServer = null,
        Action<WarehouseConfiguration>? configureWarehouse = null,
        Action<EsiurBuilder>? configureEsiur = null)
    {
        var application = BuildApplication(
            esiur =>
            {
                esiur.AddMemoryStore("sys").AllowAnonymous();
                configureEsiur?.Invoke(esiur);
            },
            configureServer,
            configureWarehouse);

        using var cancellation = new CancellationTokenSource(TestTimeout);
        await application.StartAsync(cancellation.Token);

        var addresses = application.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()
            ?.Addresses;
        var address = Assert.Single(addresses!);
        var httpAddress = new Uri(address);
        var webSocketAddress = new UriBuilder(httpAddress)
        {
            Scheme = "ws",
            Path = "/esiur",
        }.Uri;

        return new TestApplication(application, httpAddress, webSocketAddress);
    }

    private static HttpRequestMessage CreateWebSocketUpgradeRequest(
        string path,
        string? protocol)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path)
        {
            Version = HttpVersion.Version11,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact,
        };
        request.Headers.TryAddWithoutValidation("Connection", "Upgrade");
        request.Headers.TryAddWithoutValidation("Upgrade", "websocket");
        request.Headers.TryAddWithoutValidation("Sec-WebSocket-Version", "13");
        request.Headers.TryAddWithoutValidation(
            "Sec-WebSocket-Key",
            Convert.ToBase64String(Guid.NewGuid().ToByteArray()));
        if (protocol is not null)
            request.Headers.TryAddWithoutValidation("Sec-WebSocket-Protocol", protocol);

        return request;
    }

    private static async Task WaitUntilAsync(
        Func<bool> condition,
        CancellationToken cancellationToken)
    {
        while (!condition())
            await Task.Delay(10, cancellationToken);
    }

    private static int GetUnusedTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private sealed class TestApplication : IAsyncDisposable
    {
        public TestApplication(
            WebApplication application,
            Uri httpAddress,
            Uri webSocketAddress)
        {
            Application = application;
            HttpAddress = httpAddress;
            WebSocketAddress = webSocketAddress;
            Server = application.Services.GetRequiredService<EpServer>();
        }

        public WebApplication Application { get; }
        public Uri HttpAddress { get; }
        public Uri WebSocketAddress { get; }
        public EpServer Server { get; }

        public async ValueTask DisposeAsync()
        {
            try
            {
                using var cancellation = new CancellationTokenSource(TestTimeout);
                await Application.StopAsync(cancellation.Token);
            }
            finally
            {
                await Application.DisposeAsync();
            }
        }
    }

    private sealed class LifecycleTestResource : IResource
    {
        private readonly bool stallTermination;
        private readonly AsyncReply<bool> termination = new();
        private int terminationCount;

        public LifecycleTestResource(bool stallTermination = false)
        {
            this.stallTermination = stallTermination;
        }

        public event DestroyedEvent? OnDestroy;

        public Instance? Instance { get; set; }

        public int TerminationCount => Volatile.Read(ref terminationCount);

        public void ReleaseTermination() => termination.Trigger(true);

        public AsyncReply<bool> Handle(
            ResourceOperation operation,
            IResourceContext? context = null)
        {
            if (operation == ResourceOperation.Terminate)
            {
                Interlocked.Increment(ref terminationCount);
                if (stallTermination)
                    return termination;
            }

            return new AsyncReply<bool>(true);
        }

        public void Destroy() => OnDestroy?.Invoke(this);
    }

    private sealed class BlockingStartupResource : IResource
    {
        private readonly AsyncReply<bool> initialize = new();
        private readonly TaskCompletionSource initializeStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int terminationCount;

        public event DestroyedEvent? OnDestroy;
        public Instance? Instance { get; set; }
        public Task InitializeStarted => initializeStarted.Task;
        public int TerminationCount => Volatile.Read(ref terminationCount);

        public void ReleaseInitialize() => initialize.Trigger(true);

        public AsyncReply<bool> Handle(
            ResourceOperation operation,
            IResourceContext? context = null)
        {
            if (operation == ResourceOperation.Initialize)
            {
                initializeStarted.TrySetResult();
                return initialize;
            }

            if (operation == ResourceOperation.Terminate)
                Interlocked.Increment(ref terminationCount);

            return new AsyncReply<bool>(true);
        }

        public void Destroy() => OnDestroy?.Invoke(this);
    }
}
