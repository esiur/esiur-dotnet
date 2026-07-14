/*
 
Copyright (c) 2017-2026 Ahmed Kh. Zamil

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/

using Esiur.Core;
using Esiur.Data;
using Esiur.Protocol;
using Esiur.Resource;
using Esiur.Security.Authority;
using Esiur.Security.Cryptography;
using Esiur.Security.Management;
using Esiur.Security.RateLimiting;
using Esiur.Stores;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace Esiur.Tests.Functional;

internal static class Program
{
    static async Task Main()
    {
        var serverWarehouse = new Warehouse();
        var clientWarehouse = new Warehouse();
        EpConnection? connection = null;

        try
        {
            var port = FindAvailablePort();
            var server = await StartServer(serverWarehouse, port);

            connection = await ConnectClient(clientWarehouse, port);
            Require(connection.IsEncrypted, "Authenticated connection did not enable AES encryption.");
            var remote = await connection.Get("sys/service") as EpResource
                ?? throw new InvalidOperationException("Remote service was not found.");

            await RunCoreScenarios(connection, server.Service, remote);
            await ManagerScenarios.Run(connection, server.ManagerScenario);
            await RunRateControlScenarios(remote);
            await RunStreamingScenarios(server.Service, remote);

            Console.WriteLine();
            Console.WriteLine("All functional scenarios passed.");
        }
        finally
        {
            try { connection?.Destroy(); } catch { }
            try { await clientWarehouse.Close(); } catch { }
            try { await serverWarehouse.Close(); } catch { }
        }
    }

    static async Task<(MyService Service, ManagerScenarioFixture ManagerScenario)> StartServer(
        Warehouse warehouse,
        ushort port)
    {
        warehouse.RegisterAuthenticationProvider(new ServerAuthenticationProvider());
        warehouse.RegisterEncryptionProvider(new AesEncryptionProvider());

        var defaultPermissions = new DefaultAllowPermissionsManager();
        var denyPermissions = new ProbeDenyPermissionsManager();
        var rateControl = new ProbeRateControlManager();
        var attributeAudit = new AttributeProbeAuditingManager();
        var contextAudit = new ContextProbeAuditingManager();

        warehouse.RegisterPermissionsManager(defaultPermissions);
        warehouse.RegisterManager(denyPermissions);
        warehouse.RegisterRateControlManager(rateControl);
        warehouse.RegisterAuditingManager(attributeAudit);
        warehouse.RegisterAuditingManager(contextAudit);

        warehouse.Configuration.Parser.MaximumPacketSize = 8 * 1024 * 1024;
        warehouse.Configuration.Parser.MaximumAllocationSize = 4 * 1024 * 1024;
        warehouse.Configuration.Parser.MaximumCollectionItems = 65_536;
        warehouse.Configuration.ResourceAttachments.MaximumAttachedResourcesPerConnection = 4_096;
        warehouse.Configuration.ResourceAttachments.MaximumPendingAttachmentsPerConnection = 128;
        warehouse.Configuration.Connections.MaximumConnectionsPerIpAddress = 64;
        warehouse.Configuration.RateControl.DenialsBeforeConnectionBlock = 10;
        warehouse.AddRatePolicy(new BurstRatePolicy("standard-call")
        {
            PermitLimit = 1,
            Period = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });
        warehouse.AddRatePolicy(new BurstRatePolicy("standard-set")
        {
            PermitLimit = 1,
            Period = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });

        await warehouse.Put("sys", new MemoryStore());
        var server = await warehouse.Put("sys/server", new EpServer
        {
            Port = port,
            AllowedAuthenticationProviders = new[] { "hash" },
            AllowedEncryptionProviders = new[] { AesEncryptionProvider.Name },
            RequireEncryption = true,
        });

        var service = await warehouse.Put("sys/service", new MyService());
        var managerProbe = await warehouse.Put(
            "sys/manager-probe",
            new ManagerProbeResource(),
            new ResourceContext(new IResourceManager[] { contextAudit }));
        var resource1 = await warehouse.Put("sys/service/r1", new MyResource
        {
            Description = "Testing 1",
            CategoryId = 10,
        });
        var resource2 = await warehouse.Put("sys/service/r2", new MyResource
        {
            Description = "Testing 2",
            CategoryId = 11,
        });
        var child1 = await warehouse.Put("sys/service/c1", new MyChildResource
        {
            ChildName = "Child 1",
            Description = "Child Testing 3",
            CategoryId = 12,
        });
        var child2 = await warehouse.Put("sys/service/c2", new MyChildResource
        {
            ChildName = "Child 2",
            Description = "Testing lifecycle controls",
            CategoryId = 12,
        });

        service.Resource = resource1;
        service.ChildResource = child1;
        service.Resources = new MyResource[] { resource1, resource2, resource1, child1 };
        service.MyResources = new MyResource[] { resource1, resource2, child1, child2 };

        server.MapCall("Hello", (string message, DateTime _, EpConnection __) => $"Hi {message}");
        server.MapCall("temp", () => child2);

        await warehouse.Open();
        return (
            service,
            new ManagerScenarioFixture(
                managerProbe,
                defaultPermissions,
                denyPermissions,
                rateControl,
                attributeAudit,
                contextAudit));
    }

    static async Task<EpConnection> ConnectClient(Warehouse warehouse, ushort port)
    {
        warehouse.RegisterAuthenticationProvider(new ClientAuthenticationProvider());
        warehouse.RegisterEncryptionProvider(new AesEncryptionProvider());
        warehouse.Configuration.ResourceAttachments.MaximumAttachedResourcesPerConnection = 4_096;
        warehouse.Configuration.ResourceAttachments.MaximumPendingAttachmentsPerConnection = 128;

        return await warehouse.Get<EpConnection>($"ep://localhost:{port}", new EpConnectionContext
        {
            AuthenticationMode = AuthenticationMode.InitializerIdentity,
            AutoReconnect = false,
            Identity = "tester",
            AuthenticationProtocol = "hash",
            Domain = "test",
            EncryptionMode = EncryptionMode.EncryptWithSessionKey,
            EncryptionProviders = new[] { AesEncryptionProvider.Name },
        });
    }

    static async Task RunCoreScenarios(EpConnection connection, MyService local, EpResource remote)
    {
        Console.WriteLine("Core RPC and serialization");
        ReportProperties(local, remote);

        dynamic api = remote;

        var procedureResult = (string)await connection.Call("Hello", "functional", DateTime.UtcNow);
        Require(procedureResult == "Hi functional", "Procedure call returned an unexpected value.");

        var temporaryResource = (IResource)await connection.Call("temp");
        Require(
            temporaryResource is EpResource &&
            temporaryResource.Instance?.Definition.Name.EndsWith(nameof(MyChildResource), StringComparison.Ordinal) == true,
            "Procedure call did not return the expected remote resource type.");

        var optional = await api.Optional(new { a1 = 22, a2 = 33, a4 = "What?" });
        Require(optional is double, "Optional argument invocation failed.");

        var hello = await api.AsyncHello();
        Require(hello is not null, "AsyncReply invocation returned null.");

        await api.Void();
        await api.Connection("connection", 33);
        await api.ConnectionOptional("optional connection", 88);

        var tuple = await api.GetTuple4(1, "A", 1.3, true);
        Require(tuple is not null, "Tuple invocation returned null.");

        var eventReceived = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        EpResourceEvent handler = (_, value) => eventReceived.TrySetResult((string)value);
        await remote.Subscribe(nameof(MyService.StringEvent));
        api.StringEvent += handler;

        await api.InvokeEvents("event payload");
        var eventValue = await eventReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Require(eventValue == "event payload", "Remote event payload did not round-trip.");

        api.StringEvent -= handler;
        await remote.Unsubscribe(nameof(MyService.StringEvent));
        Console.WriteLine("  PASS core RPC, optional arguments, tuples, and events");
    }

    static async Task RunStreamingScenarios(MyService local, EpResource remote)
    {
        Console.WriteLine("Streaming and execution controls");

        var pull = InvokeStream<int>(remote, nameof(MyService.PullRange), 10, 5, 5);
        var pulledValues = new List<int>();
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
        {
            await foreach (var value in pull.WithCancellation(timeout.Token))
                pulledValues.Add(value);
        }

        Require(
            pulledValues.SequenceEqual(new[] { 10, 11, 12, 13, 14 }),
            "PullStream did not preserve item order or backpressure.");
        Console.WriteLine("  PASS PullStream / IAsyncEnumerable<T>");

        var terminatedBefore = local.TerminatedPullStreams;
        var infinite = InvokeStream<int>(remote, nameof(MyService.PullForever), 5);
        var infiniteEnumerator = infinite.GetAsyncEnumerator();

        Require(await infiniteEnumerator.MoveNextAsync(), "Infinite pull stream returned no first item.");
        Require(await infiniteEnumerator.MoveNextAsync(), "Infinite pull stream returned no second item.");
        await infiniteEnumerator.DisposeAsync();

        await WaitUntil(
            () => local.TerminatedPullStreams == terminatedBefore + 1,
            TimeSpan.FromSeconds(5));
        Require(infinite.Completed, "TerminateExecution did not complete the client stream.");
        Console.WriteLine("  PASS TerminateExecution / enumerator disposal");

        var push = InvokeStream<int>(remote, nameof(MyService.PushSequence), 4, 150);
        var pushEnumerator = push.GetAsyncEnumerator();
        var pushedValues = new List<int>();

        Require(await pushEnumerator.MoveNextAsync(), "Push stream returned no first item.");
        pushedValues.Add(pushEnumerator.Current);

        await push.Halt();
        var haltedMove = pushEnumerator.MoveNextAsync().AsTask();
        await Task.Delay(250);
        Require(!haltedMove.IsCompleted, "HaltExecution did not pause the producer.");

        await push.Resume();
        Require(
            await haltedMove.WaitAsync(TimeSpan.FromSeconds(5)),
            "ResumeExecution did not resume the producer.");
        pushedValues.Add(pushEnumerator.Current);

        while (await pushEnumerator.MoveNextAsync())
            pushedValues.Add(pushEnumerator.Current);

        Require(
            pushedValues.SequenceEqual(new[] { 0, 1, 2, 3 }),
            "Push stream lost or reordered items across halt/resume.");
        Console.WriteLine("  PASS HaltExecution / ResumeExecution");
    }

    static async Task RunRateControlScenarios(EpResource remote)
    {
        Console.WriteLine("Rate control");

        var function = remote.Instance.Definition.GetFunctionDefByName(nameof(MyService.RateLimitedCall))
            ?? throw new InvalidOperationException("Rate-limited function was not found.");
        var property = remote.Instance.Definition.GetPropertyDefByName(nameof(MyService.RateLimitedValue))
            ?? throw new InvalidOperationException("Rate-limited property was not found.");

        await remote._Invoke(function.Index, Array.Empty<object>());
        await ExpectRateLimit(() => remote._Invoke(function.Index, Array.Empty<object>()));

        await remote.SetResourcePropertyAsync(property.Index, 1);
        await ExpectRateLimit(() => remote.SetResourcePropertyAsync(property.Index, 2));

        Console.WriteLine("  PASS function-call and property-set policies");
    }

    static async Task ExpectRateLimit(Func<AsyncReply> action)
    {
        try
        {
            await action();
        }
        catch (AsyncException exception) when (exception.Code == ExceptionCode.RateLimitExceeded)
        {
            return;
        }

        throw new InvalidOperationException("The request was expected to be rate limited.");
    }

    static AsyncStreamReply<T> InvokeStream<T>(
        EpResource resource,
        string functionName,
        params object[] arguments)
    {
        var function = resource.Instance.Definition.GetFunctionDefByName(functionName)
            ?? throw new InvalidOperationException($"Function `{functionName}` was not found.");

        var indexedArguments = new Map<byte, object>();
        for (byte i = 0; i < arguments.Length; i++)
            indexedArguments[i] = arguments[i];

        return resource._InvokeStream<T>(function.Index, indexedArguments);
    }

    static void ReportProperties(MyService local, EpResource remote)
    {
        var compared = 0;
        var definition = local.Instance?.Definition
            ?? throw new InvalidOperationException("Local service was not initialized.");

        foreach (var property in definition.Properties)
        {
            if (!remote.TryGetPropertyValue(property.Index, out _))
                throw new InvalidOperationException($"Remote property `{property.Name}` was not attached.");

            compared++;
        }

        Console.WriteLine($"  Attached {compared} exported properties");
    }

    static async Task WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException("Timed out waiting for the functional condition.");

            await Task.Delay(10);
        }
    }

    static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
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
