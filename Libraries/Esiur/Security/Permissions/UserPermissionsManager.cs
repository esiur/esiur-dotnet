/*
 
Copyright (c) 2017 Ahmed Kh. Zamil

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

using System;
using System.Collections.Generic;
using System.Text;
using Esiur.Data;
using Esiur.Core;
using Esiur.Resource;
using Esiur.Security.Authority;
using Esiur.Data.Types;

namespace Esiur.Security.Permissions;

public class UserPermissionsManager : IPermissionsManager
{
    private static readonly IReadOnlyDictionary<ActionType, string> ResourcePermissionKeys =
        new Dictionary<ActionType, string>
        {
            [ActionType.Attach] = "_attach",
            [ActionType.Detach] = "_detach",
            [ActionType.Delete] = "_delete",
            [ActionType.CreateResource] = "_create_resource",
            [ActionType.InquireAttributes] = "_get_attributes",
            [ActionType.UpdateAttributes] = "_set_attributes",
            [ActionType.AddChild] = "_add_child",
            [ActionType.RemoveChild] = "_remove_child",
            [ActionType.AddParent] = "_add_parent",
            [ActionType.RemoveParent] = "_remove_parent",
            [ActionType.Rename] = "_rename",
            [ActionType.ViewTypeDef] = "_view_typedef"
        };

    IResource resource;
    Map<string, object> settings;

    public Map<string,object> Settings => settings;

    public Ruling Applicable(IResource resource, Session session, ActionType action, MemberDef member, object inquirer)
    {
        if (settings == null || session == null)
            return Ruling.Denied;

        Map<string, object> userPermissions = null;
        if (!string.IsNullOrEmpty(session.RemoteIdentity)
            && settings.TryGetValue(session.RemoteIdentity, out var identityPermissions))
            userPermissions = identityPermissions as Map<string, object>;
        else if (settings.TryGetValue("public", out var publicPermissions))
            userPermissions = publicPermissions as Map<string, object>;

        if (userPermissions == null)
            return Ruling.Denied;

        if (ResourcePermissionKeys.TryGetValue(action, out var resourcePermissionKey))
            return IsAllowed(userPermissions, resourcePermissionKey);

        // Member-level access is fail closed: an absent member/action entry must
        // never fall through to Warehouse's compatibility defaults.
        if (member == null
            || !userPermissions.TryGetValue(member.Name, out var rawMemberPermissions)
            || rawMemberPermissions is not Map<string, object> memberPermissions)
            return Ruling.Denied;

        return IsAllowed(memberPermissions, action.ToString());
    }

    private static Ruling IsAllowed(Map<string, object> permissions, string key)
        => permissions.TryGetValue(key, out var value)
           && value is string text
           && string.Equals(text, "yes", StringComparison.Ordinal)
            ? Ruling.Allowed
            : Ruling.Denied;

    public UserPermissionsManager()
    {

    }

    public UserPermissionsManager(Map<string, object> settings)
    {
        this.settings = settings;
    }

    public bool Initialize(Map<string, object> settings, IResource resource)
    {
        this.resource = resource;
        this.settings = settings;
        return true;
    }
}
