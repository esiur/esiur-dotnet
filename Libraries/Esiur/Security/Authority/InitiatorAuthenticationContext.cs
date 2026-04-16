using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Security.Authority
{
    public sealed class InitiatorAuthenticationContext
    {
        public string LocalIdentity { get; } = string.Empty;
        public string RemoteIdentity { get; } = string.Empty;

        public string? RemoteDomain { get; }
        public string? LocalDomain { get; }

        public string? RemoteIpAddress { get; }

        public AuthenticationMode Mode { get; }
    }
}
