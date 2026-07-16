using Esiur.Core;
using Esiur.Data;
using Esiur.Net.Packets;
using Esiur.Resource;
using Esiur.Security.Authority;
using Esiur.Security.Authority.Providers;
using Esiur.Security.Cryptography;
using Esiur.Security.Membership;
using Esiur.Security.Permissions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Protocol;

public class EpConnectionContext : IResourceContext
{
    public ExceptionLevel ExceptionLevel { get; set; }
    = ExceptionLevel.Code | ExceptionLevel.Message | ExceptionLevel.Source | ExceptionLevel.Trace;

    //public Func<AuthorizationRequest, AsyncReply<object>> Authenticator { get; set; }
    //public Func<AuthorizationRequest, AsyncReply<object>> Authenticator { get; set; }

    public AuthenticationMode AuthenticationMode { get; set; } = AuthenticationMode.None;
    public string Identity { get; set; }

    /// <summary>
    /// Expected responder identity for responder-authenticated and dual-identity
    /// protocols. Leaving this unset accepts any responder identity trusted by the
    /// selected authentication provider for the requested domain.
    /// </summary>
    public string ResponderIdentity { get; set; }

    public string AuthenticationProtocol { get; set; }
        = PasswordAuthenticationProvider.ProtocolName;

    /// <summary>
    /// Controls whether the authenticated session key must protect EP traffic.
    /// </summary>
    public EncryptionMode EncryptionMode { get; set; } = EncryptionMode.None;

    /// <summary>
    /// Encryption provider protocol names offered to the responder, in preference order.
    /// </summary>
    public string[] EncryptionProviders { get; set; } = new[] { "aes-gcm" };

    public bool AutoReconnect { get; set; } = false;

    /// <summary>Delay between reconnect attempts, in seconds.</summary>
    public uint ReconnectInterval { get; set; } = 5;

    /// <summary>
    /// Maximum time allowed for authentication, encryption setup, and any required
    /// protected key rotation. A non-positive value disables the deadline.
    /// </summary>
    public TimeSpan AuthenticationTimeout { get; set; } = TimeSpan.FromSeconds(30);

    //public string Username { get; set; }

    /// <summary>
    /// Uses WebSocket transport when set. The absolute <c>ws</c> or <c>wss</c>
    /// URI can include the endpoint path and query string.
    /// </summary>
    public Uri WebSocketUri { get; set; }

    // public string Password { get; set; }

    //public string Token { get; set; }

    //public ulong TokenIndex { get; set; }

    public string Domain { get; set; }

    public Map<string, object> Attributes {  get; set; }

    public Map<string, object> Properties { get; set; }

    public ulong Age { get; }
}
