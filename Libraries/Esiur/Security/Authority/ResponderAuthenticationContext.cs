using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Security.Authority
{
    public sealed class ResponderAuthenticationContext
    {
        public string? RemoteIpAddress { get; }

        public string? LocalDomain { get; }

        public AuthenticationMode Mode { get; }

        public IReadOnlyDictionary<string, string> Headers { get; }
            = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}
