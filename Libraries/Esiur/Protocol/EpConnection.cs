/*
 
Copyright (c) 2017-2025 Ahmed Kh. Zamil

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/

using Esiur.Core;
using Esiur.Data;
using Esiur.Data.Types;
using Esiur.Misc;
using Esiur.Net;
using Esiur.Net.Http;
using Esiur.Net.Packets;
using Esiur.Net.Packets.Http;
using Esiur.Net.Sockets;
using Esiur.Resource;
using Esiur.Security.Authority;
using Esiur.Security.Cryptography;
using Esiur.Security.Membership;
using Esiur.Security.Permissions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace Esiur.Protocol;

public partial class EpConnection : NetworkConnection, IStore
{


    public delegate void ProtocolGeneralHandler(EpConnection connection, ParsedTdu dataType, byte[] data);

    public delegate void ProtocolRequestReplyHandler(EpConnection connection, uint callbackId, ParsedTdu dataType, byte[] data);

    // Delegates
    public delegate void ReadyEvent(EpConnection sender);
    public delegate void ErrorEvent(EpConnection sender, byte errorCode, string errorMessage);
    public delegate void ResumedEvent(EpConnection sender);

    // Events

    /// <summary>
    /// Ready event is raised when autoReconnect is enabled and the connection is restored.
    /// </summary>
    public event ResumedEvent OnResumed;

    /// <summary>
    /// Ready event is raised when the connection is fully established.
    /// </summary>
    public event ReadyEvent OnReady;

    /// <summary>
    /// Error event
    /// </summary>
    public event ErrorEvent OnError;


    // Fields
    bool _invalidCredentials = false;

    enum OutboundProtectionState : byte
    {
        Plaintext,
        Encrypted,
        Closed,
    }

    const int EncryptedRecordHeaderSize = 4;
    readonly object _encryptionSendLock = new object();
    NetworkBuffer _decryptedReceiveBuffer = new NetworkBuffer();
    OutboundProtectionState _outboundProtectionState = OutboundProtectionState.Plaintext;
    volatile bool _decryptInbound;
    string[] _offeredEncryptionProviders = Array.Empty<string>();

    System.Timers.Timer _keepAliveTimer;
    DateTime? _lastKeepAliveSent;
    DateTime? _lastKeepAliveReceived;


    EpPacket _packet; //= new EpPacket();
    EpAuthPacket _authPacket;// = new EpAuthPacket();


    Session _session;

    AsyncReply<bool> _openReply;

    bool _authenticated;

    string _hostname;
    ushort _port;

    bool _initialPacket = true;
    AuthenticationDirection _authDirection = AuthenticationDirection.Responder;

    Map<ulong, RemoteTypeDef> _remoteTypeDefs = new Map<ulong, RemoteTypeDef>();

    // Properties

    public DateTime LoginDate { get; private set; }

    /// <summary>
    /// Distributed server responsible for this connection, usually for incoming connections.
    /// </summary>
    /// 
    EpServer _server;
    Warehouse _serverWarehouse;

    public EpServer Server => _server;
    internal Warehouse ParsingWarehouse => Instance?.Warehouse ?? _serverWarehouse ?? Warehouse.Default;
    //public EpServer Server
    //{
    //    get => _server;
    //    internal set
    //    {
    //        _server = value;
    //        if (_authPacket == null)
    //            _authPacket = new EpAuthPacket(value.Instance.Warehouse);
    //        if (_packet == null)
    //            _packet = new EpPacket(value.Instance.Warehouse);
    //    }
    //}


    /// <summary>
    /// The session related to this connection.
    /// </summary>
    public Session Session => _session;

    /// <summary>
    /// True after authenticated encryption is active in both directions.
    /// </summary>
    public bool IsEncrypted => _session?.EncryptionActive ?? false;

    [Export]
    public virtual EpConnectionStatus Status { get; private set; }

    [Export]
    public virtual uint Jitter { get; private set; }

    // Attributes

    //[Attribute]
    public uint KeepAliveTime { get; set; } = 10;

    //[Attribute]
    public ExceptionLevel ExceptionLevel { get; set; }
                = ExceptionLevel.Code | ExceptionLevel.Message | ExceptionLevel.Source | ExceptionLevel.Trace;



    //[Attribute]
    public bool AutoReconnect { get; set; } = false;

    //[Attribute]
    public uint ReconnectInterval { get; set; } = 5;

    //[Attribute]
    //public string Username { get; set; }

    //[Attribute]
    public bool UseWebSocket { get; set; }

    //[Attribute]
    public bool SecureWebSocket { get; set; }

    //[Attribute]
    //public string Password { get; set; }

    //[Attribute]
    //public string Token { get; set; }

    //[Attribute]
    //public ulong TokenIndex { get; set; }

    //[Attribute]
    //public string Domain { get; set; }

    string _remoteDomain, _localDomain;

    public string RemoteDomain
    {
        get => _remoteDomain;
        set
        {
            _remoteDomain = value;
            _session.RemoteHeaders.Domain = value;
        }
    }

    public string LocalDomain
    {
        get => _localDomain;
        set
        {
            _localDomain = value;
            _session.LocalHeaders.Domain = value;
        }

    }

    public bool Remove(IResource resource)
    {
        // nothing to do
        return true;
    }




    /// <summary>
    /// Send data to the other end as parameters
    /// </summary>
    /// <param name="values">Values will be converted to bytes then sent.</param>
    internal SendList SendParams(AsyncReply<object[]> reply = null)
    {
        return new SendList(this, reply);
    }

    /// <summary>
    /// Send raw data through the connection.
    /// </summary>
    /// <param name="data">Data to send.</param>
    public override void Send(byte[] data)
    {
#if VERBOSE
            Console.WriteLine("Client: {0}", data.Length);
#endif

        lock (_encryptionSendLock)
        {
            if (_outboundProtectionState == OutboundProtectionState.Closed)
                return;

            Global.Counters["Ep Sent Packets"]++;

            if (_outboundProtectionState == OutboundProtectionState.Encrypted)
                base.Send(ComposeEncryptedRecord(data));
            else
                base.Send(data);
        }
    }

    public override void Send(byte[] data, int offset, int length)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        if (offset < 0 || length < 0 || offset > data.Length - length)
            throw new ArgumentOutOfRangeException(nameof(offset));

        var message = new byte[length];
        Buffer.BlockCopy(data, offset, message, 0, length);
        Send(message);
    }

    public override AsyncReply<bool> SendAsync(byte[] message, int offset, int length)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));
        if (offset < 0 || length < 0 || offset > message.Length - length)
            throw new ArgumentOutOfRangeException(nameof(offset));

        lock (_encryptionSendLock)
        {
            if (_outboundProtectionState == OutboundProtectionState.Closed)
                return new AsyncReply<bool>(false);
            if (_outboundProtectionState == OutboundProtectionState.Plaintext)
                return base.SendAsync(message, offset, length);

            var plaintext = new byte[length];
            Buffer.BlockCopy(message, offset, plaintext, 0, length);
            var record = ComposeEncryptedRecord(plaintext);
            return base.SendAsync(record, 0, record.Length);
        }
    }

    byte[] ComposeEncryptedRecord(byte[] plaintext)
    {
        var cipher = _session?.SymetricCipher
            ?? throw new InvalidOperationException("Session encryption is active without a cipher.");
        var provider = _session.EncryptionProvider
            ?? throw new InvalidOperationException("Session encryption is active without a provider.");
        var maximumRecordSize = ParsingWarehouse.Configuration.Encryption.MaximumRecordSize;

        if (maximumRecordSize > 0
            && (ulong)plaintext.LongLength + provider.MaximumRecordOverhead > maximumRecordSize)
            throw new ParserLimitException(
                $"Encrypted record would exceed the {maximumRecordSize}-byte limit.");

        var protectedPayload = cipher.Encrypt(plaintext);

        if (maximumRecordSize > 0 && protectedPayload.Length > maximumRecordSize)
            throw new InvalidOperationException(
                $"Encryption provider `{provider.DefaultName}` exceeded its declared record overhead.");
        if (protectedPayload.Length > int.MaxValue - EncryptedRecordHeaderSize)
            throw new ParserLimitException("Encrypted record exceeds the runtime allocation limit.");

        var record = new byte[EncryptedRecordHeaderSize + protectedPayload.Length];
        BinaryPrimitives.WriteUInt32BigEndian(record.AsSpan(0, EncryptedRecordHeaderSize),
                                              (uint)protectedPayload.Length);
        Buffer.BlockCopy(protectedPayload, 0, record, EncryptedRecordHeaderSize, protectedPayload.Length);
        return record;
    }

    /// <summary>
    /// KeyList to store user variables related to this connection.
    /// </summary>
    public KeyList<string, object> Variables { get; } = new KeyList<string, object>();

    /// <summary>
    /// IResource interface.
    /// </summary>
    public Instance Instance
    {
        get;
        set;
    }

    /// <summary>
    /// Assign a socket to the connection.
    /// </summary>
    /// <param name="socket">Any socket that implements ISocket.</param>
    public override void Assign(ISocket socket)
    {
        base.Assign(socket);

        _session.LocalHeaders.IPAddress = socket.RemoteEndPoint.Address.GetAddressBytes();

        if (socket.State == SocketState.Established &&
            _authDirection == AuthenticationDirection.Initiator)
        {
            Declare();
        }
    }

    private void Declare()
    {
        if (_authDirection != AuthenticationDirection.Initiator)
            return;

        if (_session.EncryptionMode != EncryptionMode.None)
        {
            try
            {
                PrepareEncryptionOffer();
            }
            catch (Exception ex)
            {
                _invalidCredentials = true;
                FailPendingOpen(new AsyncException(
                    ErrorType.Management,
                    0,
                    ex.Message));
                Close();
                return;
            }
        }

        var headers = _session.LocalHeaders.Copy();

        if (_session.AuthenticationMode != AuthenticationMode.None)
        {
            if (_session.AuthenticationHandler == null)
                throw new Exception("Authentication handler must be assigned for the session.");

            var initAuthResult = _session.AuthenticationHandler.Process(null);

            if (initAuthResult.Ruling == AuthenticationRuling.Failed)
                throw new InvalidOperationException("Authentication initialization failed.");

            if (initAuthResult.Ruling == AuthenticationRuling.Succeeded)
            {
                SetSessionKey(initAuthResult.SessionKey);
                _session.LocalIdentity = initAuthResult.LocalIdentity;
                _session.RemoteIdentity = initAuthResult.RemoteIdentity;
            }

            headers.AuthenticationProtocol = _session.AuthenticationHandler.Protocol;
            headers.AuthenticationData = initAuthResult.AuthenticationData;
            headers.Domain = _remoteDomain;

        }

        SendAuthHeaders((EpAuthPacketMethod)(
              (byte)EpAuthPacketMethod.Initialize
            | ((byte)_session.AuthenticationMode & 0x3) << 2
            | ((byte)_session.EncryptionMode & 0x3)), headers);
    }

    void PrepareEncryptionOffer()
    {
        if (_session.AuthenticationMode == AuthenticationMode.None)
            throw new InvalidOperationException(
                "Session-key encryption requires an authenticated session.");
        if (_session.EncryptionMode != EncryptionMode.EncryptWithSessionKey
            && _session.EncryptionMode != EncryptionMode.EncryptWithSessionKeyAndAddress)
            throw new InvalidOperationException($"Unsupported encryption mode `{_session.EncryptionMode}`.");

        var warehouse = Instance?.Warehouse ?? _serverWarehouse ?? Warehouse.Default;
        var configured = _offeredEncryptionProviders ?? Array.Empty<string>();
        if (configured.Length == 0)
            configured = warehouse.GetEncryptionProviderNames();

        var offered = configured
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .Where(x => warehouse.TryGetEncryptionProvider(x) != null)
            .ToArray();

        if (offered.Length == 0)
            throw new InvalidOperationException(
                "Encryption was requested but none of the offered providers are registered.");

        _offeredEncryptionProviders = offered;
        _session.LocalHeaders.SupportedCiphers = offered;
        _session.LocalHeaders.CipherType = null;
        _session.LocalHeaders.CipherNonce = Global.GenerateBytes(32);
    }

    bool NegotiateEncryptionAsResponder(SessionHeaders localHeaders)
    {
        _session.EncryptionMode = _authPacket.EncryptionMode;

        if (_session.EncryptionMode == EncryptionMode.None)
        {
            if (Server?.RequireEncryption == true)
                return RejectEncryption("This server requires an encrypted authenticated session.");

            return true;
        }

        if (_session.EncryptionMode != EncryptionMode.EncryptWithSessionKey
            && _session.EncryptionMode != EncryptionMode.EncryptWithSessionKeyAndAddress)
            return RejectEncryption("The requested encryption mode is not supported.");
        if (_authPacket.AuthMode == AuthenticationMode.None)
            return RejectEncryption("Session-key encryption requires authentication.");

        var offered = _session.RemoteHeaders.SupportedCiphers ?? Array.Empty<string>();
        var allowed = Server?.AllowedEncryptionProviders ?? Array.Empty<string>();
        var selected = offered.FirstOrDefault(name =>
            !string.IsNullOrWhiteSpace(name)
            && allowed.Contains(name, StringComparer.Ordinal)
            && _serverWarehouse.TryGetEncryptionProvider(name) != null);

        if (selected == null)
            return RejectEncryption("No mutually supported encryption provider is available.");
        if (_session.RemoteHeaders.CipherNonce == null
            || _session.RemoteHeaders.CipherNonce.Length < 16
            || _session.RemoteHeaders.CipherNonce.Length > 64)
            return RejectEncryption("The initiator did not supply a valid cipher nonce.");

        _session.EncryptionProvider = _serverWarehouse.GetEncryptionProvider(selected);
        _session.LocalHeaders.SupportedCiphers = allowed
            .Where(name => _serverWarehouse.TryGetEncryptionProvider(name) != null)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        _session.LocalHeaders.CipherType = selected;
        _session.LocalHeaders.CipherNonce = Global.GenerateBytes(32);

        localHeaders.SupportedCiphers = _session.LocalHeaders.SupportedCiphers;
        localHeaders.CipherType = selected;
        localHeaders.CipherNonce = _session.LocalHeaders.CipherNonce;
        return true;
    }

    bool AcceptEncryptionAsInitiator()
    {
        if (_session.EncryptionMode == EncryptionMode.None)
            return _session.RemoteHeaders.CipherType == null
                || RejectEncryption("The responder selected encryption that was not requested.");

        var selected = _session.RemoteHeaders.CipherType;
        if (string.IsNullOrWhiteSpace(selected)
            || !_offeredEncryptionProviders.Contains(selected, StringComparer.Ordinal))
            return RejectEncryption("The responder did not select an offered encryption provider.");
        if (_session.RemoteHeaders.CipherNonce == null
            || _session.RemoteHeaders.CipherNonce.Length < 16
            || _session.RemoteHeaders.CipherNonce.Length > 64)
            return RejectEncryption("The responder did not supply a valid cipher nonce.");

        var provider = Instance?.Warehouse.TryGetEncryptionProvider(selected);
        if (provider == null)
            return RejectEncryption($"Encryption provider `{selected}` is not registered locally.");

        _session.EncryptionProvider = provider;
        return true;
    }

    bool RejectEncryption(string message)
    {
        _invalidCredentials = true;

        try
        {
            SendAuthMessage(EpAuthPacketMethod.ErrorMustEncrypt, message);
        }
        catch (Exception ex)
        {
            Global.Log("EpConnection:EncryptionNegotiation", LogType.Warning, ex.Message);
        }

        FailPendingOpen(new AsyncException(ErrorType.Management, 0, message));
        Task.Delay(100).ContinueWith(_ => Close());
        return false;
    }

    void PrepareSessionEncryption()
    {
        if (_session.EncryptionMode == EncryptionMode.None || _session.SymetricCipher != null)
            return;
        if (_session.Key == null || _session.Key.Length == 0)
            throw new InvalidOperationException(
                "The authentication provider did not derive a session key for encryption.");
        if (_session.EncryptionProvider == null)
            throw new InvalidOperationException("No encryption provider was negotiated.");

        var initiator = _authDirection == AuthenticationDirection.Initiator;
        var initiatorNonce = initiator
            ? _session.LocalHeaders.CipherNonce
            : _session.RemoteHeaders.CipherNonce;
        var responderNonce = initiator
            ? _session.RemoteHeaders.CipherNonce
            : _session.LocalHeaders.CipherNonce;
        var initiatorAddress = initiator
            ? _session.RemoteHeaders.IPAddress
            : _session.LocalHeaders.IPAddress;
        var responderAddress = initiator
            ? _session.LocalHeaders.IPAddress
            : _session.RemoteHeaders.IPAddress;
        var offeredProtocols = initiator
            ? _offeredEncryptionProviders
            : _session.RemoteHeaders.SupportedCiphers;
        var authenticationProtocol = initiator
            ? _session.AuthenticationHandler?.Protocol
            : _session.RemoteHeaders.AuthenticationProtocol;

        _session.SymetricCipher = _session.EncryptionProvider.CreateCipher(new EncryptionContext
        {
            Key = _session.Key,
            Direction = _authDirection,
            Mode = _session.EncryptionMode,
            Protocol = initiator
                ? _session.RemoteHeaders.CipherType
                : _session.LocalHeaders.CipherType,
            OfferedProtocols = offeredProtocols?.ToArray() ?? Array.Empty<string>(),
            AuthenticationMode = _session.AuthenticationMode,
            AuthenticationProtocol = authenticationProtocol,
            Domain = initiator ? _remoteDomain : _session.RemoteHeaders.Domain,
            InitiatorNonce = initiatorNonce,
            ResponderNonce = responderNonce,
            InitiatorAddress = initiatorAddress,
            ResponderAddress = responderAddress,
        });
    }

    void EnableInboundEncryption()
    {
        if (_session.SymetricCipher == null)
            throw new InvalidOperationException("Cannot enable encryption before creating a cipher.");

        lock (_encryptionSendLock)
        {
            _decryptInbound = true;
            _session.EncryptionActive =
                _outboundProtectionState == OutboundProtectionState.Encrypted;
        }
    }

    void EnableEncryption(Action firstProtectedSend = null)
    {
        if (_session.SymetricCipher == null)
            throw new InvalidOperationException("Cannot enable encryption before creating a cipher.");

        lock (_encryptionSendLock)
        {
            _decryptInbound = true;
            _outboundProtectionState = OutboundProtectionState.Encrypted;
            _session.EncryptionActive = true;
            firstProtectedSend?.Invoke();
        }
    }

    void SendPlaintextAndEnableEncryption(Action finalPlaintextSend, Action firstProtectedSend)
    {
        if (_session.SymetricCipher == null)
            throw new InvalidOperationException("Cannot enable encryption before creating a cipher.");

        lock (_encryptionSendLock)
        {
            finalPlaintextSend();
            _decryptInbound = true;
            _outboundProtectionState = OutboundProtectionState.Encrypted;
            _session.EncryptionActive = true;
            firstProtectedSend();
        }
    }

    void SetSessionKey(byte[] key)
    {
        // Authentication providers own their result buffers. Keep a private copy so
        // disconnect cleanup cannot erase a provider-wide or cached shared secret.
        var replacement = key == null ? null : (byte[])key.Clone();
        if (_session.Key != null)
            Array.Clear(_session.Key, 0, _session.Key.Length);
        _session.Key = replacement;
    }

    void CompletePendingOpen()
    {
        var pending = Interlocked.Exchange(ref _openReply, null);
        if (pending == null)
            return;

        try
        {
            pending.Trigger(true);
        }
        catch (Exception ex)
        {
            Global.Log(ex);
        }
    }

    void FailPendingOpen(Exception exception)
    {
        var pending = Interlocked.Exchange(ref _openReply, null);
        if (pending == null)
            return;

        try
        {
            pending.TriggerError(exception);
        }
        catch (Exception ex)
        {
            Global.Log(ex);
        }
    }

    void BeginPlaintextHandshake()
    {
        lock (_encryptionSendLock)
        {
            _outboundProtectionState = OutboundProtectionState.Plaintext;
            _decryptInbound = false;
            _decryptedReceiveBuffer = new NetworkBuffer();
            if (_session != null)
                _session.EncryptionActive = false;
        }
    }

    void DisposeSessionEncryption()
    {
        lock (_encryptionSendLock)
        {
            _outboundProtectionState = OutboundProtectionState.Closed;
            _decryptInbound = false;
            _decryptedReceiveBuffer = new NetworkBuffer();

            if (_session?.SymetricCipher is IDisposable disposable)
                disposable.Dispose();

            if (_session != null)
            {
                _session.SymetricCipher = null;
                _session.EncryptionProvider = null;
                _session.EncryptionActive = false;
                SetSessionKey(null);
            }
        }
    }

    /// <summary>
    /// Create a new distributed connection. 
    /// </summary>
    /// <param name="socket">Socket to transfer data through.</param>
    /// <param name="authenticationHandler">Authentication handler for the session.</param>
    /// <param name="headers">Initial local session headers.</param>
    public EpConnection(ISocket socket, IAuthenticationHandler authenticationHandler, SessionHeaders headers)
    {
        _session = new Session();

        //if (authenticationHandler.Type != AuthenticationType.Initiator)
        //    throw new Exception(""
        //session.AuthenticationType = AuthenticationMode.Initiator;
        _session.LocalHeaders = headers;

        if (authenticationHandler != null)
        {
            _session.AuthenticationHandler = authenticationHandler;
            //session.AuthenticationMode = authenticationHandler.Mode;
        }

        //this.localPasswordOrToken = DC.ToBytes(password);

        init();

        Assign(socket);
    }

    //public EpConnection(ISocket socket, string domain, ulong tokenIndex, string token)
    //{
    //    this.session = new Session();


    //    session.AuthenticationType = AuthenticationType.Client;
    //    session.LocalHeaders[EpAuthPacketHeader.Domain] = domain;
    //    session.LocalHeaders[EpAuthPacketHeader.TokenIndex] = tokenIndex;
    //    session.LocalMethod = AuthenticationMethod.Credentials;
    //    session.RemoteMethod = AuthenticationMethod.None;
    //    this.localPasswordOrToken = DC.ToBytes(token);

    //    init();

    //    Assign(socket);
    //}


    /// <summary>
    /// Create a new instance of a distributed connection
    /// </summary>
    public EpConnection()
    {
        _session = new Session();
        //session.AuthenticationType = AuthenticationMode.Responder;
        //session.AuthenticationResponder = authenticationResponder;

        //authenticationResponder.Initiate(session);
        init();
    }



    public string Link(IResource resource)
    {
        if (resource is EpResource)
        {
            var r = resource as EpResource;
            if (r.Instance.Store == this)
                return this.Instance.Name + "/" + r.ResourceInstanceId;
        }

        return null;
    }


    /// <summary>
    /// Enables or disables retaining delivered resource queue items for diagnostics.
    /// Capture is disabled by default.
    /// </summary>
    public void SetFinishedQueueCapture(bool enabled)
    {
        _queue.SetProcessedCapture(enabled);
    }

    /// <summary>
    /// Atomically returns and removes the retained delivered resource queue items.
    /// </summary>
    public List<AsyncQueueItem<EpResourceQueueItem>> GetFinishedQueue()
    {
        return _queue.DrainProcessed();
    }

    void init()
    {
        //var q = queue;
        _queue.Then((x) =>
        {
            if (x.Type == EpResourceQueueItem.DistributedResourceQueueItemType.Event)
                x.Resource._EmitEventByIndex(x.Index, x.Value);
            else
                x.Resource._UpdatePropertyByIndex(x.Index, x.Value);
        }).Error(e =>
        {
            // do nothing
            //Console.WriteLine("Queue is empty");
            throw e;
        });

        // set local nonce
        //session.LocalHeaders[EpAuthPacketHeader.Nonce] = Global.GenerateBytes(32);

        _keepAliveTimer = new System.Timers.Timer(KeepAliveInterval * 1000);
        _keepAliveTimer.Elapsed += KeepAliveTimer_Elapsed; ;
    }

    private void KeepAliveTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        if (!IsConnected)
            return;


        _keepAliveTimer.Stop();

        var now = DateTime.UtcNow;

        uint interval = _lastKeepAliveSent == null ? 0 :
                        (uint)(now - (DateTime)_lastKeepAliveSent).TotalMilliseconds;

        _lastKeepAliveSent = now;

        SendRequest(EpPacketRequest.KeepAlive, now, interval)
                .Then(x =>
                {
                    Jitter = Convert.ToUInt32(((object[])x)[1]);
                    _keepAliveTimer.Start();
                }).Error(ex =>
                {
                    _keepAliveTimer.Stop();
                    Close();
                }).Timeout((int)(KeepAliveTime * 1000), () =>
                {
                    _keepAliveTimer.Stop();
                    Close();
                });

    }

    public uint KeepAliveInterval { get; set; } = 30;

    public override void Destroy()
    {
        TerminateInvocations();
        UnsubscribeAll();
        DisposeSessionEncryption();
        this.OnReady = null;
        this.OnError = null;
        base.Destroy();
    }


    private uint processPacket(byte[] msg, uint offset, uint ends, NetworkBuffer data, int chunkId)
    {
        if (_authenticated)
        {
            var rt = _packet.Parse(msg, offset, ends);

            if (rt <= 0)
            {
                var size = ends - offset;
                data.HoldFor(msg, offset, size, size + (uint)(-rt));
                return ends;
            }
            else
            {
                offset += (uint)rt;

                if (_packet.Tdu == null &&
                    _packet.Method != EpPacketMethod.Reply &&
                    _packet.Method != EpPacketMethod.Extension)
                    return offset;

#if VERBOSE
                Console.WriteLine("Incoming: " +  _packet + " " + _packet.CallbackId);
#endif
                if (_packet.Method == EpPacketMethod.Notification)
                {

                    var dt = _packet.Tdu.Value;

                    switch (_packet.Notification)
                    {
                        // Invoke
                        case EpPacketNotification.PropertyModified:
                            EpNotificationPropertyModified(dt);
                            break;
                        case EpPacketNotification.EventOccurred:
                            EpNotificationEventOccurred(dt);
                            break;
                        // Manage
                        case EpPacketNotification.ResourceDestroyed:
                            EpNotificationResourceDestroyed(dt);
                            break;
                        case EpPacketNotification.ResourceReassigned:
                            EpNotificationResourceReassigned(dt);
                            break;
                        case EpPacketNotification.ResourceMoved:
                            EpNotificationResourceMoved(dt);
                            break;
                        case EpPacketNotification.SystemFailure:
                            EpNotificationSystemFailure(dt);
                            break;
                    }
                }
                else if (_packet.Method == EpPacketMethod.Request)
                {
                    if (IsRateControlBlocked)
                        return offset;

                    var dt = _packet.Tdu.Value;

                    switch (_packet.Request)
                    {
                        // Invoke
                        case EpPacketRequest.InvokeFunction:
                            EpRequestInvokeFunction(_packet.CallbackId, dt);
                            break;
                        case EpPacketRequest.SetProperty:
                            EpRequestSetProperty(_packet.CallbackId, dt);
                            break;
                        case EpPacketRequest.Subscribe:
                            EpRequestSubscribe(_packet.CallbackId, dt);
                            break;
                        case EpPacketRequest.Unsubscribe:
                            EpRequestUnsubscribe(_packet.CallbackId, dt);
                            break;
                        // Inquire
                        case EpPacketRequest.TypeDefIdsByNames:
                            EpRequestTypeDefIdsByNames(_packet.CallbackId, dt);
                            break;
                        case EpPacketRequest.TypeDefById:
                            EpRequestTypeDefById(_packet.CallbackId, dt);
                            break;
                        case EpPacketRequest.TypeDefByResourceId:
                            EpRequestTypeDefByResourceId(_packet.CallbackId, dt);
                            break;
                        case EpPacketRequest.Query:
                            EpRequestQueryResources(_packet.CallbackId, dt);
                            break;
                        case EpPacketRequest.LinkTypeDefs:
                            EpRequestLinkTypeDefs(_packet.CallbackId, dt);
                            break;
                        case EpPacketRequest.Token:
                            EpRequestToken(_packet.CallbackId, dt);
                            break;
                        case EpPacketRequest.GetResourceIdByLink:
                            EpRequestGetResourceIdByLink(_packet.CallbackId, dt);
                            break;
                        // Manage
                        case EpPacketRequest.AttachResource:
                            EpRequestAttachResource(_packet.CallbackId, dt);
                            break;
                        case EpPacketRequest.ReattachResource:
                            EpRequestReattachResource(_packet.CallbackId, dt);
                            break;
                        case EpPacketRequest.DetachResource:
                            EpRequestDetachResource(_packet.CallbackId, dt);
                            break;
                        case EpPacketRequest.CreateResource:
                            EpRequestCreateResource(_packet.CallbackId, dt);
                            break;
                        case EpPacketRequest.DeleteResource:
                            EpRequestDeleteResource(_packet.CallbackId, dt);
                            break;
                        case EpPacketRequest.MoveResource:
                            EpRequestMoveResource(_packet.CallbackId, dt);
                            break;
                        // Static
                        case EpPacketRequest.KeepAlive:
                            EpRequestKeepAlive(_packet.CallbackId, dt);
                            break;
                        case EpPacketRequest.ProcedureCall:
                            EpRequestProcedureCall(_packet.CallbackId, dt);
                            break;
                        case EpPacketRequest.StaticCall:
                            EpRequestStaticCall(_packet.CallbackId, dt);
                            break;
                        case EpPacketRequest.PullStream:
                            EpRequestPullStream(_packet.CallbackId, dt);
                            break;
                        case EpPacketRequest.TerminateExecution:
                            EpRequestTerminateExecution(_packet.CallbackId, dt);
                            break;
                        case EpPacketRequest.HaltExecution:
                            EpRequestHaltExecution(_packet.CallbackId, dt);
                            break;
                        case EpPacketRequest.ResumeExecution:
                            EpRequestResumeExecution(_packet.CallbackId, dt);
                            break;
                    }
                }
                else if (_packet.Method == EpPacketMethod.Reply)
                {
                    var dt = _packet.Tdu;

                    switch (_packet.Reply)
                    {
                        case EpPacketReply.Completed:
                            EpReplyCompleted(_packet.CallbackId, dt);
                            break;
                        case EpPacketReply.Stream:
                            EpReplyStream(_packet.CallbackId);
                            break;
                        case EpPacketReply.Propagated:
                            if (dt.HasValue)
                                EpReplyPropagated(_packet.CallbackId, dt.Value);
                            break;
                        case EpPacketReply.PermissionError:
                            if (dt.HasValue)
                                EpReplyError(_packet.CallbackId, dt.Value, ErrorType.Management);
                            break;
                        case EpPacketReply.ExecutionError:
                            if (dt.HasValue)
                                EpReplyError(_packet.CallbackId, dt.Value, ErrorType.Exception);
                            break;

                        case EpPacketReply.Progress:
                            if (dt.HasValue)
                                EpReplyProgress(_packet.CallbackId, dt.Value);
                            break;

                        case EpPacketReply.Chunk:
                            if (dt.HasValue)
                                EpReplyChunk(_packet.CallbackId, dt.Value);
                            break;

                        case EpPacketReply.Warning:
                            if (dt.HasValue)
                                EpReplyWarning(_packet.CallbackId, dt.Value);
                            break;

                    }
                }
                else if (_packet.Method == EpPacketMethod.Extension)
                {
                    EpExtensionAction(_packet.Extension, _packet.Tdu);
                }
            }
        }
        else
        {
            // check if the request through Websockets
            if (_initialPacket)
            {
                var available = ends - offset;
                var matchesGetPrefix = available > 0 && msg[offset] == 'G' &&
                                       (available < 2 || msg[offset + 1] == 'E') &&
                                       (available < 3 || msg[offset + 2] == 'T');

                if (matchesGetPrefix && available < 3)
                {
                    data.HoldFor(msg, offset, available, 3);
                    return ends;
                }

                _initialPacket = false;

                if (available >= 3 &&
                    msg[offset] == 'G' && msg[offset + 1] == 'E' && msg[offset + 2] == 'T')
                {
                    // Parse with http packet
                    var req = new HttpRequestPacket();
                    long pSize;
                    try
                    {
                        pSize = req.Parse(msg, offset, ends);
                    }
                    catch (Exception exception) when (
                        exception is InvalidDataException ||
                        exception is ParserLimitException ||
                        exception is ArgumentException)
                    {
                        Global.Log(exception);
                        pSize = 0;
                    }

                    if (pSize > 0)
                    {
                        // check for WS upgrade

                        if (HttpConnection.IsWebsocketRequest(req))
                        {

                            Socket?.Unhold();

                            var res = new HttpResponsePacket();

                            HttpConnection.Upgrade(req, res);

                            res.Compose(HttpComposeOption.AllCalculateLength);
                            Send(res.Data);
                            // replace my socket with websockets
                            var tcpSocket = this.Unassign();
                            var wsSocket = new WSocket(tcpSocket);
                            this.Assign(wsSocket);
                        }
                        else
                        {

                            var res = new HttpResponsePacket();
                            res.Number = HttpResponseCode.BadRequest;
                            res.Compose(HttpComposeOption.AllCalculateLength);
                            Send(res.Data);
                            //@TODO: kill the connection
                        }
                    }
                    else if (pSize < 0)
                    {
                        _initialPacket = true;
                        var requiredLength = (ulong)available + (ulong)(-pSize);
                        if (requiredLength > uint.MaxValue)
                            throw new ParserLimitException("HTTP upgrade request is too large.");

                        data.HoldFor(msg, offset, available, (uint)requiredLength);
                        return ends;
                    }
                    else
                    {
                        var res = new HttpResponsePacket
                        {
                            Number = HttpResponseCode.BadRequest
                        };
                        res.Compose(HttpComposeOption.AllCalculateLength);
                        Send(res.Data);
                    }

                    // switching completed
                    return (uint)msg.Length;
                }
            }

            var rt = _authPacket.Parse(msg, offset, ends);

            if (rt <= 0)
            {
                data.HoldFor(msg, ends + (uint)(-rt));
                return ends;
            }
            else
            {
                offset += (uint)rt;

#if VERBOSE
                Console.WriteLine($"AuthPacket: RT: {rt} {authPacket.ToString()}");
#endif

                if (_authPacket.Command == EpAuthPacketCommand.Initialize && _authDirection == AuthenticationDirection.Initiator)
                    throw new Exception("Bad authentication packet received. Connection is initiator but received an initialization packet.");

                if (_authPacket.Command == EpAuthPacketCommand.Acknowledge && _authDirection == AuthenticationDirection.Responder)
                    throw new Exception("Bad authentication packet received. Connection is responder but received an acknowledge packet.");

                if (_authPacket.Command == EpAuthPacketCommand.Initialize)
                {

                    var remoteHeaders = new SessionHeaders();
                    object remoteAuthData = null;

                    if (_authPacket.Tdu != null)
                    {
                        remoteHeaders = Codec.ParseIndexedType<SessionHeaders>(
                            _authPacket.Tdu.Value,
                            _serverWarehouse);
                        remoteAuthData = remoteHeaders.AuthenticationData;
                        remoteHeaders.AuthenticationData = null;
                    }

                    _session.RemoteHeaders = remoteHeaders;
                    _session.AuthenticationMode = _authPacket.AuthMode;
                    var localHeaders = _session.LocalHeaders.Copy();

                    if (!NegotiateEncryptionAsResponder(localHeaders))
                        return offset;

                    if (_authPacket.AuthMode == AuthenticationMode.None)
                    {
                        if (!(Server?.AllowUnauthorizedAccess ?? false))
                        {
                            SendAuthMessage(EpAuthPacketMethod.ErrorTerminate, "Unauthorized access not allowed.");
                            _invalidCredentials = true;
                            //Close();
                            return offset;
                        }

                        SendAuthHeaders(EpAuthPacketMethod.SessionEstablished, localHeaders);

                        _session.Authenticated = true;
                        _session.LocalIdentity = null;
                        _session.RemoteIdentity = null;
                        AuthenticatonCompleted();

                        return offset;
                    }

                    var authenticationProtocol = _session.RemoteHeaders.AuthenticationProtocol;
                    var allowedAuthenticationProviders =
                        Server?.AllowedAuthenticationProviders ?? Array.Empty<string>();
                    var provider = !string.IsNullOrWhiteSpace(authenticationProtocol)
                                   && allowedAuthenticationProviders.Contains(
                                       authenticationProtocol,
                                       StringComparer.Ordinal)
                        ? _serverWarehouse?.TryGetAuthenticationProvider(authenticationProtocol)
                        : null;

                    if (provider == null)
                    {
                        SendAuthHeaders(EpAuthPacketMethod.NotSupported, localHeaders);
                        _invalidCredentials = true;
                        Close();
                        return offset;
                    }

                    var handler = provider.CreateAuthenticationHandler(new AuthenticationContext()
                    {
                        Direction = AuthenticationDirection.Responder,
                        Mode = _authPacket.AuthMode,
                        Domain = _session.RemoteHeaders.Domain,
                        Materials = new AuthenticationMaterial[] { new AuthenticationMaterial() { Type = AuthenticationMaterialType.Data, Value = remoteAuthData } }
                    });

                    if (handler == null)
                    {
                        SendAuthHeaders(EpAuthPacketMethod.NotSupported, localHeaders);
                        _invalidCredentials = true;
                        Close();
                        return offset;
                    }

                    // set auth handler for the session
                    _session.AuthenticationHandler = handler;

                    var authResult = handler.Process(remoteAuthData);


                    // send acknowledgements

                    localHeaders.AuthenticationData = authResult.AuthenticationData;

                    if (authResult.Ruling == AuthenticationRuling.Failed)
                    {
                        SendAuthHeaders(EpAuthPacketMethod.Denied, localHeaders);
                        _invalidCredentials = true;
                        Task.Delay(100).ContinueWith(x => Close());

                    }
                    else if (authResult.Ruling == AuthenticationRuling.InProgress)
                    {
                        SendAuthHeaders(EpAuthPacketMethod.ProceedToHandshake, localHeaders);
                    }
                    else if (authResult.Ruling == AuthenticationRuling.Succeeded)
                    {
                        _session.Authenticated = true;
                        _session.LocalIdentity = authResult.LocalIdentity;
                        _session.RemoteIdentity = authResult.RemoteIdentity;
                        SetSessionKey(authResult.SessionKey);

                        try
                        {
                            PrepareSessionEncryption();
                        }
                        catch (Exception ex)
                        {
                            RejectEncryption(ex.Message);
                            return offset;
                        }

                        AuthenticatonCompleted(() =>
                        {
                            // The initiator needs the selected provider and responder nonce
                            // before it can construct its cipher, so this acknowledgement is
                            // intentionally the final plaintext packet in a one-step handshake.
                            if (_session.EncryptionMode != EncryptionMode.None)
                            {
                                SendPlaintextAndEnableEncryption(
                                    () => SendAuthHeaders(EpAuthPacketMethod.SessionEstablished, localHeaders),
                                    () => SendAuth(EpAuthPacketMethod.Established));
                            }
                            else
                                SendAuthHeaders(EpAuthPacketMethod.SessionEstablished, localHeaders);
                        });
                    }
                }
                else if (_authPacket.Command == EpAuthPacketCommand.Acknowledge)
                {
                    // Anonymous (None-mode) success: the responder establishes the session directly
                    // via SessionEstablished, without a handshake exchange. Complete the connection so
                    // the pending open request resolves. (Previously this was only handled inside the
                    // ProceedToHandshake branch, so a direct SessionEstablished left the initiator hung.)
                    if (_session.AuthenticationMode == AuthenticationMode.None
                        && _authPacket.Method == EpAuthPacketMethod.SessionEstablished)
                    {
                        _session.Authenticated = true;
                        _session.LocalIdentity = null;
                        _session.RemoteIdentity = null;
                        SetSessionKey(null);
                        AuthenticatonCompleted();
                        return offset;
                    }

                    if (_session.AuthenticationMode != AuthenticationMode.None
                        && _authPacket.Method == EpAuthPacketMethod.SessionEstablished)
                    {
                        var remoteHeaders = new SessionHeaders();
                        object remoteAuthData = null;

                        if (_authPacket.Tdu != null)
                        {
                            remoteHeaders = Codec.ParseIndexedType<SessionHeaders>(
                                _authPacket.Tdu.Value,
                                Instance.Warehouse);
                            remoteAuthData = remoteHeaders.AuthenticationData;
                            remoteHeaders.AuthenticationData = null;
                        }

                        _session.RemoteHeaders = remoteHeaders;
                        if (!AcceptEncryptionAsInitiator())
                            return offset;

                        if (_session.Key == null)
                        {
                            var authResult = _session.AuthenticationHandler.Process(remoteAuthData);
                            if (authResult.Ruling != AuthenticationRuling.Succeeded)
                            {
                                _invalidCredentials = true;
                                FailPendingOpen(new AsyncException(
                                    ErrorType.Management,
                                    0,
                                    "Authentication did not produce a session key."));
                                Task.Delay(100).ContinueWith(_ => Close());
                                return offset;
                            }

                            SetSessionKey(authResult.SessionKey);
                            _session.LocalIdentity = authResult.LocalIdentity;
                            _session.RemoteIdentity = authResult.RemoteIdentity;
                        }

                        _session.Authenticated = true;

                        try
                        {
                            PrepareSessionEncryption();
                            if (_session.EncryptionMode != EncryptionMode.None)
                                EnableInboundEncryption();
                        }
                        catch (Exception ex)
                        {
                            RejectEncryption(ex.Message);
                            return offset;
                        }

                        if (_session.EncryptionMode == EncryptionMode.None)
                            AuthenticatonCompleted();

                        return offset;
                    }

                    if (_authPacket.Method == EpAuthPacketMethod.ProceedToHandshake
                        || _authPacket.Method == EpAuthPacketMethod.ProceedToFinalHandshake)
                    {
                        var remoteHeaders = new SessionHeaders();
                        object remoteAuthData = null;

                        if (_authPacket.Tdu != null)
                        {
                            remoteHeaders = Codec.ParseIndexedType<SessionHeaders>(
                                _authPacket.Tdu.Value,
                                Instance.Warehouse);
                            remoteAuthData = remoteHeaders.AuthenticationData;
                            remoteHeaders.AuthenticationData = null;
                        }

                        _session.RemoteHeaders = remoteHeaders;

                        if (!AcceptEncryptionAsInitiator())
                            return offset;

                        if (_session.AuthenticationMode == AuthenticationMode.None)
                        {
                            if (_authPacket.Method == EpAuthPacketMethod.SessionEstablished)
                            {
                                _session.Authenticated = true;
                                _session.LocalIdentity = null;
                                _session.RemoteIdentity = null;
                                SetSessionKey(null);
                                AuthenticatonCompleted();
                            }
                            else
                            {
                                _invalidCredentials = true;
                                Task.Delay(100).ContinueWith(x => Close());
                            }

                            return offset;
                        }

                        var authResult = _session.AuthenticationHandler.Process(remoteAuthData);

                        if (authResult.Ruling == AuthenticationRuling.Failed)
                        {
                            SendAuth(EpAuthPacketMethod.ErrorTerminate);
                            _invalidCredentials = true;
                            Task.Delay(100).ContinueWith(x => Close());
                            return offset;
                        }
                        else if (authResult.Ruling == AuthenticationRuling.InProgress)
                        {
                            if (_authPacket.Method == EpAuthPacketMethod.ProceedToHandshake)
                            {
                                SendAuthData(EpAuthPacketMethod.Handshake,
                                              authResult.AuthenticationData);
                            }
                            else
                            {
                                throw new Exception("Bad protocol sequence.");
                            }
                        }
                        else if (authResult.Ruling == AuthenticationRuling.Succeeded)
                        {
                            _session.Authenticated = true;
                            SetSessionKey(authResult.SessionKey);
                            _session.LocalIdentity = authResult.LocalIdentity;
                            _session.RemoteIdentity = authResult.RemoteIdentity;

                            try
                            {
                                PrepareSessionEncryption();
                            }
                            catch (Exception ex)
                            {
                                RejectEncryption(ex.Message);
                                return offset;
                            }

                            // send final handshake with data
                            SendAuthData(EpAuthPacketMethod.FinalHandshake,
                                        authResult.AuthenticationData);

                            if (_session.EncryptionMode != EncryptionMode.None)
                                EnableInboundEncryption();

                            //if (_authPacket.Method == EpAuthPacketMethod.SessionEstablished)
                            //{
                            //    AuthenticatonCompleted(authResult.LocalIdentity, authResult.RemoteIdentity);
                            //}
                            //else if (_authPacket.Method == EpAuthPacketMethod.ProceedToEstablishSession
                            //        || _authPacket.Method == EpAuthPacketMethod.FinalHandshake)
                            //{
                            //    // Send establish request

                            //    SendAuthData(EpAuthPacketMethod.FinalHandshake,
                            //                    authResult.AuthenticationData);
                            //}

                        }
                    }
                    else if (_authPacket.Method == EpAuthPacketMethod.Denied)
                    {
                        var errorMessage = "Authentication error.";
                        if (_authPacket.Tdu != null)
                        {
                            var parsed = Codec.ParseSync(_authPacket.Tdu.Value, _serverWarehouse);
                            if (parsed is string parsedErrorMsg)
                                errorMessage = parsedErrorMsg;
                        }

                        _invalidCredentials = true;
                        OnError?.Invoke(this, _authPacket.ErrorCode, errorMessage);
                        FailPendingOpen(new AsyncException(
                            ErrorType.Management,
                            _authPacket.ErrorCode,
                            errorMessage));

                    }
                }
                else if (_authPacket.Command == EpAuthPacketCommand.Action)
                {

                    object authData = null;

                    if (_authPacket.Tdu != null)
                    {
                        var parsed = Codec.ParseSync(_authPacket.Tdu.Value, _serverWarehouse);
                        authData = parsed;
                    }

                    if (_authPacket.Method == EpAuthPacketMethod.Handshake
                        || _authPacket.Method == EpAuthPacketMethod.FinalHandshake)
                    {
                        var authResult = _session.AuthenticationHandler.Process(authData);

                        if (authResult.Ruling == AuthenticationRuling.Failed)
                        {
                            SendAuth(EpAuthPacketMethod.ErrorTerminate);
                            _invalidCredentials = true;
                            Task.Delay(100).ContinueWith(x => Close());
                        }
                        else if (authResult.Ruling == AuthenticationRuling.InProgress)
                        {
                            SendAuthData(EpAuthPacketMethod.Handshake, authResult.AuthenticationData);
                        }
                        else if (authResult.Ruling == AuthenticationRuling.Succeeded)
                        {
                            _session.Authenticated = true;
                            SetSessionKey(authResult.SessionKey);
                            _session.LocalIdentity = authResult.LocalIdentity;
                            _session.RemoteIdentity = authResult.RemoteIdentity;

                            if (_authDirection == AuthenticationDirection.Responder
                                && _authPacket.Method == EpAuthPacketMethod.FinalHandshake)
                            {
                                try
                                {
                                    PrepareSessionEncryption();
                                }
                                catch (Exception ex)
                                {
                                    RejectEncryption(ex.Message);
                                    return offset;
                                }

                                // Registration and receive readiness must complete before the
                                // initiator is allowed to send its first application request.
                                AuthenticatonCompleted(() =>
                                {
                                    if (_session.EncryptionMode != EncryptionMode.None)
                                    {
                                        EnableEncryption(() =>
                                        {
                                            if (authResult.AuthenticationData != null)
                                                SendAuthData(EpAuthPacketMethod.FinalHandshake,
                                                             authResult.AuthenticationData);

                                            // The completion packet is protected and confirms that
                                            // both peers derived the same transcript-bound key.
                                            SendAuth(EpAuthPacketMethod.Established);
                                        });
                                    }
                                    else
                                    {
                                        if (authResult.AuthenticationData != null)
                                            SendAuthData(EpAuthPacketMethod.FinalHandshake,
                                                         authResult.AuthenticationData);
                                        SendAuth(EpAuthPacketMethod.Established);
                                    }
                                });
                            }
                            else if (authResult.AuthenticationData != null)
                            {
                                SendAuthData(EpAuthPacketMethod.FinalHandshake,
                                             authResult.AuthenticationData);
                            }

                        }
                    }
                }
                else if (_authPacket.Command == EpAuthPacketCommand.Event)
                {
                    if (_authPacket.Method == EpAuthPacketMethod.ErrorTerminate
                        || _authPacket.Method == EpAuthPacketMethod.ErrorMustEncrypt
                        || _authPacket.Method == EpAuthPacketMethod.ErrorRetry)
                    {
                        var errorMessage = "Authentication error.";
                        if (_authPacket.Tdu != null)
                        {
                            var parsed = Codec.ParseSync(_authPacket.Tdu.Value, _serverWarehouse);
                            if (parsed is string parsedErrorMsg)
                                errorMessage = parsedErrorMsg;
                        }

                        _invalidCredentials = true;
                        OnError?.Invoke(this, _authPacket.ErrorCode, errorMessage);
                        FailPendingOpen(new AsyncException(
                            ErrorType.Management,
                            _authPacket.ErrorCode,
                            errorMessage));

                        Task.Delay(100).ContinueWith(x => Close());
                    }
                    else if (_authPacket.Method == EpAuthPacketMethod.Established)
                    {
                        if (_session.Authenticated)
                        {
                            if (_session.EncryptionMode != EncryptionMode.None)
                                EnableEncryption();

                            AuthenticatonCompleted();
                        }
                        else
                        {
                            _invalidCredentials = true;
                            OnError?.Invoke(this, _authPacket.ErrorCode, "Authentication error.");
                            FailPendingOpen(new AsyncException(ErrorType.Management, _authPacket.ErrorCode, "Authentication error."));
                            Task.Delay(100).ContinueWith(x => Close());
                        }
                    }
                    else if (_authPacket.Method == EpAuthPacketMethod.IndicationEstablished)
                    {
                        // @TODO: handle multi-factor authentication indication
                    }
                }
            }

        }

        return offset;

    }



    void AuthenticatonCompleted(Action beforeReady = null)
    {

        if (this.Instance == null)
        {
            Server.Instance.Warehouse.Put(
                Server.Instance.Link + "/" + this.GetHashCode().ToString().Replace("/", "_"), this)
                .Then(x =>
                {

                    _authenticated = true;

                    beforeReady?.Invoke();
                    Status = EpConnectionStatus.Connected;
                    CompletePendingOpen();
                    OnReady?.Invoke(this);

                    _session.AuthenticationHandler?.Provider?.Login(_session);
                    //Server?.Membership?.Login(_session);
                    LoginDate = DateTime.Now;

                }).Error(x =>
                {
                    FailPendingOpen(x);
                });
        }
        else
        {
            _authenticated = true;
            beforeReady?.Invoke();
            Status = EpConnectionStatus.Connected;

            _session.AuthenticationHandler?.Provider?.Login(_session);


            OnReady?.Invoke(this);

            var proxyTypes = Instance.Warehouse.GetProxyTypesByDomain(_remoteDomain);

            if (proxyTypes != null)
            {
                var typeDefNames = new List<string>();


                foreach (var kk in proxyTypes)
                {
                    foreach (var kv in kk.Value)
                    {
                        typeDefNames.Add(kv.Key);
                    }
                }

                if (typeDefNames.Count > 0)
                {
                    GetTypeDefIds(typeDefNames.ToArray()).Then(ids =>
                    {
                        var bag = new AsyncBag<object>();
                        foreach (var id in ids)
                            bag.Add(FetchTypeDef(id, null));

                        bag.Seal();

                        bag.Then((o) =>
                        {
                            CompletePendingOpen();
                        });
                    }).Error(ex =>
                    {
                        FailPendingOpen(ex);
                        // do nothing, proxies won't work but connection is established
                    });
                }
                else
                {
                    CompletePendingOpen();
                }
            }
            else
            {
                CompletePendingOpen();
            }

        }
    }
    //private void ProcessClientAuth(byte[] data)
    //{
    //    if (authPacket.Command == EpAuthPacketCommand.Acknowledge)
    //    {
    //        // if there is a mismatch in authentication
    //        if (session.LocalMethod != authPacket.RemoteMethod
    //            || session.RemoteMethod != authPacket.LocalMethod)
    //        {
    //            openReply?.TriggerError(new Exception("Peer refused authentication method."));
    //            openReply = null;
    //        }

    //        // Parse remote headers

    //        var dataType = authPacket.DataType.Value;

    //        var (_, parsed) = Codec.ParseSync(dataType, Instance.Warehouse);

    //        var rt = (Map<byte, object>)parsed;

    //        session.RemoteHeaders = rt.Select(x => new KeyValuePair<EpAuthPacketHeader, object>((EpAuthPacketHeader)x.Key, x.Value));

    //        if (session.LocalMethod == AuthenticationMethod.None)
    //        {
    //            // send establish
    //            SendParams()
    //                        .AddUInt8((byte)EpAuthPacketAction.EstablishNewSession)
    //                        .Done();
    //        }
    //        else if (session.LocalMethod == AuthenticationMethod.Credentials
    //                || session.LocalMethod == AuthenticationMethod.Token)
    //        {
    //            var remoteNonce = (byte[])session.RemoteHeaders[EpAuthPacketHeader.Nonce];
    //            var localNonce = (byte[])session.LocalHeaders[EpAuthPacketHeader.Nonce];

    //            // send our hash
    //            var hashFunc = SHA256.Create();
    //            // local nonce + password or token + remote nonce
    //            var challenge = hashFunc.ComputeHash(new BinaryList()
    //                                                .AddUInt8Array(localNonce)
    //                                                .AddUInt8Array(localPasswordOrToken)
    //                                                .AddUInt8Array(remoteNonce)
    //                                                .ToArray());

    //            SendParams()
    //                .AddUInt8((byte)EpAuthPacketAction.AuthenticateHash)
    //                .AddUInt8((byte)EpAuthPacketHashAlgorithm.SHA256)
    //                .AddUInt16((ushort)challenge.Length)
    //                .AddUInt8Array(challenge)
    //                .Done();
    //        }

    //    }
    //    else if (authPacket.Command == EpAuthPacketCommand.Action)
    //    {
    //        if (authPacket.Action == EpAuthPacketAction.AuthenticateHash)
    //        {
    //            var remoteNonce = (byte[])session.RemoteHeaders[EpAuthPacketHeader.Nonce];
    //            var localNonce = (byte[])session.LocalHeaders[EpAuthPacketHeader.Nonce];

    //            // check if the server knows my password
    //            var hashFunc = SHA256.Create();

    //            var challenge = hashFunc.ComputeHash(new BinaryList()
    //                                                    .AddUInt8Array(remoteNonce)
    //                                                    .AddUInt8Array(localPasswordOrToken)
    //                                                    .AddUInt8Array(localNonce)
    //                                                    .ToArray());


    //            if (challenge.SequenceEqual(authPacket.Challenge))
    //            {
    //                // send establish request
    //                SendParams()
    //                            .AddUInt8((byte)EpAuthPacketAction.EstablishNewSession)
    //                            .Done();
    //            }
    //            else
    //            {
    //                SendParams()
    //                            .AddUInt8((byte)EpAuthPacketEvent.ErrorTerminate)
    //                            .AddUInt8((byte)ExceptionCode.ChallengeFailed)
    //                            .AddUInt16(16)
    //                            .AddString("Challenge Failed")
    //                            .Done();

    //            }
    //        }
    //    }
    //    else if (authPacket.Command == EpAuthPacketCommand.Event)
    //    {
    //        if (authPacket.Event == EpAuthPacketEvent.ErrorTerminate
    //            || authPacket.Event == EpAuthPacketEvent.ErrorMustEncrypt
    //            || authPacket.Event == EpAuthPacketEvent.ErrorRetry)
    //        {
    //            invalidCredentials = true;
    //            openReply?.TriggerError(new AsyncException(ErrorType.Management, authPacket.ErrorCode, authPacket.Message));
    //            openReply = null;
    //            OnError?.Invoke(this, authPacket.ErrorCode, authPacket.Message);
    //            Close();
    //        }
    //        else if (authPacket.Event == EpAuthPacketEvent.IndicationEstablished)
    //        {
    //            session.Id = authPacket.SessionId;
    //            session.AuthorizedAccount = authPacket.AccountId.GetString(0, (uint)authPacket.AccountId.Length);

    //            ready = true;
    //            Status = EpConnectionStatus.Connected;


    //            // put it in the warehouse

    //            if (this.Instance == null)
    //            {
    //                Server.Instance.Warehouse.Put(Server + "/" + session.AuthorizedAccount.Replace("/", "_"), this)
    //                    .Then(x =>
    //                {
    //                    openReply?.Trigger(true);
    //                    OnReady?.Invoke(this);
    //                    openReply = null;


    //                }).Error(x =>
    //                {
    //                    openReply?.TriggerError(x);
    //                    openReply = null;
    //                });
    //            }
    //            else
    //            {
    //                openReply?.Trigger(true);
    //                openReply = null;

    //                OnReady?.Invoke(this);
    //            }

    //            // start perodic keep alive timer
    //            keepAliveTimer.Start();

    //        }
    //        else if (authPacket.Event == EpAuthPacketEvent.IAuthPlain)
    //        {
    //            var dataType = authPacket.DataType.Value;
    //            var (_, parsed) = Codec.ParseSync(dataType, Instance.Warehouse);
    //            var rt = (Map<byte, object>)parsed;

    //            var headers = rt.Select(x => new KeyValuePair<EpAuthPacketIAuthHeader, object>((EpAuthPacketIAuthHeader)x.Key, x.Value));
    //            var iAuthRequest = new AuthorizationRequest(headers);

    //            if (Authenticator == null)
    //            {
    //                SendParams()
    //                 .AddUInt8((byte)EpAuthPacketEvent.ErrorTerminate)
    //                 .AddUInt8((byte)ExceptionCode.NotSupported)
    //                 .AddUInt16(13)
    //                 .AddString("Not supported")
    //                 .Done();
    //            }
    //            else
    //            {
    //                Authenticator(iAuthRequest).Then(response =>
    //                {
    //                    SendParams()
    //                        .AddUInt8((byte)EpAuthPacketAction.IAuthPlain)
    //                        .AddUInt32((uint)headers[EpAuthPacketIAuthHeader.Reference])
    //                        .AddUInt8Array(Codec.Compose(response, this.Instance.Warehouse, this))
    //                        .Done();
    //                })
    //                .Timeout(iAuthRequest.Timeout * 1000,
    //                    () =>
    //                    {
    //                        SendParams()
    //                            .AddUInt8((byte)EpAuthPacketEvent.ErrorTerminate)
    //                            .AddUInt8((byte)ExceptionCode.Timeout)
    //                            .AddUInt16(7)
    //                            .AddString("Timeout")
    //                            .Done();
    //                    });
    //            }
    //        }
    //        else if (authPacket.Event == EpAuthPacketEvent.IAuthHashed)
    //        {
    //            var dataType = authPacket.DataType.Value;
    //            var (_, parsed) = Codec.ParseSync(dataType, Instance.Warehouse);
    //            var rt = (Map<byte, object>)parsed;


    //            var headers = rt.Select(x => new KeyValuePair<EpAuthPacketIAuthHeader, object>((EpAuthPacketIAuthHeader)x.Key, x.Value));
    //            var iAuthRequest = new AuthorizationRequest(headers);

    //            if (Authenticator == null)
    //            {
    //                SendParams()
    //                 .AddUInt8((byte)EpAuthPacketEvent.ErrorTerminate)
    //                 .AddUInt8((byte)ExceptionCode.NotSupported)
    //                 .AddUInt16(13)
    //                 .AddString("Not supported")
    //                 .Done();
    //            }
    //            else
    //            {

    //                Authenticator(iAuthRequest).Then(response =>
    //                {
    //                    var sha = SHA256.Create();
    //                    var hash = sha.ComputeHash(new BinaryList()
    //                        .AddUInt8Array((byte[])session.LocalHeaders[EpAuthPacketHeader.Nonce])
    //                        .AddUInt8Array(Codec.Compose(response, this.Instance.Warehouse, this))
    //                        .AddUInt8Array((byte[])session.RemoteHeaders[EpAuthPacketHeader.Nonce])
    //                        .ToArray());

    //                    SendParams()
    //                        .AddUInt8((byte)EpAuthPacketAction.IAuthHashed)
    //                        .AddUInt32((uint)headers[EpAuthPacketIAuthHeader.Reference])
    //                        .AddUInt8((byte)EpAuthPacketHashAlgorithm.SHA256)
    //                        .AddUInt16((ushort)hash.Length)
    //                        .AddUInt8Array(hash)
    //                        .Done();
    //                })
    //                .Timeout(iAuthRequest.Timeout * 1000,
    //                    () =>
    //                    {
    //                        SendParams()
    //                            .AddUInt8((byte)EpAuthPacketEvent.ErrorTerminate)
    //                            .AddUInt8((byte)ExceptionCode.Timeout)
    //                            .AddUInt16(7)
    //                            .AddString("Timeout")
    //                            .Done();
    //                    });
    //            }
    //        }
    //        else if (authPacket.Event == EpAuthPacketEvent.IAuthEncrypted)
    //        {
    //            throw new NotImplementedException("IAuthEncrypted not implemented.");
    //        }
    //    }
    //}

    //private void ProcessHostAuth(byte[] data)
    //{
    //    if (authPacket.Command == EpAuthPacketCommand.Initialize)
    //    {
    //        // Parse headers

    //        var dataType = authPacket.DataType.Value;

    //        var (_, parsed) = Codec.ParseSync(dataType, Server.Instance.Warehouse);

    //        var rt = (Map<byte, object>)parsed;

    //        session.RemoteHeaders = rt.Select(x => new KeyValuePair<EpAuthPacketHeader, object>((EpAuthPacketHeader)x.Key, x.Value));

    //        session.RemoteMethod = authPacket.LocalMethod;


    //        if (authPacket.Initialization == EpAuthPacketInitialize.CredentialsNoAuth)
    //        {
    //            try
    //            {

    //                var username = (string)session.RemoteHeaders[EpAuthPacketHeader.Username];
    //                var domain = (string)session.RemoteHeaders[EpAuthPacketHeader.Domain];
    //                //var remoteNonce = (byte[])session.RemoteHeaders[EpAuthPacketHeader.Nonce];

    //                if (Server.Membership == null)
    //                {
    //                    var errMsg = DC.ToBytes("Membership not set.");

    //                    SendParams()
    //                        .AddUInt8((byte)EpAuthPacketEvent.ErrorTerminate)
    //                        .AddUInt8((byte)ExceptionCode.GeneralFailure)
    //                        .AddUInt16((ushort)errMsg.Length)
    //                        .AddUInt8Array(errMsg)
    //                        .Done();
    //                }
    //                else Server.Membership.UserExists(username, domain).Then(x =>
    //                {
    //                    if (x != null)
    //                    {
    //                        session.AuthorizedAccount = x;

    //                        var localHeaders = session.LocalHeaders.Select(x => new KeyValuePair<byte, object>((byte)x.Key, x.Value));

    //                        SendParams()
    //                                    .AddUInt8((byte)EpAuthPacketAcknowledge.NoAuthCredentials)
    //                                    .AddUInt8Array(Codec.Compose(localHeaders, Server.Instance.Warehouse, this))
    //                                    .Done();
    //                    }
    //                    else
    //                    {
    //                        // Send user not found error
    //                        SendParams()
    //                                    .AddUInt8((byte)EpAuthPacketEvent.ErrorTerminate)
    //                                    .AddUInt8((byte)ExceptionCode.UserOrTokenNotFound)
    //                                    .AddUInt16(14)
    //                                    .AddString("User not found")
    //                                    .Done();
    //                    }
    //                });
    //            }
    //            catch (Exception ex)
    //            {
    //                // Send the server side error
    //                var errMsg = DC.ToBytes(ex.Message);

    //                SendParams()
    //                    .AddUInt8((byte)EpAuthPacketEvent.ErrorTerminate)
    //                    .AddUInt8((byte)ExceptionCode.GeneralFailure)
    //                    .AddUInt16((ushort)errMsg.Length)
    //                    .AddUInt8Array(errMsg)
    //                    .Done();
    //            }
    //        }
    //        else if (authPacket.Initialization == EpAuthPacketInitialize.TokenNoAuth)
    //        {
    //            try
    //            {
    //                if (Server.Membership == null)
    //                {
    //                    SendParams()
    //                                        .AddUInt8((byte)EpAuthPacketEvent.ErrorTerminate)
    //                                        .AddUInt8((byte)ExceptionCode.UserOrTokenNotFound)
    //                                        .AddUInt16(15)
    //                                        .AddString("Token not found")
    //                                        .Done();
    //                }
    //                // Check if user and token exists
    //                else
    //                {
    //                    var tokenIndex = (ulong)session.RemoteHeaders[EpAuthPacketHeader.TokenIndex];
    //                    var domain = (string)session.RemoteHeaders[EpAuthPacketHeader.Domain];
    //                    //var nonce = (byte[])session.RemoteHeaders[EpAuthPacketHeader.Nonce];

    //                    Server.Membership.TokenExists(tokenIndex, domain).Then(x =>
    //                    {
    //                        if (x != null)
    //                        {
    //                            session.AuthorizedAccount = x;

    //                            var localHeaders = session.LocalHeaders.Select(x => new KeyValuePair<byte, object>((byte)x.Key, x.Value));

    //                            SendParams()
    //                                        .AddUInt8((byte)EpAuthPacketAcknowledge.NoAuthToken)
    //                                        .AddUInt8Array(Codec.Compose(localHeaders, this.Instance.Warehouse, this))
    //                                        .Done();

    //                        }
    //                        else
    //                        {
    //                            // Send token not found error.
    //                            SendParams()
    //                                        .AddUInt8((byte)EpAuthPacketEvent.ErrorTerminate)
    //                                        .AddUInt8((byte)ExceptionCode.UserOrTokenNotFound)
    //                                        .AddUInt16(15)
    //                                        .AddString("Token not found")
    //                                        .Done();
    //                        }
    //                    });
    //                }
    //            }
    //            catch (Exception ex)
    //            {
    //                // Sender server side error.

    //                var errMsg = DC.ToBytes(ex.Message);

    //                SendParams()
    //                    .AddUInt8((byte)EpAuthPacketEvent.ErrorTerminate)
    //                    .AddUInt8((byte)ExceptionCode.GeneralFailure)
    //                    .AddUInt16((ushort)errMsg.Length)
    //                    .AddUInt8Array(errMsg)
    //                    .Done();
    //            }
    //        }
    //        else if (authPacket.Initialization == EpAuthPacketInitialize.NoAuthNoAuth)
    //        {
    //            try
    //            {
    //                // Check if guests are allowed
    //                if (Server.Membership?.GuestsAllowed ?? true)
    //                {
    //                    var localHeaders = session.LocalHeaders.Select(x => new KeyValuePair<byte, object>((byte)x.Key, x.Value));

    //                    session.AuthorizedAccount = "g-" + Global.GenerateCode();

    //                    readyToEstablish = true;

    //                    SendParams()
    //                                .AddUInt8((byte)EpAuthPacketAcknowledge.NoAuthNoAuth)
    //                                .AddUInt8Array(Codec.Compose(localHeaders,Server.Instance.Warehouse, this))
    //                                .Done();
    //                }
    //                else
    //                {
    //                    // Send access denied error because the server does not allow guests.
    //                    SendParams()
    //                                .AddUInt8((byte)EpAuthPacketEvent.ErrorTerminate)
    //                                .AddUInt8((byte)ExceptionCode.AccessDenied)
    //                                .AddUInt16(18)
    //                                .AddString("Guests not allowed")
    //                                .Done();
    //                }
    //            }
    //            catch (Exception ex)
    //            {
    //                // Send the server side error.
    //                var errMsg = DC.ToBytes(ex.Message);

    //                SendParams()
    //                    .AddUInt8((byte)EpAuthPacketEvent.ErrorTerminate)
    //                    .AddUInt8((byte)ExceptionCode.GeneralFailure)
    //                    .AddUInt16((ushort)errMsg.Length)
    //                    .AddUInt8Array(errMsg)
    //                    .Done();
    //            }
    //        }

    //    }
    //    else if (authPacket.Command == EpAuthPacketCommand.Action)
    //    {
    //        if (authPacket.Action == EpAuthPacketAction.AuthenticateHash)
    //        {
    //            var remoteHash = authPacket.Challenge;
    //            AsyncReply<byte[]> reply = null;

    //            try
    //            {
    //                if (session.RemoteMethod == AuthenticationMethod.Credentials)
    //                {
    //                    reply = Server.Membership.GetPassword((string)session.RemoteHeaders[EpAuthPacketHeader.Username],
    //                                                  (string)session.RemoteHeaders[EpAuthPacketHeader.Domain]);
    //                }
    //                else if (session.RemoteMethod == AuthenticationMethod.Token)
    //                {
    //                    reply = Server.Membership.GetToken((ulong)session.RemoteHeaders[EpAuthPacketHeader.TokenIndex],
    //                                                  (string)session.RemoteHeaders[EpAuthPacketHeader.Domain]);
    //                }
    //                else
    //                {
    //                    throw new NotImplementedException("Authentication method unsupported.");
    //                }

    //                reply.Then((pw) =>
    //                {
    //                    if (pw != null)
    //                    {
    //                        var localNonce = (byte[])session.LocalHeaders[EpAuthPacketHeader.Nonce];
    //                        var remoteNonce = (byte[])session.RemoteHeaders[EpAuthPacketHeader.Nonce];

    //                        var hashFunc = SHA256.Create();
    //                        var hash = hashFunc.ComputeHash((new BinaryList())
    //                                                            .AddUInt8Array(remoteNonce)
    //                                                            .AddUInt8Array(pw)
    //                                                            .AddUInt8Array(localNonce)
    //                                                            .ToArray());

    //                        if (hash.SequenceEqual(remoteHash))
    //                        {
    //                            // send our hash
    //                            var localHash = hashFunc.ComputeHash((new BinaryList())
    //                                                .AddUInt8Array(localNonce)
    //                                                .AddUInt8Array(pw)
    //                                                .AddUInt8Array(remoteNonce)
    //                                                .ToArray());

    //                            SendParams()
    //                                .AddUInt8((byte)EpAuthPacketAction.AuthenticateHash)
    //                                .AddUInt8((byte)EpAuthPacketHashAlgorithm.SHA256)
    //                                .AddUInt16((ushort)localHash.Length)
    //                                .AddUInt8Array(localHash)
    //                                .Done();

    //                            readyToEstablish = true;
    //                        }
    //                        else
    //                        {
    //                            //Global.Log("auth", LogType.Warning, "U:" + RemoteUsername + " IP:" + Socket.RemoteEndPoint.Address.ToString() + " S:DENIED");
    //                            SendParams()
    //                                .AddUInt8((byte)EpAuthPacketEvent.ErrorTerminate)
    //                                .AddUInt8((byte)ExceptionCode.AccessDenied)
    //                                .AddUInt16(13)
    //                                .AddString("Access Denied")
    //                                .Done();
    //                        }
    //                    }
    //                });
    //            }
    //            catch (Exception ex)
    //            {
    //                var errMsg = DC.ToBytes(ex.Message);

    //                SendParams()
    //                    .AddUInt8((byte)EpAuthPacketEvent.ErrorTerminate)
    //                    .AddUInt8((byte)ExceptionCode.GeneralFailure)
    //                    .AddUInt16((ushort)errMsg.Length)
    //                    .AddUInt8Array(errMsg)
    //                    .Done();
    //            }
    //        }
    //        else if (authPacket.Action == EpAuthPacketAction.IAuthPlain)
    //        {
    //            var reference = authPacket.Reference;
    //            var dataType = authPacket.DataType.Value;

    //            var (_, value) = Codec.ParseSync(dataType, Instance.Warehouse);

    //            Server.Membership.AuthorizePlain(session, reference, value)
    //                .Then(x => ProcessAuthorization(x));


    //        }
    //        else if (authPacket.Action == EpAuthPacketAction.IAuthHashed)
    //        {
    //            var reference = authPacket.Reference;
    //            var value = authPacket.Challenge;
    //            var algorithm = authPacket.HashAlgorithm;

    //            Server.Membership.AuthorizeHashed(session, reference, algorithm, value)
    //                .Then(x => ProcessAuthorization(x));

    //        }
    //        else if (authPacket.Action == EpAuthPacketAction.IAuthEncrypted)
    //        {
    //            var reference = authPacket.Reference;
    //            var value = authPacket.Challenge;
    //            var algorithm = authPacket.PublicKeyAlgorithm;

    //            Server.Membership.AuthorizeEncrypted(session, reference, algorithm, value)
    //                .Then(x => ProcessAuthorization(x));
    //        }
    //        else if (authPacket.Action == EpAuthPacketAction.EstablishNewSession)
    //        {
    //            if (readyToEstablish)
    //            {

    //                if (Server.Membership == null)
    //                {
    //                    ProcessAuthorization(null);
    //                }
    //                else
    //                {
    //                    Server.Membership?.Authorize(session).Then(x =>
    //                    {
    //                        ProcessAuthorization(x);
    //                    });
    //                }

    //                //Global.Log("auth", LogType.Warning, "U:" + RemoteUsername + " IP:" + Socket.RemoteEndPoint.Address.ToString() + " S:AUTH");

    //            }
    //            else
    //            {
    //                SendParams()
    //                    .AddUInt8((byte)EpAuthPacketEvent.ErrorTerminate)
    //                    .AddUInt8((byte)ExceptionCode.GeneralFailure)
    //                    .AddUInt16(9)
    //                    .AddString("Not ready")
    //                    .Done();
    //            }
    //        }
    //    }
    //}

    //internal void ProcessAuthorization(AuthorizationResults results)
    //{
    //    if (results == null || results.Response == Security.Membership.AuthorizationResultsResponse.Success)
    //    {
    //        var r = new Random();
    //        session.Id = new byte[32];
    //        r.NextBytes(session.Id);
    //        var accountId = session.AuthorizedAccount.ToBytes();

    //        SendParams()
    //            .AddUInt8((byte)EpAuthPacketEvent.IndicationEstablished)
    //            .AddUInt8((byte)session.Id.Length)
    //            .AddUInt8Array(session.Id)
    //            .AddUInt8((byte)accountId.Length)
    //            .AddUInt8Array(accountId)
    //            .Done();

    //        if (this.Instance == null)
    //        {
    //            Server.Instance.Warehouse.Put(
    //                Server.Instance.Link + "/" + this.GetHashCode().ToString().Replace("/", "_"), this)
    //                .Then(x =>
    //            {
    //                ready = true;
    //                Status = EpConnectionStatus.Connected;
    //                openReply?.Trigger(true);
    //                openReply = null;
    //                OnReady?.Invoke(this);

    //                Server?.Membership?.Login(session);
    //                LoginDate = DateTime.Now;

    //            }).Error(x =>
    //            {
    //                openReply?.TriggerError(x);
    //                openReply = null;

    //            });
    //        }
    //        else
    //        {
    //            ready = true;
    //            Status = EpConnectionStatus.Connected;

    //            openReply?.Trigger(true);
    //            openReply = null;

    //            OnReady?.Invoke(this);
    //            Server?.Membership?.Login(session);
    //        }
    //    }
    //    else if (results.Response == Security.Membership.AuthorizationResultsResponse.Failed)
    //    {
    //        SendParams()
    //            .AddUInt8((byte)EpAuthPacketEvent.ErrorTerminate)
    //            .AddUInt8((byte)ExceptionCode.ChallengeFailed)
    //            .AddUInt16(21)
    //            .AddString("Authentication failed")
    //            .Done();
    //    }
    //    else if (results.Response == Security.Membership.AuthorizationResultsResponse.Expired)
    //    {
    //        SendParams()
    //            .AddUInt8((byte)EpAuthPacketEvent.ErrorTerminate)
    //            .AddUInt8((byte)ExceptionCode.Timeout)
    //            .AddUInt16(22)
    //            .AddString("Authentication expired")
    //            .Done();
    //    }
    //    else if (results.Response == Security.Membership.AuthorizationResultsResponse.ServiceUnavailable)
    //    {
    //        SendParams()
    //            .AddUInt8((byte)EpAuthPacketEvent.ErrorTerminate)
    //            .AddUInt8((byte)ExceptionCode.GeneralFailure)
    //            .AddUInt16(19)
    //            .AddString("Service unavailable")
    //            .Done();
    //    }
    //    else if (results.Response == Security.Membership.AuthorizationResultsResponse.IAuthPlain)
    //    {
    //        var args = new Map<EpAuthPacketIAuthHeader, object>()
    //        {
    //            [EpAuthPacketIAuthHeader.Reference] = results.Reference,
    //            [EpAuthPacketIAuthHeader.Destination] = results.Destination,
    //            [EpAuthPacketIAuthHeader.Trials] = results.Trials,
    //            [EpAuthPacketIAuthHeader.Clue] = results.Clue,
    //            [EpAuthPacketIAuthHeader.RequiredFormat] = results.RequiredFormat,
    //        }.Select(m => new KeyValuePair<byte, object>((byte)m.Key, m.Value));

    //        SendParams()
    //            .AddUInt8((byte)EpAuthPacketEvent.IAuthPlain)
    //            .AddUInt8Array(Codec.Compose(args, this.Instance.Warehouse, this))
    //            .Done();

    //    }
    //    else if (results.Response == Security.Membership.AuthorizationResultsResponse.IAuthHashed)
    //    {
    //        var args = new Map<EpAuthPacketIAuthHeader, object>()
    //        {
    //            [EpAuthPacketIAuthHeader.Reference] = results.Reference,
    //            [EpAuthPacketIAuthHeader.Destination] = results.Destination,
    //            [EpAuthPacketIAuthHeader.Expire] = results.Expire,
    //            //[EpAuthPacketIAuthHeader.Issue] = results.Issue,
    //            [EpAuthPacketIAuthHeader.Clue] = results.Clue,
    //            [EpAuthPacketIAuthHeader.RequiredFormat] = results.RequiredFormat,
    //        }.Select(m => new KeyValuePair<byte, object>((byte)m.Key, m.Value));

    //        SendParams()
    //            .AddUInt8((byte)EpAuthPacketEvent.IAuthHashed)
    //            .AddUInt8Array(Codec.Compose(args, Server.Instance.Warehouse, this))
    //            .Done();

    //    }
    //    else if (results.Response == Security.Membership.AuthorizationResultsResponse.IAuthEncrypted)
    //    {
    //        var args = new Map<EpAuthPacketIAuthHeader, object>()
    //        {
    //            [EpAuthPacketIAuthHeader.Destination] = results.Destination,
    //            [EpAuthPacketIAuthHeader.Expire] = results.Expire,
    //            [EpAuthPacketIAuthHeader.Clue] = results.Clue,
    //            [EpAuthPacketIAuthHeader.RequiredFormat] = results.RequiredFormat,
    //        }.Select(m => new KeyValuePair<byte, object>((byte)m.Key, m.Value));

    //        SendParams()
    //            .AddUInt8((byte)EpAuthPacketEvent.IAuthEncrypted)
    //            .AddUInt8Array(Codec.Compose(args, this.Instance.Warehouse, this))
    //            .Done();
    //    }
    //}

    protected override void DataReceived(NetworkBuffer data)
    {
        var msg = data.Read();
        if (msg == null)
            return;

        this.Socket.Hold();

        try
        {
            if (_decryptInbound)
                ProcessEncryptedRecords(msg, data);
            else
                ProcessPlainPackets(msg, data);
        }
        catch (ParserLimitException ex)
        {
            _invalidCredentials = true;
            Global.Log("EpConnection:ParserLimit", LogType.Warning, ex.Message);
            FailPendingOpen(new AsyncException(
                ErrorType.Management,
                0,
                "Session establishment exceeded a configured parser limit."));
            Close();
        }
        catch (CryptographicException ex)
        {
            _invalidCredentials = true;
            Global.Log("EpConnection:Encryption", LogType.Warning, ex.Message);
            FailPendingOpen(new AsyncException(
                ErrorType.Management,
                0,
                "Encrypted session validation failed."));
            Close();
        }
        catch (InvalidDataException ex)
        {
            _invalidCredentials = true;
            Global.Log("EpConnection:Encryption", LogType.Warning, ex.Message);
            FailPendingOpen(new AsyncException(
                ErrorType.Management,
                0,
                "Encrypted session framing is invalid."));
            Close();
        }
        catch (Exception ex)
        {
            Global.Log(ex);
            if (_decryptInbound)
            {
                _invalidCredentials = true;
                FailPendingOpen(new AsyncException(
                    ErrorType.Management,
                    0,
                    "Encrypted session processing failed."));
                Close();
            }
        }
        finally
        {
            this.Socket?.Unhold();
        }
    }

    void ProcessPlainPackets(byte[] msg, NetworkBuffer holdingBuffer)
    {
        uint offset = 0;
        var ends = (uint)msg.Length;
        var chunkId = (new Random()).Next(1000, 1000000);

        while (offset < ends)
        {
            var encryptionWasEnabled = _decryptInbound;
            offset = processPacket(msg, offset, ends, holdingBuffer, chunkId);

            // A handshake packet may switch the remainder of the same socket read to
            // encrypted records. Preserve that boundary even when TCP coalesces writes.
            if (!encryptionWasEnabled && _decryptInbound && offset < ends)
            {
                var remaining = new byte[ends - offset];
                Buffer.BlockCopy(msg, (int)offset, remaining, 0, remaining.Length);
                ProcessEncryptedRecords(remaining, holdingBuffer);
                return;
            }
        }
    }

    void ProcessEncryptedRecords(byte[] data, NetworkBuffer holdingBuffer)
    {
        uint offset = 0;
        var ends = (uint)data.Length;

        while (offset < ends)
        {
            var remaining = ends - offset;
            if (remaining < EncryptedRecordHeaderSize)
            {
                holdingBuffer.HoldFor(data, offset, remaining, EncryptedRecordHeaderSize);
                return;
            }

            var protectedLength = BinaryPrimitives.ReadUInt32BigEndian(
                data.AsSpan((int)offset, EncryptedRecordHeaderSize));
            var maximumRecordSize = ParsingWarehouse.Configuration.Encryption.MaximumRecordSize;

            if (maximumRecordSize > 0 && protectedLength > maximumRecordSize)
                throw new ParserLimitException(
                    $"Encrypted record of {protectedLength} bytes exceeds the {maximumRecordSize}-byte limit.");
            if (protectedLength > int.MaxValue)
                throw new ParserLimitException("Encrypted record exceeds the runtime allocation limit.");
            if (protectedLength > uint.MaxValue - EncryptedRecordHeaderSize)
                throw new InvalidDataException("Encrypted record length is invalid.");

            var totalLength = protectedLength + EncryptedRecordHeaderSize;
            if (remaining < totalLength)
            {
                holdingBuffer.HoldFor(data, offset, remaining, totalLength);
                return;
            }

            var protectedPayload = new byte[(int)protectedLength];
            Buffer.BlockCopy(data,
                             (int)offset + EncryptedRecordHeaderSize,
                             protectedPayload,
                             0,
                             protectedPayload.Length);

            var cipher = _session?.SymetricCipher
                ?? throw new InvalidDataException("Encrypted data arrived before cipher initialization.");
            var plaintext = cipher.Decrypt(protectedPayload);
            _decryptedReceiveBuffer.Write(plaintext);

            while (_decryptedReceiveBuffer.Available > 0 && !_decryptedReceiveBuffer.Protected)
            {
                var plainPacket = _decryptedReceiveBuffer.Read();
                if (plainPacket != null)
                    ProcessPlainPackets(plainPacket, _decryptedReceiveBuffer);
            }

            offset += totalLength;
        }
    }

    /// <summary>
    /// Resource interface
    /// </summary>
    /// <param name="trigger">Resource trigger.</param>
    /// <returns></returns>
    public AsyncReply<bool> Handle(ResourceOperation trigger, IResourceContext context = null)
    {
        if (trigger == ResourceOperation.Configure)
        {
            if (context is EpServerConnectionContext serverContext)
            {
                _server = serverContext.Server;
                _serverWarehouse = serverContext.Warehouse;
                _authPacket = new EpAuthPacket(_serverWarehouse);
                _packet = new EpPacket(_serverWarehouse);
            }
        }
        else if (trigger == ResourceOperation.Initialize)
        {
            if (_authPacket == null)
                _authPacket = new EpAuthPacket(Instance.Warehouse);
            if (_packet == null)
                _packet = new EpPacket(Instance.Warehouse);
        }
        else if (trigger == ResourceOperation.Open)
        {
            // @TODO: Need a better way to check for initiator or responder
            if (this.Server != null)
                return new AsyncReply<bool>(true);


            var host = Instance.Name.Split(':');

            var address = host[0];
            var port = host.Length > 1 ? ushort.Parse(host[1]) : (ushort)10518;

            // assign domain from hostname if not provided
            if (context is EpConnectionContext epContext)
            {
                var provider = Instance.Warehouse.TryGetAuthenticationProvider(epContext.AuthenticationProtocol);

                _remoteDomain = epContext.Domain ?? address;

                if (provider != null)
                {
                    _session.AuthenticationHandler = provider.CreateAuthenticationHandler(new AuthenticationContext()
                    {
                        Direction = AuthenticationDirection.Initiator,
                        Domain = _remoteDomain,
                        HostName = address,
                        InitiatorIdentity = epContext.Identity,
                        Mode = epContext.AuthenticationMode,
                    });
                }

                _session.AuthenticationMode = epContext.AuthenticationMode;
                _session.EncryptionMode = epContext.EncryptionMode;
                _offeredEncryptionProviders = epContext.EncryptionProviders ?? Array.Empty<string>();
                _session.LocalIdentity = epContext.Identity;
                ReconnectInterval = epContext.ReconnectInterval;
                ExceptionLevel = epContext.ExceptionLevel;
                UseWebSocket = epContext.UseWebSocket;
                SecureWebSocket = epContext.SecureWebSocket;
                _remoteDomain = epContext.Domain;
                AutoReconnect = epContext.AutoReconnect;
                _hostname = address;
                _port = port;

                return Connect();

            }
            else if (_remoteDomain == null)
                _remoteDomain = address;

            return Connect(null, address, port, _remoteDomain);
        }

        return new AsyncReply<bool>(true);
    }




    public AsyncReply<bool> Connect(ISocket socket = null, string hostname = null, ushort port = 0, string domain = null)
    {
        if (IsConnected || Status == EpConnectionStatus.Connected)
            throw new AsyncException(ErrorType.Exception, 0, "Connection is already established");
        var openReply = new AsyncReply<bool>();
        if (Interlocked.CompareExchange(ref _openReply, openReply, null) != null)
            throw new AsyncException(ErrorType.Exception, 0, "Connection in progress");

        Status = EpConnectionStatus.Connecting;

        // set auth direction to initiator
        _authDirection = AuthenticationDirection.Initiator;

        if (hostname != null)
        {
            DisposeSessionEncryption();
            _session = new Session();
            _authDirection = AuthenticationDirection.Initiator;
            _invalidCredentials = false;

            _session.LocalHeaders.Domain = domain;
            _hostname = hostname;
        }

        if (port > 0)
            this._port = port;

        if (_session == null)
            throw new AsyncException(ErrorType.Exception, 0, "Session not initialized");

        BeginPlaintextHandshake();

        if (socket == null)
        {
            var os = RuntimeInformation.FrameworkDescription;
            if (UseWebSocket || RuntimeInformation.OSDescription == "Browser")
                socket = new FrameworkWebSocket();
            else
                socket = new TcpSocket();
        }


        connectSocket(socket);

        return openReply;
    }

    void connectSocket(ISocket socket)
    {
        socket.Connect(this._hostname, this._port).Then(x =>
        {
            Assign(socket);
        }).Error((x) =>
        {
            if (AutoReconnect)
            {
                Global.Log("EpConnection", LogType.Debug, "Reconnecting socket...");
                Task.Delay((int)ReconnectInterval).ContinueWith((x) => connectSocket(socket));
            }
            else
            {
                FailPendingOpen(x);
            }
        });

    }

    public async AsyncReply<bool> Reconnect()
    {
        try
        {
            if (!await Connect())
                return false;

            try
            {

                var toBeRestored = new List<EpResource>();
                foreach (KeyValuePair<uint, WeakReference<EpResource>> kv in _suspendedResources)
                {
                    EpResource r;
                    if (kv.Value.TryGetTarget(out r))
                        toBeRestored.Add(r);
                }

                foreach (var r in toBeRestored)
                {

                    var link = DC.ToBytes(r.ResourceLink);

                    Global.Log("EpConnection", LogType.Debug, "Restoreing " + r.ResourceLink);

                    try
                    {
                        var id = (uint)await SendRequest(EpPacketRequest.GetResourceIdByLink, link);


                        // remove from suspended.
                        _suspendedResources.Remove(r.ResourceInstanceId);

                        // id changed ?
                        if (id != r.ResourceInstanceId)
                            r.ResourceInstanceId = id;

                        _neededResources[id] = r;

                        // Reattach using the last-known age so only properties modified while
                        // disconnected are transferred and merged, instead of re-fetching all.
                        await Reattach(id, r.Instance.Age, r);

                        Global.Log("EpConnection", LogType.Debug, "Restored " + id);

                    }
                    catch (AsyncException ex)
                    {
                        if (ex.Code == ExceptionCode.ResourceNotFound)
                        {
                            // skip this resource
                        }
                        else
                        {
                            Global.Log(ex);
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Log(ex);
            }
        }
        catch
        {
            return false;
        }

        OnResumed?.Invoke(this);

        return true;
    }


    /// <summary>
    /// Store interface.
    /// </summary>
    /// <param name="resource">Resource.</param>
    /// <returns></returns>
    public AsyncReply<bool> Put(IResource resource, string path)
    {
        if (Codec.IsLocalResource(resource, this))
            _neededResources.Add((resource as EpResource).ResourceInstanceId, (EpResource)resource);
        // else ... send it to the peer
        return new AsyncReply<bool>(true);
    }

    public bool Record(IResource resource, string propertyName, object value, ulong? age, DateTime? dateTime)
    {
        // nothing to do
        return true;
    }

    public bool Modify(IResource resource, PropertyDef propertyDef, object value, ulong? age, DateTime? dateTime)
    {
        // nothing to do
        return true;
    }

    public AsyncBag<T> Children<T>(IResource resource, string name) where T : IResource
    {
        throw new Exception("Not implemented");
    }



    protected override void Connected()
    {
        if (_authDirection == AuthenticationDirection.Initiator)
            Declare();
    }

    protected override void Disconnected()
    {
        // clean up
        var wasAuthenticated = _authenticated || (_session?.Authenticated ?? false);
        TerminateInvocations();
        DisposeSessionEncryption();
        _authenticated = false;
        if (_session != null)
            _session.Authenticated = false;
        Status = EpConnectionStatus.Closed;
        FailPendingOpen(new AsyncException(
            ErrorType.Management,
            0,
            "Connection closed before session establishment completed."));

        _keepAliveTimer.Stop();

        // @TODO: lock requests

        foreach (var x in _requests.Values)
        {
            try
            {
                x.TriggerError(new AsyncException(ErrorType.Management, 0, "Connection closed"));
            }
            catch (Exception ex)
            {
                Global.Log(ex);
            }
        }

        foreach (var x in _resourceRequests.Values)
        {
            try
            {
                x.Reply.TriggerError(new AsyncException(ErrorType.Management, 0, "Connection closed"));
            }
            catch (Exception ex)
            {
                Global.Log(ex);
            }
        }


        foreach (var x in _typeDefRequests.Values)
        {
            try
            {
                x.Reply.TriggerError(new AsyncException(ErrorType.Management, 0, "Connection closed"));
            }
            catch (Exception ex)
            {
                Global.Log(ex);
            }
        }


        _requests.Clear();
        _resourceRequests.Clear();
        _typeDefRequests.Clear();
        _resourcesFetchBlockedOn.Clear();
        _typeDefsFetchBlockedOn.Clear();

        foreach (var x in _attachedResources.Values)
        {
            EpResource r;
            if (x.TryGetTarget(out r))
            {
                r.Suspend();
                _suspendedResources[r.ResourceInstanceId] = x;
            }
        }

        if (Server != null)
        {
            _suspendedResources.Clear();

            UnsubscribeAll();
            Instance?.Warehouse?.Remove(this);

            if (wasAuthenticated)
            {
                _session.AuthenticationHandler?.Provider.Logout(_session);
                //Server.Membership?.Logout(_session);
            }

        }
        else if (AutoReconnect && !_invalidCredentials)
        {
            // reconnect
            Task.Delay((int)ReconnectInterval).ContinueWith((x) => Reconnect());
        }
        else
        {
            _suspendedResources.Clear();
        }


        _attachedResources.Clear();

    }

    public AsyncBag<T> Parents<T>(IResource resource, string name) where T : IResource
    {
        throw new NotImplementedException();
    }

    public AsyncReply<KeyList<PropertyDef, PropertyValue[]>> GetRecord(IResource resource, DateTime fromDate, DateTime toDate)
    {
        throw new NotImplementedException();
    }

    AsyncReply<bool> IStore.Remove(IResource resource)
    {
        // @TODO: this is called when no connection is possible
        return new AsyncReply<bool>(true);
    }

    public AsyncReply<bool> Remove(string path)
    {
        throw new NotImplementedException();
    }

    public AsyncReply<bool> Move(IResource resource, string newPath)
    {
        throw new NotImplementedException();
    }


}
