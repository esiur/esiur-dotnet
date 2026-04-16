using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Security.Authority
{
    public class AuthenticationContext
    {
        public AuthenticationMode Mode { get; }

        public string? LocalDomain { get; }
        public string? RemoteDomain { get; }

        public string? LocalHost { get; }
        public string? RemoteHost { get; }

        //public AuthenticationComponentContext LocalToRemote { get; } = new();
        //public AuthenticationComponentContext RemoteToLocal { get; } = new();

    }
}
