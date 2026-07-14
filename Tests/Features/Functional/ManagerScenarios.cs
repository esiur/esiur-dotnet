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
using Esiur.Data.Types;
using Esiur.Protocol;
using Esiur.Resource;
using Esiur.Security.Authority;
using Esiur.Security.Management;
using Esiur.Security.Permissions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace Esiur.Tests.Functional;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, Inherited = true)]
internal sealed class FunctionalManagerPolicyAttribute : Attribute
{
    public string Name { get; }
    public Ruling Permissions { get; set; } = Ruling.DontCare;
    public Ruling RateControl { get; set; } = Ruling.DontCare;

    public FunctionalManagerPolicyAttribute(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A manager policy name is required.", nameof(name));

        Name = name;
    }
}

internal sealed class ManagerObservation
{
    public ActionType Action { get; }
    public string? MemberName { get; }
    public string? PolicyName { get; }
    public string? CallerIdentity { get; }
    public bool HasConnection { get; }
    public IResource? Resource { get; }

    public ManagerObservation(
        ActionType action,
        string? memberName,
        string? policyName,
        string? callerIdentity,
        bool hasConnection,
        IResource? resource)
    {
        Action = action;
        MemberName = memberName;
        PolicyName = policyName;
        CallerIdentity = callerIdentity;
        HasConnection = hasConnection;
        Resource = resource;
    }
}

internal abstract class ManagerObservationRecorder
{
    readonly ConcurrentQueue<ManagerObservation> observations = new();

    protected static FunctionalManagerPolicyAttribute? GetPolicy(MemberDef? member)
        => member?.MemberPolicyAttributes
            .OfType<FunctionalManagerPolicyAttribute>()
            .SingleOrDefault();

    protected void Record(
        ActionType action,
        MemberDef? member,
        IResource? resource,
        Session? session,
        EpConnection? connection)
    {
        observations.Enqueue(new ManagerObservation(
            action,
            member?.Name,
            GetPolicy(member)?.Name,
            session?.RemoteIdentity ?? session?.LocalIdentity,
            connection != null,
            resource));
    }

    public ManagerObservation[] ExecuteObservations(string memberName)
        => observations
            .Where(observation =>
                observation.Action == ActionType.Execute &&
                string.Equals(observation.MemberName, memberName, StringComparison.Ordinal))
            .ToArray();
}

internal sealed class DefaultAllowPermissionsManager :
    ManagerObservationRecorder,
    IPermissionsManager
{
    public Map<string, object> Settings { get; } = new();

    public Ruling Applicable(
        IResource resource,
        Session session,
        ActionType action,
        MemberDef member,
        object inquirer = null!)
    {
        Record(action, member, resource, session, inquirer as EpConnection);

        return action == ActionType.Execute || action == ActionType.SetProperty
            ? Ruling.Allowed
            : Ruling.DontCare;
    }

    public bool Initialize(Map<string, object> settings, IResource resource) => true;
}

internal sealed class ProbeDenyPermissionsManager :
    ManagerObservationRecorder,
    IPermissionsManager
{
    public Map<string, object> Settings { get; } = new();

    public Ruling Applicable(
        IResource resource,
        Session session,
        ActionType action,
        MemberDef member,
        object inquirer = null!)
    {
        Record(action, member, resource, session, inquirer as EpConnection);
        return GetPolicy(member)?.Permissions ?? Ruling.DontCare;
    }

    public bool Initialize(Map<string, object> settings, IResource resource) => true;
}

internal sealed class ProbeRateControlManager :
    ManagerObservationRecorder,
    IRateControlManager
{
    public Ruling Applicable(ResourceManagerContext context)
    {
        Record(
            context.Action,
            context.Member,
            context.Resource,
            context.Session,
            context.Connection);

        var ruling = GetPolicy(context.Member)?.RateControl ?? Ruling.DontCare;
        if (ruling == Ruling.Denied)
            context.DenialReason = "The functional rate-control manager denied the operation.";

        return ruling;
    }
}

internal sealed class AttributeProbeAuditingManager :
    ManagerObservationRecorder,
    IAuditingManager
{
    public Ruling Applicable(ResourceManagerContext context)
    {
        Record(
            context.Action,
            context.Member,
            context.Resource,
            context.Session,
            context.Connection);

        return Ruling.Allowed;
    }
}

internal sealed class ContextProbeAuditingManager :
    ManagerObservationRecorder,
    IAuditingManager
{
    public Ruling Applicable(ResourceManagerContext context)
    {
        Record(
            context.Action,
            context.Member,
            context.Resource,
            context.Session,
            context.Connection);

        return Ruling.DontCare;
    }
}

[Resource]
[PermissionsManager<ProbeDenyPermissionsManager>]
[RateControlManager<ProbeRateControlManager>]
[AuditingManager<AttributeProbeAuditingManager>]
public partial class ManagerProbeResource
{
    int allowedExecutions;
    int permissionDeniedExecutions;
    int rateDeniedExecutions;

    internal int AllowedExecutions => Volatile.Read(ref allowedExecutions);
    internal int PermissionDeniedExecutions => Volatile.Read(ref permissionDeniedExecutions);
    internal int RateDeniedExecutions => Volatile.Read(ref rateDeniedExecutions);

    [Export]
    [FunctionalManagerPolicy(
        "manager-allowed",
        Permissions = Ruling.DontCare,
        RateControl = Ruling.Allowed)]
    public int AllowedCall(int value)
    {
        Interlocked.Increment(ref allowedExecutions);
        return value + 1;
    }

    [Export]
    [FunctionalManagerPolicy(
        "manager-permission-denied",
        Permissions = Ruling.Denied,
        RateControl = Ruling.Allowed)]
    public int PermissionDeniedCall()
    {
        Interlocked.Increment(ref permissionDeniedExecutions);
        return permissionDeniedExecutions;
    }

    [Export]
    [FunctionalManagerPolicy(
        "manager-rate-denied",
        Permissions = Ruling.DontCare,
        RateControl = Ruling.Denied)]
    public int RateDeniedCall()
    {
        Interlocked.Increment(ref rateDeniedExecutions);
        return rateDeniedExecutions;
    }
}

internal sealed class ManagerScenarioFixture
{
    public ManagerProbeResource Resource { get; }
    public IReadOnlyList<ManagerObservationRecorder> Managers { get; }

    public ManagerScenarioFixture(
        ManagerProbeResource resource,
        params ManagerObservationRecorder[] managers)
    {
        Resource = resource;
        Managers = managers;
    }
}

internal static class ManagerScenarios
{
    static readonly (string MemberName, string PolicyName)[] expectedCalls =
    {
        (nameof(ManagerProbeResource.AllowedCall), "manager-allowed"),
        (nameof(ManagerProbeResource.PermissionDeniedCall), "manager-permission-denied"),
        (nameof(ManagerProbeResource.RateDeniedCall), "manager-rate-denied"),
    };

    public static async Task Run(EpConnection connection, ManagerScenarioFixture fixture)
    {
        Console.WriteLine("Registered resource managers");

        var remote = await connection.Get("sys/manager-probe") as EpResource
            ?? throw new InvalidOperationException("The manager probe resource was not found.");

        var allowed = GetFunction(remote, nameof(ManagerProbeResource.AllowedCall));
        var permissionDenied = GetFunction(remote, nameof(ManagerProbeResource.PermissionDeniedCall));
        var rateDenied = GetFunction(remote, nameof(ManagerProbeResource.RateDeniedCall));

        var allowedArguments = new Map<byte, object> { [0] = 41 };
        var allowedResult = await remote._Invoke(allowed.Index, allowedArguments);
        Require(Convert.ToInt32(allowedResult) == 42, "The manager-approved invocation returned an unexpected value.");

        await ExpectError(
            () => remote._Invoke(permissionDenied.Index, Array.Empty<object>()),
            ExceptionCode.InvokeDenied);
        await ExpectError(
            () => remote._Invoke(rateDenied.Index, Array.Empty<object>()),
            ExceptionCode.RateLimitExceeded);

        Require(fixture.Resource.AllowedExecutions == 1, "The allowed manager probe did not execute exactly once.");
        Require(fixture.Resource.PermissionDeniedExecutions == 0, "A permissions-denied invocation reached the resource.");
        Require(fixture.Resource.RateDeniedExecutions == 0, "A rate-denied invocation reached the resource.");

        foreach (var manager in fixture.Managers)
        {
            foreach (var expected in expectedCalls)
            {
                var observations = manager.ExecuteObservations(expected.MemberName);
                Require(
                    observations.Length == 1,
                    $"Manager `{manager.GetType().Name}` observed `{expected.MemberName}` {observations.Length} times.");

                var observation = observations[0];
                Require(
                    string.Equals(observation.PolicyName, expected.PolicyName, StringComparison.Ordinal),
                    $"Manager `{manager.GetType().Name}` did not receive the policy for `{expected.MemberName}`.");
                Require(
                    string.Equals(observation.CallerIdentity, "tester", StringComparison.Ordinal),
                    $"Manager `{manager.GetType().Name}` did not receive the authenticated identity.");
                Require(
                    observation.HasConnection,
                    $"Manager `{manager.GetType().Name}` did not receive the connection context.");
                Require(
                    ReferenceEquals(observation.Resource, fixture.Resource),
                    $"Manager `{manager.GetType().Name}` did not receive the target resource.");
            }
        }

        Console.WriteLine("  PASS defaults, type attributes, ResourceContext, deny-overrides, and audit fan-out");
    }

    static FunctionDef GetFunction(EpResource resource, string name)
        => resource.Instance.Definition.GetFunctionDefByName(name)
            ?? throw new InvalidOperationException($"Manager probe function `{name}` was not found.");

    static async Task ExpectError(Func<AsyncReply> action, ExceptionCode expectedCode)
    {
        try
        {
            await action();
        }
        catch (AsyncException exception) when (exception.Code == expectedCode)
        {
            return;
        }

        throw new InvalidOperationException($"The request was expected to fail with `{expectedCode}`.");
    }

    static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
