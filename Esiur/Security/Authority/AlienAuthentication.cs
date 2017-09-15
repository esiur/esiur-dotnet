using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Security.Authority
{
    public class AlienAuthentication : Authentication
    {
        public AlienAuthentication(Certificate certificate, AuthenticationState state) : 
            base(certificate, state, AuthenticationType.Alien)
        {

        }
    }
}
