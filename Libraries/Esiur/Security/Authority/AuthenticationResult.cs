using Esiur.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Security.Authority
{
    public class AuthenticationResult
    {
        public AuthenticationRuling Ruling { get; internal set; }
        public string Identity { get; internal set; }

        public object HandshakePayload { get; internal set; }

        public byte[] SessionKey { get; internal set;  }

        public ExceptionCode? ExceptionCode { get; internal set; }
        public string ExceptionMessage { get; internal set; }

        public AuthenticationResult(AuthenticationRuling ruling, string identity, object handshakePayload, byte[] sessionKey)
        {
            Ruling = ruling;
            Identity = identity;
            HandshakePayload = handshakePayload;
            SessionKey = sessionKey;
        }
    }
}
