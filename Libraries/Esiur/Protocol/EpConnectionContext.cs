using Esiur.Core;
using Esiur.Data;
using Esiur.Net.Packets;
using Esiur.Resource;
using Esiur.Security.Authority;
using Esiur.Security.Cryptography;
using Esiur.Security.Membership;
using Esiur.Security.Permissions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Protocol;

public class EpConnectionContext : IResourceContext
{
    //public EpConnectionContext()
    //    : base(0, new Map<string, object>(), null, null)
    //{

    //}

    //public override void Build()
    //{
    //    Attributes["AutoConnect"] = AutoReconnect;
    //    Attributes["ReconnectInterval"] = ReconnectInterval;
    //    Attributes["UseWebSocket"] = UseWebSocket;
    //    Attributes["SecureWebSocket"] = SecureWebSocket;
    //    Attributes["Domain"] = SecureWebSocket;
    //    Attributes["AuthenticationProtocol"] = SecureWebSocket;
    //    Attributes["Identity"] = SecureWebSocket;
    //}

    public ExceptionLevel ExceptionLevel { get; set; }
    = ExceptionLevel.Code | ExceptionLevel.Message | ExceptionLevel.Source | ExceptionLevel.Trace;

    //public Func<AuthorizationRequest, AsyncReply<object>> Authenticator { get; set; }
    //public Func<AuthorizationRequest, AsyncReply<object>> Authenticator { get; set; }

    public AuthenticationMode AuthenticationMode { get; set; } = AuthenticationMode.None;
    public string Identity { get; set; }
    public string AuthenticationProtocol { get; set; } = "hash";

    /// <summary>
    /// Controls whether the authenticated session key must protect EP traffic.
    /// </summary>
    public EncryptionMode EncryptionMode { get; set; } = EncryptionMode.None;

    /// <summary>
    /// Encryption provider protocol names offered to the responder, in preference order.
    /// </summary>
    public string[] EncryptionProviders { get; set; } = new[] { "aes-gcm" };

    public bool AutoReconnect { get; set; } = false;

    public uint ReconnectInterval { get; set; } = 5;

    //public string Username { get; set; }

    public bool UseWebSocket { get; set; }

    public bool SecureWebSocket { get; set; }

    // public string Password { get; set; }

    //public string Token { get; set; }

    //public ulong TokenIndex { get; set; }

    public string Domain { get; set; }

    public Map<string, object> Attributes {  get; set; }

    public Map<string, object> Properties { get; set; }

    public ulong Age { get; }
}
