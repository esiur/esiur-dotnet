using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Security.Authority
{
    public enum CertificateType
    {
        CAPublic = 0,
        CAPrivate,
        DomainPublic,
        DomainPrivate,
        UserPublic,
        UserPrivate
    }
}
