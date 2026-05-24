using Esiur.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Security.Authority
{
    public class AuthenticationResult
    {
        public AuthenticationRuling Ruling { get; internal set; }
        public string LocalIdentity { get; internal set; }
        public string RemoteIdentity { get; internal set; }

        //public object HandshakePayload { get; internal set; }

        public byte[] SessionKey { get; internal set;  }

        public ExceptionCode? ExceptionCode { get; internal set; }
        public string ExceptionMessage { get; internal set; }

        public object AuthenticationData { get; internal set; }

        public AuthenticationResult(AuthenticationRuling ruling, object authenticationData, string localIdentity = null, string remoteIdentity = null, byte[] sessionKey = null)
        {
            Ruling = ruling;
            LocalIdentity = localIdentity;
            RemoteIdentity = remoteIdentity;
            AuthenticationData = authenticationData;
            SessionKey = sessionKey;
        }
    }
}
