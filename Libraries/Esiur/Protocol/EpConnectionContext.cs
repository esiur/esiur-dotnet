using Esiur.Core;
using Esiur.Data;
using Esiur.Net.Packets;
using Esiur.Resource;
using Esiur.Security.Membership;
using Esiur.Security.Permissions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Protocol;

public class EpConnectionContext : ResourceContext
{
    public EpConnectionContext()
        : base(0, new Map<string, object>(), null, null)
    {

    }

    public override void Build()
    {
        Attributes["AutoConnect"] = AutoReconnect;
        Attributes["ReconnectInterval"] = ReconnectInterval;
        Attributes["UseWebSocket"] = UseWebSocket;
        Attributes["SecureWebSocket"] = SecureWebSocket;
        Attributes["Domain"] = SecureWebSocket;
        Attributes["AuthenticationProtocol"] = SecureWebSocket;
        Attributes["Identity"] = SecureWebSocket;
    }

    public ExceptionLevel ExceptionLevel { get; set; }
    = ExceptionLevel.Code | ExceptionLevel.Message | ExceptionLevel.Source | ExceptionLevel.Trace;

    //public Func<AuthorizationRequest, AsyncReply<object>> Authenticator { get; set; }
    //public Func<AuthorizationRequest, AsyncReply<object>> Authenticator { get; set; }

    public string Identity { get; set; }
    public string AuthenticationProtocol { get; set; }

    public bool AutoReconnect { get; set; } = false;

    public uint ReconnectInterval { get; set; } = 5;

    //public string Username { get; set; }

    public bool UseWebSocket { get; set; }

    public bool SecureWebSocket { get; set; }

    // public string Password { get; set; }

    //public string Token { get; set; }

    //public ulong TokenIndex { get; set; }

    public string Domain { get; set; }
}
