using Esiur.Engine;
using Esiur.Net;
using Esiur.Resource;
using Esiur.Resource.Template;
using Esiur.Security.Authority;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Security.Permissions
{
    public interface IPermissionManager
    {
        bool Applicable(IResource resource, Session session, ActionType action, MemberTemplate member);
    }
}
