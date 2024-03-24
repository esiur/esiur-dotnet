using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Security.Membership
{
    public class AuthorizationResults
    {
        AuthorizationResultsResponse Response { get; set; }
        TwoFactorAuthorizationMethod TwoFactorMethod { get; set; }
        public string Clue { get; set; }
        public string AppName { get; set; }
        public string Code { get; set; }
        public int Timeout { get; set; }
    }
}
