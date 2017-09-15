using Esiur.Resource;
using Esiur.Security.Authority;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Authority
{
    public interface IDomain : IResource
    {
        string Name { get; }
        DomainCertificate Certificate { get; }
    }
}
