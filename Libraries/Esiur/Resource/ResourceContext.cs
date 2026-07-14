using Esiur.Data;
using Esiur.Security.Authority;
using Esiur.Security.Permissions;
using Esiur.Security.Management;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;

namespace Esiur.Resource
{
    public class ResourceContext : IResourceContext, IResourceManagersContext
    {
        public ulong Age { get; }
        public Map<string, object> Attributes { get; }
        public Map<string, object> Properties { get; }
        public IPermissionsManager PermissionsManager { get; }
        public IReadOnlyList<IResourceManager> ResourceManagers { get; }

        public ResourceContext(ulong age, Map<string, object> attributes, Map<string, object> properties, IPermissionsManager permissionsManager)
        {
            Age = age;
            Attributes = attributes;
            Properties = properties;
            PermissionsManager = permissionsManager;
            ResourceManagers = permissionsManager is null
                ? Array.Empty<IResourceManager>()
                : new IResourceManager[] { permissionsManager };
        }

        /// <summary>
        /// Creates a context with one or more locally supplied managers. Every manager
        /// must already be registered with the target Warehouse.
        /// </summary>
        public ResourceContext(
            IEnumerable<IResourceManager> resourceManagers,
            ulong age = 0,
            Map<string, object> attributes = null,
            Map<string, object> properties = null)
        {
            if (resourceManagers == null)
                throw new ArgumentNullException(nameof(resourceManagers));

            var managers = resourceManagers.ToArray();
            if (managers.Any(manager => manager == null))
                throw new ArgumentException(
                    "Resource managers cannot contain null values.",
                    nameof(resourceManagers));

            Age = age;
            Attributes = attributes;
            Properties = properties;
            ResourceManagers = Array.AsReadOnly(managers);
            PermissionsManager = managers.OfType<IPermissionsManager>().FirstOrDefault();
        }

        //public virtual void Build()
        //{
        //    // update the context based on the current state of the resource and its environment
        //}
    }
}
