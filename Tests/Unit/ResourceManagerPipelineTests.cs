using Esiur.Data;
using Esiur.Data.Types;
using Esiur.Protocol;
using Esiur.Resource;
using Esiur.Security.Authority;
using Esiur.Security.Management;
using Esiur.Security.Permissions;
using Esiur.Stores;
using System.Text;

namespace Esiur.Tests.Unit;

public class ResourceManagerPipelineTests
{
    [Fact]
    public async Task Put_WithUnregisteredTypeManager_FailsBeforeAssigningInstance()
    {
        var warehouse = await CreateWarehouseAsync();
        var resource = new AttributeManagedResource();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await warehouse.Put("sys/unregistered-manager", resource);
        });

        Assert.Contains(nameof(AttributePermissionsManager), exception.Message);
        Assert.Null(resource.Instance);
    }

    [Fact]
    public async Task ResourceContext_RequiresTheExactRegisteredManagerInstance()
    {
        var warehouse = await CreateWarehouseAsync();
        var registered = new ContextPermissionsManager();
        var differentInstance = new ContextPermissionsManager();
        warehouse.RegisterManager(registered);

        var rejectedResource = new PlainResource();
        var rejectedContext = new ResourceContext(
            new IResourceManager[] { differentInstance });

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await warehouse.Put("sys/rejected-context-manager", rejectedResource, rejectedContext);
        });

        Assert.Null(rejectedResource.Instance);

        var acceptedContext = new ResourceContext(
            new IResourceManager[] { registered });
        var acceptedResource = await warehouse.Put(
            "sys/accepted-context-manager",
            new PlainResource(),
            acceptedContext);

        Assert.Contains(
            acceptedResource.Instance!.Managers.ToArray(),
            manager => ReferenceEquals(manager, registered));
    }

    [Fact]
    public void EvaluateManagers_ExecutesAllPermissionsManagersAndDenialOverrides()
    {
        var warehouse = new Warehouse();
        var defaultAllow = new DefaultAllowPermissionsManager();
        var localDeny = new DenyPermissionsManager();
        var trailingObserver = new ObserverPermissionsManager();

        warehouse.RegisterManager(defaultAllow, useAsDefault: true);
        warehouse.RegisterManager(localDeny);
        warehouse.RegisterManager(trailingObserver);

        Assert.Same(defaultAllow, warehouse.TryGetManager<DefaultAllowPermissionsManager>());
        Assert.Contains(typeof(DefaultAllowPermissionsManager), warehouse.GetRegisteredManagerTypes());
        Assert.Contains(
            warehouse.GetDefaultManagers(),
            manager => ReferenceEquals(manager, defaultAllow));

        var context = new ResourceManagerContext(
            warehouse,
            null,
            new Session(),
            null,
            new FunctionDef { Name = "Call" },
            ActionType.Execute);

        var evaluation = warehouse.EvaluateManagers(
            context,
            new IResourceManager[] { localDeny, trailingObserver });

        Assert.Equal(1, defaultAllow.Calls);
        Assert.Equal(1, localDeny.Calls);
        Assert.Equal(1, trailingObserver.Calls);
        Assert.Equal(Ruling.Denied, evaluation.Permissions);
        Assert.False(evaluation.IsAllowed);
    }

    [Fact]
    public void RateControlAllow_DoesNotGrantDefaultDeniedPropertySet()
    {
        var warehouse = new Warehouse();
        var rateManager = new AllowRateControlManager();
        warehouse.RegisterManager(rateManager, useAsDefault: true);

        var context = new ResourceManagerContext(
            warehouse,
            null,
            new Session(),
            null,
            new PropertyDef { Name = "Value" },
            ActionType.SetProperty);

        var evaluation = warehouse.EvaluateManagers(context);

        Assert.Equal(1, rateManager.Calls);
        Assert.Equal(Ruling.Allowed, evaluation.RateControl);
        Assert.Equal(Ruling.Denied, evaluation.Permissions);
        Assert.False(evaluation.IsAllowed);
    }

    [Fact]
    public async Task MemberPolicyAttributes_AreLocalAndNotTransportedToRemoteTypeDefs()
    {
        var warehouse = await CreateWarehouseAsync();
        var local = new LocalTypeDef(typeof(MemberPolicyResource), warehouse);

        var localFunction = local.GetFunctionDefByName(nameof(MemberPolicyResource.Call));
        var localProperty = local.GetPropertyDefByName(nameof(MemberPolicyResource.Value));
        var localEvent = local.GetEventDefByName(nameof(MemberPolicyResource.Changed));

        Assert.Equal("call-rate", localFunction!.RatePolicyName);
        Assert.Equal("set-rate", localProperty!.RatePolicyName);
        Assert.Equal(
            "call-policy",
            Assert.Single(localFunction.MemberPolicyAttributes.OfType<LocalMemberPolicyAttribute>()).Name);
        Assert.Equal(
            "property-policy",
            Assert.Single(localProperty.MemberPolicyAttributes.OfType<LocalMemberPolicyAttribute>()).Name);
        Assert.Equal(
            "event-policy",
            Assert.Single(localEvent!.MemberPolicyAttributes.OfType<LocalMemberPolicyAttribute>()).Name);

        var bytes = Codec.Compose(TypeDefInfo.FromTypeDef(local), warehouse, null!);
        var wireText = Encoding.UTF8.GetString(bytes);
        Assert.DoesNotContain("call-policy", wireText);
        Assert.DoesNotContain("property-policy", wireText);
        Assert.DoesNotContain("event-policy", wireText);
        Assert.DoesNotContain("call-rate", wireText);
        Assert.DoesNotContain("set-rate", wireText);

        var connection = await warehouse.Put("sys/type-parser", new EpConnection());
        connection.RemoteDomain = "remote.test";
        var remote = await RemoteTypeDef.Parse(
            new RemoteTypeDef(),
            connection.RemoteDomain,
            bytes,
            connection,
            Array.Empty<ulong>());

        Assert.Empty(remote.GetFunctionDefByName(nameof(MemberPolicyResource.Call))!
            .MemberPolicyAttributes);
        Assert.Empty(remote.GetPropertyDefByName(nameof(MemberPolicyResource.Value))!
            .MemberPolicyAttributes);
        Assert.Empty(remote.GetEventDefByName(nameof(MemberPolicyResource.Changed))!
            .MemberPolicyAttributes);
        Assert.Null(remote.GetFunctionDefByName(nameof(MemberPolicyResource.Call))!.RatePolicyName);
        Assert.Null(remote.GetPropertyDefByName(nameof(MemberPolicyResource.Value))!.RatePolicyName);
    }

    static async Task<Warehouse> CreateWarehouseAsync()
    {
        var warehouse = new Warehouse();
        await warehouse.Put("sys", new MemoryStore());
        return warehouse;
    }

    abstract class TestPermissionsManager : IPermissionsManager
    {
        readonly Ruling _ruling;

        protected TestPermissionsManager(Ruling ruling)
        {
            _ruling = ruling;
        }

        public int Calls { get; private set; }
        public Map<string, object> Settings { get; } = new();

        public Ruling Applicable(
            IResource resource,
            Session session,
            ActionType action,
            MemberDef member,
            object inquirer = null!)
        {
            Calls++;
            return _ruling;
        }

        public bool Initialize(Map<string, object> settings, IResource resource) => true;
    }

    sealed class AttributePermissionsManager : TestPermissionsManager
    {
        public AttributePermissionsManager() : base(Ruling.DontCare)
        {
        }
    }

    sealed class ContextPermissionsManager : TestPermissionsManager
    {
        public ContextPermissionsManager() : base(Ruling.DontCare)
        {
        }
    }

    sealed class DefaultAllowPermissionsManager : TestPermissionsManager
    {
        public DefaultAllowPermissionsManager() : base(Ruling.Allowed)
        {
        }
    }

    sealed class DenyPermissionsManager : TestPermissionsManager
    {
        public DenyPermissionsManager() : base(Ruling.Denied)
        {
        }
    }

    sealed class ObserverPermissionsManager : TestPermissionsManager
    {
        public ObserverPermissionsManager() : base(Ruling.DontCare)
        {
        }
    }

    sealed class AllowRateControlManager : IRateControlManager
    {
        public int Calls { get; private set; }

        public Ruling Applicable(ResourceManagerContext context)
        {
            Calls++;
            return Ruling.Allowed;
        }
    }

    [PermissionsManager<AttributePermissionsManager>]
    sealed class AttributeManagedResource : Esiur.Resource.Resource
    {
    }

    sealed class PlainResource : Esiur.Resource.Resource
    {
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Event)]
    sealed class LocalMemberPolicyAttribute : Attribute
    {
        public string Name { get; }

        public LocalMemberPolicyAttribute(string name)
        {
            Name = name;
        }
    }

    [Export]
    sealed class MemberPolicyResource : Esiur.Resource.Resource
    {
        [RateControl("call-rate")]
        [LocalMemberPolicy("call-policy")]
        public void Call()
        {
        }

        [RateControl("set-rate")]
        [LocalMemberPolicy("property-policy")]
        public int Value { get; set; }

        [LocalMemberPolicy("event-policy")]
        public event ResourceEventHandler<int>? Changed;
    }
}
