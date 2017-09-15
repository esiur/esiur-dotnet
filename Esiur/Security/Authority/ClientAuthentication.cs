using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Security.Authority
{
    public class ClientAuthentication : Authentication
    {
        public ClientAuthentication(byte[] credentials, UserCertificate certificate, AuthenticationState state) 
            : base(certificate, state, AuthenticationType.Client)
        {

        }
    }
}
