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
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
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
    bool invalidCredentials = false;

    System.Timers.Timer keepAliveTimer;
    DateTime? lastKeepAliveSent;
    DateTime? lastKeepAliveReceived;


    EpPacket packet = new EpPacket();
    EpAuthPacket authPacket = new EpAuthPacket();


    Session session;

    AsyncReply<bool> openReply;


    //byte[] localPasswordOrToken;
    bool authenticated, readyToEstablish;

    string _hostname;
    ushort _port;

    bool initialPacket = true;
    bool isInitiator = false;


    // Properties

    public DateTime LoginDate { get; private set; }

    /// <summary>
    /// Distributed server responsible for this connection, usually for incoming connections.
    /// </summary>
    public EpServer Server { get; internal set; }


    /// <summary>
    /// The session related to this connection.
    /// </summary>
    public Session Session => session;

    [Export]
    public virtual EpConnectionStatus Status { get; private set; }

    [Export]
    public virtual uint Jitter { get; private set; }

    // Attributes

    [Attribute]
    public uint KeepAliveTime { get; set; } = 10;

    [Attribute]
    public ExceptionLevel ExceptionLevel { get; set; }
                = ExceptionLevel.Code | ExceptionLevel.Message | ExceptionLevel.Source | ExceptionLevel.Trace;

    //[Attribute]
    //public Func<AuthorizationRequest, AsyncReply<object>> Authenticator { get; set; }


    [Attribute]
    public bool AutoReconnect { get; set; } = false;

    [Attribute]
    public uint ReconnectInterval { get; set; } = 5;

    //[Attribute]
    //public string Username { get; set; }

    [Attribute]
    public bool UseWebSocket { get; set; }

    [Attribute]
    public bool SecureWebSocket { get; set; }

    //[Attribute]
    //public string Password { get; set; }

    //[Attribute]
    //public string Token { get; set; }

    //[Attribute]
    //public ulong TokenIndex { get; set; }

    [Attribute]
    public string Domain { get; set; }

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
        //Console.WriteLine("Client: {0}", Data.Length);

        Global.Counters["Ep Sent Packets"]++;
        base.Send(data);
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

        session.LocalHeaders[EpAuthPacketHeader.IPAddress] 
                                = socket.RemoteEndPoint.Address.GetAddressBytes();

        if (socket.State == SocketState.Established &&
            isInitiator)
        {
            Declare();
        }
    }

    private void Declare()
    {
        //if (session.KeyExchanger != null)
        //{
        //    // create key
        //    var key = session.KeyExchanger.GetPublicKey();
        //    session.LocalHeaders[EpAuthPacketHeader.CipherKey] = key;
        //}


        if (!isInitiator)
            return;

        if (session.AuthenticationMode != AuthenticationMode.None)
        {
            if (session.AuthenticationHandler == null)
                throw new Exception("Authentication handler must be assigned for the session.");
            
            var initAuthData = session.AuthenticationHandler.Initialize(session, null);

            session.LocalHeaders.Add(EpAuthPacketHeader.AuthenticationData, initAuthData);
        }

        if (session.EncryptionMode != EncryptionMode.None)
        {
            // get the handler
        }

        // change to Map<byte, object> for compatibility
        var headers = Codec.Compose(session.LocalHeaders.Select(x => new KeyValuePair<byte, object>((byte)x.Key, x.Value)), this.Instance.Warehouse, this);

        SendParams()
            .AddUInt8((byte)(0x20 | ((byte)session.AuthenticationMode << 2)
            | (byte)session.EncryptionMode))
            .AddUInt8Array(headers)
            .Done();
    }

    /// <summary>
    /// Create a new distributed connection. 
    /// </summary>
    /// <param name="socket">Socket to transfer data through.</param>
    /// <param name="domain">Working domain.</param>
    /// <param name="username">Username.</param>
    /// <param name="password">Password.</param>
    public EpConnection(ISocket socket, IAuthenticationHandler authenticationHandler, Map<EpAuthPacketHeader, object> headers)
    {
        this.session = new Session();

        //if (authenticationHandler.Type != AuthenticationType.Initiator)
        //    throw new Exception(""
        //session.AuthenticationType = AuthenticationMode.Initiator;
        session.LocalHeaders = headers;

        if (authenticationHandler != null)
        {
            session.AuthenticationHandler = authenticationHandler;
            session.AuthenticationMode = authenticationHandler.Mode;
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
        session = new Session();
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
                return this.Instance.Name + "/" + r.DistributedResourceInstanceId;
        }

        return null;
    }


    public List<AsyncQueueItem<EpResourceQueueItem>> GetFinishedQueue()
    {
        var l = queue.Processed.ToArray().ToList();
        queue.Processed.Clear();
        return l;
    }

    void init()
    {
        //var q = queue;
        queue.Then((x) =>
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

        keepAliveTimer = new System.Timers.Timer(KeepAliveInterval * 1000);
        keepAliveTimer.Elapsed += KeepAliveTimer_Elapsed; ;
    }

    private void KeepAliveTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        if (!IsConnected)
            return;


        keepAliveTimer.Stop();

        var now = DateTime.UtcNow;

        uint interval = lastKeepAliveSent == null ? 0 :
                        (uint)(now - (DateTime)lastKeepAliveSent).TotalMilliseconds;

        lastKeepAliveSent = now;

        SendRequest(EpPacketRequest.KeepAlive, now, interval)
                .Then(x =>
                {
                    Jitter = Convert.ToUInt32(((object[])x)[1]);
                    keepAliveTimer.Start();
                }).Error(ex =>
                {
                    keepAliveTimer.Stop();
                    Close();
                }).Timeout((int)(KeepAliveTime * 1000), () =>
                {
                    keepAliveTimer.Stop();
                    Close();
                });

    }

    public uint KeepAliveInterval { get; set; } = 30;

    public override void Destroy()
    {
        UnsubscribeAll();
        this.OnReady = null;
        this.OnError = null;
        base.Destroy();
    }


    private uint processPacket(byte[] msg, uint offset, uint ends, NetworkBuffer data, int chunkId)
    {
        if (authenticated)
        {
            var rt = packet.Parse(msg, offset, ends);

            if (rt <= 0)
            {
                var size = ends - offset;
                data.HoldFor(msg, offset, size, size + (uint)(-rt));
                return ends;
            }
            else
            {

                offset += (uint)rt;

                if (packet.Tdu == null)
                    return offset;

                //Console.WriteLine("Incoming: " +  packet + " " + packet.CallbackId);

                if (packet.Method == EpPacketMethod.Notification)
                {

                    var dt = packet.Tdu.Value;

                    switch (packet.Notification)
                    {
                        // Invoke
                        case EpPacketNotification.PropertyModified:
                            EpNotificationPropertyModified(dt);
                            break;
                        case EpPacketNotification.EventOccurred:
                            EpNotificationEventOccurred(dt, msg);
                            break;
                        // Manage
                        case EpPacketNotification.ResourceDestroyed:
                            EpNotificationResourceDestroyed(dt, msg);
                            break;
                        case EpPacketNotification.ResourceReassigned:
                            EpNotificationResourceReassigned(dt);
                            break;
                        case EpPacketNotification.ResourceMoved:
                            EpNotificationResourceMoved(dt, msg);
                            break;
                        case EpPacketNotification.SystemFailure:
                            EpNotificationSystemFailure(dt, msg);
                            break;
                    }
                }
                else if (packet.Method == EpPacketMethod.Request)
                {
                    var dt = packet.Tdu.Value;

                    switch (packet.Request)
                    {
                        // Invoke
                        case EpPacketRequest.InvokeFunction:
                            EpRequestInvokeFunction(packet.CallbackId, dt, msg);
                            break;
                        case EpPacketRequest.SetProperty:
                            EpRequestSetProperty(packet.CallbackId, dt, msg);
                            break;
                        case EpPacketRequest.Subscribe:
                            EpRequestSubscribe(packet.CallbackId, dt, msg);
                            break;
                        case EpPacketRequest.Unsubscribe:
                            EpRequestUnsubscribe(packet.CallbackId, dt, msg);
                            break;
                        // Inquire
                        case EpPacketRequest.TypeDefByName:
                            EpRequestTypeDefByName(packet.CallbackId, dt, msg);
                            break;
                        case EpPacketRequest.TypeDefById:
                            EpRequestTypeDefById(packet.CallbackId, dt, msg);
                            break;
                        case EpPacketRequest.TypeDefByResourceId:
                            EpRequestTypeDefByResourceId(packet.CallbackId, dt, msg);
                            break;
                        case EpPacketRequest.Query:
                            EpRequestQueryResources(packet.CallbackId, dt, msg);
                            break;
                        case EpPacketRequest.LinkTypeDefs:
                            EpRequestLinkTypeDefs(packet.CallbackId, dt, msg);
                            break;
                        case EpPacketRequest.Token:
                            EpRequestToken(packet.CallbackId, dt, msg);
                            break;
                        case EpPacketRequest.GetResourceIdByLink:
                            EpRequestGetResourceIdByLink(packet.CallbackId, dt, msg);
                            break;
                        // Manage
                        case EpPacketRequest.AttachResource:
                            EpRequestAttachResource(packet.CallbackId, dt, msg);
                            break;
                        case EpPacketRequest.ReattachResource:
                            EpRequestReattachResource(packet.CallbackId, dt, msg);
                            break;
                        case EpPacketRequest.DetachResource:
                            EpRequestDetachResource(packet.CallbackId, dt, msg);
                            break;
                        case EpPacketRequest.CreateResource:
                            EpRequestCreateResource(packet.CallbackId, dt, msg);
                            break;
                        case EpPacketRequest.DeleteResource:
                            EpRequestDeleteResource(packet.CallbackId, dt, msg);
                            break;
                        case EpPacketRequest.MoveResource:
                            EpRequestMoveResource(packet.CallbackId, dt, msg);
                            break;
                        // Static
                        case EpPacketRequest.KeepAlive:
                            EpRequestKeepAlive(packet.CallbackId, dt, msg);
                            break;
                        case EpPacketRequest.ProcedureCall:
                            EpRequestProcedureCall(packet.CallbackId, dt, msg);
                            break;
                        case EpPacketRequest.StaticCall:
                            EpRequestStaticCall(packet.CallbackId, dt, msg);
                            break;
                    }
                }
                else if (packet.Method == EpPacketMethod.Reply)
                {
                    var dt = packet.Tdu.Value;

                    switch (packet.Reply)
                    {
                        case EpPacketReply.Completed:
                            EpReplyCompleted(packet.CallbackId, dt);
                            break;
                        case EpPacketReply.Propagated:
                            EpReplyPropagated(packet.CallbackId, dt, msg);
                            break;
                        case EpPacketReply.PermissionError:
                            EpReplyError(packet.CallbackId, dt, msg, ErrorType.Management);
                            break;
                        case EpPacketReply.ExecutionError:
                            EpReplyError(packet.CallbackId, dt, msg, ErrorType.Exception);
                            break;

                        case EpPacketReply.Progress:
                            EpReplyProgress(packet.CallbackId, dt, msg);
                            break;

                        case EpPacketReply.Chunk:
                            EpReplyChunk(packet.CallbackId, dt);
                            break;

                        case EpPacketReply.Warning:
                            EpReplyWarning(packet.Extension, dt, msg);
                            break;

                    }
                }
                else if (packet.Method == EpPacketMethod.Extension)
                {
                    EpExtensionAction(packet.Extension, packet.Tdu, msg);
                }
            }
        }
        else
        {
            // check if the request through Websockets
            if (initialPacket)
            {
                initialPacket = false;

                if (msg.Length > 3 && Encoding.Default.GetString(msg, 0, 3) == "GET")
                {
                    // Parse with http packet
                    var req = new HttpRequestPacket();
                    var pSize = req.Parse(msg, 0, (uint)msg.Length);
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
                    else
                    {
                        // packet incomplete
                        return (uint)pSize;
                    }

                    // switching completed
                    return (uint)msg.Length;
                }
            }

            var rt = authPacket.Parse(msg, offset, ends);

            if (rt <= 0)
            {
                data.HoldFor(msg, ends + (uint)(-rt));
                return ends;
            }
            else
            {
                offset += (uint)rt;

                if (authPacket.Command == EpAuthPacketCommand.Initialize && isInitiator)
                    throw new Exception("Bad authentication packet received. Connection is initiator but received an initialization packet.");

                if (authPacket.Command == EpAuthPacketCommand.Acknowledge && !isInitiator)
                    throw new Exception("Bad authentication packet received. Connection is responder but received an acknowledge packet.");

                if (authPacket.Command == EpAuthPacketCommand.Initialize)
                {
                    if (authPacket.Tdu != null)
                    {
                        var (_, parsed) = Codec.ParseSync(authPacket.Tdu.Value, Instance.Warehouse);

                        if (parsed is Map<byte, object> headers)
                        {
                            session.RemoteHeaders = headers.Select(x => new KeyValuePair<EpAuthPacketHeader, object>((EpAuthPacketHeader)x.Key, x.Value));
                        }
                    }




                    //@TODO: get the authentication handler
                    if (session.RemoteHeaders.ContainsKey(EpAuthPacketHeader.AuthenticationData))
                    {
                        var authResult = session.AuthenticationHandler.Initialize(session, session.RemoteHeaders[EpAuthPacketHeader.AuthenticationData]);
                    }

                    //@TODO allow all for testing
                    SendParams()
                            .AddUInt8((byte)EpAuthPacketAcknowledgement.SessionEstablished)
                            .Done();

                }
                else if (authPacket.Command == EpAuthPacketCommand.Acknowledge)
                {
                    //@TODO: get the authentication handler

                    if (authPacket.Tdu != null)
                    {
                        var (_, parsed) = Codec.ParseSync(authPacket.Tdu.Value, Instance.Warehouse);

                        if (parsed is Map<byte, object> headers)
                        {
                            session.RemoteHeaders = headers.Select(x => new KeyValuePair<EpAuthPacketHeader, object>((EpAuthPacketHeader)x.Key, x.Value));
                        }
                    }

                    if (session.RemoteHeaders.ContainsKey(EpAuthPacketHeader.AuthenticationData))
                    {
                        var authResult = session.AuthenticationHandler.Initialize(session, session.RemoteHeaders[EpAuthPacketHeader.AuthenticationData]);
                    }

                    if (authPacket.Acknowledgement == EpAuthPacketAcknowledgement.SessionEstablished)
                    {
                        // session established, check if authentication is required
                        AuthenticatonCompleted("guest");
                    }
                }


                //if (session.AuthenticationMode == AuthenticationMode.None)
                //{
                //    // establish session without authentication
                //}

                //if (session.AuthenticationHandler == null)
                //{
                //    throw new Exception("No authentication handler assigned for the session.");
                //}

                //try
                //{
                //    var result = session.AuthenticationHandler.Process(authPacket);
                //    if (result.Ruling == AuthenticationRuling.Succeeded)
                //    {
                //        AuthenticatonCompleted(result.Identity);
                //    }
                //    else if (result.Ruling == AuthenticationRuling.InProgress)
                //    {
                //        SendParams()
                //         .AddUInt8((byte)EpAuthPacketCommand.Acknowledge)
                //         .AddUInt8Array(Codec.Compose(
                //                         result.HandshakePayload
                //                        , this.Instance.Warehouse, this))
                //         .Done();

                //    }
                //    else if (result.Ruling == AuthenticationRuling.Failed)
                //    {
                //        // Send the server side error
                //        SendParams()
                //            .AddUInt8((byte)EpAuthPacketEvent.ErrorTerminate)
                //            .AddUInt8Array(Codec.Compose(
                //                new object[] {(ushort)result.ExceptionCode,
                //                        result.ExceptionMessage }
                //                , this.Instance.Warehouse, this))
                //            .Done();
                //    }
                //}
                //catch (Exception ex)
                //{
                //    // Send the server side error
                //    SendParams()
                //        .AddUInt8((byte)EpAuthPacketEvent.ErrorTerminate)
                //        .AddUInt8Array(Codec.Compose(
                //            new object[] { (ushort)ExceptionCode.GeneralFailure,
                //                    ex.Message }
                //        , this.Instance.Warehouse, this))
                //        .Done();
                //}

            }
        }

        return offset;
    }

    void AuthenticatonCompleted(string identity)
    {
        if (this.Instance == null)
        {
            Server.Instance.Warehouse.Put(
                Server.Instance.Link + "/" + this.GetHashCode().ToString().Replace("/", "_"), this)
                .Then(x =>
                {
                    session.AuthorizedIdentity = identity;

                    authenticated = true;
                    Status = EpConnectionStatus.Connected;
                    openReply?.Trigger(true);
                    openReply = null;
                    OnReady?.Invoke(this);

                    Server?.Membership?.Login(session);
                    LoginDate = DateTime.Now;

                }).Error(x =>
                {
                    openReply?.TriggerError(x);
                    openReply = null;
                });
        }
        else
        {
            session.AuthorizedIdentity = identity;
            authenticated = true;
            Status = EpConnectionStatus.Connected;
            openReply?.Trigger(true);
            openReply = null;
            OnReady?.Invoke(this);
            Server?.Membership?.Login(session);
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
        uint offset = 0;
        uint ends = (uint)msg.Length;

        var packs = new List<string>();

        var chunkId = (new Random()).Next(1000, 1000000);


        this.Socket.Hold();

        try
        {
            while (offset < ends)
            {
                offset = processPacket(msg, offset, ends, data, chunkId);
            }
        }
        catch (Exception ex)
        {
            Global.Log(ex);
        }
        finally
        {
            this.Socket?.Unhold();
        }
    }

    /// <summary>
    /// Resource interface
    /// </summary>
    /// <param name="trigger">Resource trigger.</param>
    /// <returns></returns>
    public AsyncReply<bool> Trigger(ResourceTrigger trigger)
    {
        if (trigger == ResourceTrigger.Open)
        {
            if (this.Server != null)
                return new AsyncReply<bool>(true);

            var host = Instance.Name.Split(':');

            var address = host[0];
            var port = host.Length > 1 ? ushort.Parse(host[1]) : (ushort)10518;
            // assign domain from hostname if not provided
            var domain = Domain != null ? Domain : address;

            return Connect(null, address, port, domain);
        }

        return new AsyncReply<bool>(true);
    }




    public AsyncReply<bool> Connect(ISocket socket = null, string hostname = null, ushort port = 0, string domain = null)
    {
        if (openReply != null)
            throw new AsyncException(ErrorType.Exception, 0, "Connection in progress");

        Status = EpConnectionStatus.Connecting;

        openReply = new AsyncReply<bool>();

        if (hostname != null)
        {
            session = new Session();
            isInitiator = true;
            invalidCredentials = false;

            session.LocalHeaders[EpAuthPacketHeader.Domain] = domain;
        }

        if (session == null)
            throw new AsyncException(ErrorType.Exception, 0, "Session not initialized");

        if (socket == null)
        {
            var os = RuntimeInformation.FrameworkDescription;
            if (UseWebSocket || RuntimeInformation.OSDescription == "Browser")
                socket = new FrameworkWebSocket();
            else
                socket = new TCPSocket();
        }

        if (port > 0)
            this._port = port;
        if (hostname != null)
            this._hostname = hostname;

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
                openReply.TriggerError(x);
                openReply = null;
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
                foreach (KeyValuePair<uint, WeakReference<EpResource>> kv in suspendedResources)
                {
                    EpResource r;
                    if (kv.Value.TryGetTarget(out r))
                        toBeRestored.Add(r);
                }

                foreach (var r in toBeRestored)
                {

                    var link = DC.ToBytes(r.DistributedResourceLink);

                    Global.Log("EpConnection", LogType.Debug, "Restoreing " + r.DistributedResourceLink);

                    try
                    {
                        var id = (uint)await SendRequest(EpPacketRequest.GetResourceIdByLink, link);


                        // remove from suspended.
                        suspendedResources.Remove(r.DistributedResourceInstanceId);

                        // id changed ?
                        if (id != r.DistributedResourceInstanceId)
                            r.DistributedResourceInstanceId = id;

                        neededResources[id] = r;

                        await Fetch(id, null);

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
            neededResources.Add((resource as EpResource).DistributedResourceInstanceId, (EpResource)resource);
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
        if (isInitiator)
            Declare();
    }

    protected override void Disconnected()
    {
        // clean up
        authenticated = false;
        readyToEstablish = false;
        Status = EpConnectionStatus.Closed;

        keepAliveTimer.Stop();

        // @TODO: lock requests

        foreach (var x in requests.Values)
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

        foreach (var x in resourceRequests.Values)
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

        foreach (var x in typeDefsByIdRequests.Values)
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

        foreach (var x in typeDefsByNameRequests.Values)
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


        requests.Clear();
        resourceRequests.Clear();
        typeDefsByIdRequests.Clear();
        typeDefsByNameRequests.Clear();


        foreach (var x in attachedResources.Values)
        {
            EpResource r;
            if (x.TryGetTarget(out r))
            {
                r.Suspend();
                suspendedResources[r.DistributedResourceInstanceId] = x;
            }
        }

        if (Server != null)
        {
            suspendedResources.Clear();

            UnsubscribeAll();
            Instance.Warehouse.Remove(this);

            if (authenticated)
                Server.Membership?.Logout(session);

        }
        else if (AutoReconnect && !invalidCredentials)
        {
            // reconnect
            Task.Delay((int)ReconnectInterval).ContinueWith((x) => Reconnect());
        }
        else
        {
            suspendedResources.Clear();
        }


        attachedResources.Clear();

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
