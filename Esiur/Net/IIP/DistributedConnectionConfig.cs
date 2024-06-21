using Esiur.Core;
using Esiur.Data;
using Esiur.Net.Packets;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Net.IIP
{
    public class DistributedConnectionConfig
    {
        public ExceptionLevel ExceptionLevel { get; set; }
        = ExceptionLevel.Code | ExceptionLevel.Message | ExceptionLevel.Source | ExceptionLevel.Trace;

        public Func<Map<IIPAuthPacketIAuthHeader, object>, AsyncReply<object>> Authenticator { get; set; }

        public bool AutoReconnect { get; set; } = false;

        public uint ReconnectInterval { get; set; } = 5;

        public string Username { get; set; }

        public bool UseWebSocket { get; set; }

        public bool SecureWebSocket { get; set; }

        public string Password { get; set; }

        public string Token { get; set; }

        public ulong TokenIndex { get; set; }

        public string Domain { get; set; }
    }
}
