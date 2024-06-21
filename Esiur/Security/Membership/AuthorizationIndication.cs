using Esiur.Security.Authority;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Security.Membership
{
    public class AuthorizationIndication
    {
        public Session Session { get; set; }
        public AuthorizationResults Results { get; set; }
    }
}
