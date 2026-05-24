using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Security.Authority
{
    public class AuthenticationContext
    {
        public AuthenticationDirection Direction { get; set; }
        public AuthenticationMode Mode { get; set; }

        public string Domain { get; set; }
        public string? InitiatorIdentity { get; set; }
        public string? ResponderIdentity { get; set; }
        public AuthenticationMaterial[] Materials { get; set; }

        public string? HostName { get; set; }
    }
}
