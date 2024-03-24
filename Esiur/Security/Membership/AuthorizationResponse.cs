using Esiur.Security.Authority;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Security.Membership
{
    public class AuthorizationResponse
    {
        public Session Session { get; set; }
        public bool Succeeded { get; set; }
    }
}
