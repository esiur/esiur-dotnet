using Esiur.Data;
using Esiur.Security.Authority;
using Esiur.Security.Permissions;
using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Text;

namespace Esiur.Resource
{
    public class ResourceContext
    {
        public ulong Age { get; }
        public Map<string, object> Attributes { get; }
        public Map<string, object> Properties { get; }
        public IPermissionsManager PermissionsManager { get; }
     
        public ResourceContext(ulong age, Map<string, object> attributes, Map<string, object> properties, IPermissionsManager permissionsManager)
        {
            Age = age;
            Attributes = attributes;
            Properties = properties;
            PermissionsManager = permissionsManager;
        }

        public virtual void Build()
        {
            // update the context based on the current state of the resource and its environment
        }
    }
}
