using Esiur.Data;
using Esiur.Data.Types;
using Esiur.Security.Authority;
using Esiur.Security.Permissions;

namespace Esiur.Tests.Unit;

public class UserPermissionsManagerTests
{
    [Fact]
    public void ExplicitMemberGrant_AllowsReadAndWriteActions()
    {
        var memberPermissions = new Map<string, object>
        {
            [ActionType.GetProperty.ToString()] = "yes",
            [ActionType.SetProperty.ToString()] = "yes"
        };
        var manager = CreateManager(new Map<string, object>
        {
            ["Value"] = memberPermissions
        });
        var session = new Session { RemoteIdentity = "alice" };
        var member = new MemberDef { Name = "Value" };

        Assert.Equal(
            Ruling.Allowed,
            manager.Applicable(null!, session, ActionType.GetProperty, member, null!));
        Assert.Equal(
            Ruling.Allowed,
            manager.Applicable(null!, session, ActionType.SetProperty, member, null!));
    }

    [Fact]
    public void MissingMemberOrAction_DeniesInsteadOfFallingThrough()
    {
        var manager = CreateManager(new Map<string, object>
        {
            ["Value"] = new Map<string, object>
            {
                [ActionType.GetProperty.ToString()] = "yes"
            }
        });
        var session = new Session { RemoteIdentity = "alice" };

        Assert.Equal(
            Ruling.Denied,
            manager.Applicable(
                null!,
                session,
                ActionType.Execute,
                new MemberDef { Name = "Missing" },
                null!));
        Assert.Equal(
            Ruling.Denied,
            manager.Applicable(
                null!,
                session,
                ActionType.SetProperty,
                new MemberDef { Name = "Value" },
                null!));
    }

    [Fact]
    public void ExplicitResourceGrant_AllowsAttributeInquiry()
    {
        var manager = CreateManager(new Map<string, object>
        {
            ["_get_attributes"] = "yes"
        });

        Assert.Equal(
            Ruling.Allowed,
            manager.Applicable(
                null!,
                new Session { RemoteIdentity = "alice" },
                ActionType.InquireAttributes,
                null!,
                null!));
    }

    [Fact]
    public void MissingIdentityAndMalformedPermissionMaps_FailClosed()
    {
        var manager = new UserPermissionsManager(new Map<string, object>
        {
            ["alice"] = "not a permission map"
        });

        Assert.Equal(
            Ruling.Denied,
            manager.Applicable(
                null!,
                new Session { RemoteIdentity = "bob" },
                ActionType.Attach,
                null!,
                null!));
        Assert.Equal(
            Ruling.Denied,
            manager.Applicable(
                null!,
                new Session { RemoteIdentity = "alice" },
                ActionType.Attach,
                null!,
                null!));
    }

    private static UserPermissionsManager CreateManager(Map<string, object> permissions)
        => new(new Map<string, object> { ["alice"] = permissions });
}
