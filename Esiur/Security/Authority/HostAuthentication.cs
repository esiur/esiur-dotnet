using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Security.Authority
{
    public class HostAuthentication : Authentication
    {
        public HostAuthentication(DomainCertificate certificate, AuthenticationState state) 
            : base(certificate, state, AuthenticationType.Host)
        {

        }
    }
}
