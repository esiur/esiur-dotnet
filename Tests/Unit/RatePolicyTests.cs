using Esiur.Data.Types;
using Esiur.Protocol;
using Esiur.Resource;
using Esiur.Security.Authority;
using Esiur.Security.Permissions;
using Esiur.Security.RateLimiting;
using System.Reflection;

namespace Esiur.Tests.Unit;

public class RatePolicyTests
{
    [Fact]
    public void RateControlAttribute_IsCapturedForFunctionsAndProperties()
    {
        var warehouse = new Warehouse();
        var method = typeof(RateFixture).GetMethod(nameof(RateFixture.Call))!;
        var property = typeof(RateFixture).GetProperty(nameof(RateFixture.Value))!;

        var functionDefinition = FunctionDef.MakeFunctionDef(
            warehouse, typeof(RateFixture), method, 0, method.Name, new TypeDef());
        var propertyDefinition = PropertyDef.MakePropertyDef(
            warehouse, typeof(RateFixture), property, property.Name, 0, new TypeDef());

        Assert.Equal("standard-call", functionDefinition.RatePolicyName);
        Assert.Equal("standard-set", propertyDefinition.RatePolicyName);
    }

    [Fact]
    public void Warehouse_RegistersPoliciesByTheirConfiguredName()
    {
        var warehouse = new Warehouse();
        var policy = new DenyPolicy { Name = "deny" };

        warehouse.AddRatePolicy(policy);

        Assert.Same(policy, warehouse.TryGetRatePolicy("deny"));
        Assert.Throws<InvalidOperationException>(() => warehouse.AddRatePolicy(policy));
        Assert.True(warehouse.RemoveRatePolicy("deny"));
        Assert.Null(warehouse.TryGetRatePolicy("deny"));
    }

    [Fact]
    public void ContextFreePolicies_AreSupported()
    {
        var policy = new DenyPolicy();
        Assert.Equal(Ruling.Denied, policy.Applicable(CreateContext()));
    }

    [Fact]
    public void BurstPolicy_AllowsBurstQueuesOverflowAndThenDenies()
    {
        var policy = new BurstRatePolicy("standard-call")
        {
            PermitLimit = 1,
            Period = TimeSpan.FromSeconds(1),
            BurstLimit = 1,
            QueueLimit = 1,
        };

        var immediateOne = CreateContext();
        var immediateTwo = CreateContext(immediateOne.Connection);
        var queued = CreateContext(immediateOne.Connection);
        var denied = CreateContext(immediateOne.Connection);

        Assert.Equal(Ruling.Allowed, policy.Applicable(immediateOne));
        Assert.Equal(Ruling.Allowed, policy.Applicable(immediateTwo));
        Assert.Equal(Ruling.Allowed, policy.Applicable(queued));
        Assert.True(queued.Delay > TimeSpan.Zero);
        Assert.Equal(Ruling.Denied, policy.Applicable(denied));
    }

    [Fact]
    public void WarehouseConfiguration_IsIsolatedPerWarehouse()
    {
        var configured = new Warehouse(new WarehouseConfiguration
        {
            RateControl = new RateControlConfiguration
            {
                DenialsBeforeConnectionBlock = 2,
                DenialWindow = TimeSpan.FromSeconds(10),
                ConnectionBlockDelay = TimeSpan.Zero,
            },
        });
        var defaults = new Warehouse();

        Assert.Equal(2, configured.Configuration.RateControl.DenialsBeforeConnectionBlock);
        Assert.Equal(5, defaults.Configuration.RateControl.DenialsBeforeConnectionBlock);
    }

    static RateControlContext CreateContext(EpConnection? connection = null)
        => new(
            new Warehouse(),
            connection ?? new EpConnection(),
            new Session(),
            null,
            new FunctionDef { Name = "Call" },
            ActionType.Execute);

    sealed class DenyPolicy : RatePolicy
    {
        public override Ruling Applicable() => Ruling.Denied;
    }

    sealed class RateFixture
    {
        [RateControl("standard-call")]
        public void Call()
        {
        }

        [RateControl("standard-set")]
        public int Value { get; set; }
    }
}
