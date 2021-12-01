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
using Esiur.Resource.Template;
using Esiur.Security.Authority;

namespace Esiur.Security.Permissions;

public class UserPermissionsManager : IPermissionsManager
{
    IResource resource;
    Structure settings;

    public Structure Settings => settings;

    public Ruling Applicable(IResource resource, Session session, ActionType action, MemberTemplate member, object inquirer)
    {
        Structure userPermissions = null;

        if (settings.ContainsKey(session.RemoteAuthentication.FullName))
            userPermissions = settings[session.RemoteAuthentication.FullName] as Structure;
        else if (settings.ContainsKey("public"))
            userPermissions = settings["public"] as Structure;
        else
            return Ruling.Denied;

        if (action == ActionType.Attach)// || action == ActionType.Delete)
        {
            if ((string)userPermissions["_attach"] != "yes")
                return Ruling.Denied;
        }
        else if (action == ActionType.Delete)
        {
            if ((string)userPermissions["_delete"] != "yes")
                return Ruling.Denied;
        }
        else if (action == ActionType.InquireAttributes)
        {
            if ((string)userPermissions["_get_attributes"] == "yes")
                return Ruling.Denied;
        }
        else if (action == ActionType.UpdateAttributes)
        {
            if ((string)userPermissions["_set_attributes"] != "yes")
                return Ruling.Denied;
        }
        else if (action == ActionType.AddChild)
        {
            if ((string)userPermissions["_add_child"] != "yes")
                return Ruling.Denied;
        }
        else if (action == ActionType.RemoveChild)
        {
            if ((string)userPermissions["_remove_child"] != "yes")
                return Ruling.Denied;
        }
        else if (action == ActionType.AddParent)
        {
            if ((string)userPermissions["_add_parent"] != "yes")
                return Ruling.Denied;
        }
        else if (action == ActionType.RemoveParent)
        {
            if ((string)userPermissions["_remove_parent"] != "yes")
                return Ruling.Denied;
        }
        else if (action == ActionType.Rename)
        {
            if ((string)userPermissions["_rename"] != "yes")
                return Ruling.Denied;
        }
        else if (userPermissions.ContainsKey(member?.Name))
        {
            Structure methodPermissions = userPermissions[member.Name] as Structure;
            if ((string)methodPermissions[action.ToString()] != "yes")
                return Ruling.Denied;
        }

        return Ruling.DontCare;
    }

    public UserPermissionsManager()
    {

    }

    public UserPermissionsManager(Structure settings)
    {
        this.settings = settings;
    }

    public bool Initialize(Structure settings, IResource resource)
    {
        this.resource = resource;
        this.settings = settings;
        return true;
    }
}
