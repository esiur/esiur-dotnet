using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Security.Authority
{
    public class Authentication
    {
        Certificate certificate;
        AuthenticationState state;
        AuthenticationType type;

        public Certificate Certificate
        {
            get { return certificate; }
        }

        public AuthenticationState State
        {
            get { return state; }
        }

        public AuthenticationType Type
        {
            get { return type; }
        }

        public Authentication(Certificate certificate, AuthenticationState state, AuthenticationType type)
        {

        }
    }
}
