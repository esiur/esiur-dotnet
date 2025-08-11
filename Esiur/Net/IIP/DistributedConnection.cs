/*
 
Copyright (c) 2017 Ahmed Kh. Zamil

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

using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using Esiur.Net.Sockets;
using Esiur.Data;
using Esiur.Misc;
using Esiur.Core;
using Esiur.Resource;
using Esiur.Security.Authority;
using Esiur.Resource.Template;
using System.Linq;
using Esiur.Net.HTTP;
using System.Timers;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Esiur.Net.Packets.HTTP;
using Esiur.Security.Membership;
using Esiur.Net.Packets;

namespace Esiur.Net.IIP;
public partial class DistributedConnection : NetworkConnection, IStore
{

    // Delegates
    public delegate void ReadyEvent(DistributedConnection sender);
    public delegate void ErrorEvent(DistributedConnection sender, byte errorCode, string errorMessage);
    public delegate void ResumedEvent(DistributedConnection sender);

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

    Timer keepAliveTimer;
    DateTime? lastKeepAliveSent;
    DateTime? lastKeepAliveReceived;


    IIPPacket packet = new IIPPacket();
    IIPAuthPacket authPacket = new IIPAuthPacket();


    Session session;

    AsyncReply<bool> openReply;

    byte[] localPasswordOrToken;
    bool ready, readyToEstablish;

    string _hostname;
    ushort _port;

    bool initialPacket = true;


    // Properties

    public DateTime LoginDate { get; private set; }

    /// <summary>
    /// Distributed server responsible for this connection, usually for incoming connections.
    /// </summary>
    public DistributedServer Server { get; internal set; }


    /// <summary>
    /// The session related to this connection.
    /// </summary>
    public Session Session => session;

    [Export] 
    public virtual ConnectionStatus Status { get; private set; }

    [Export] 
    public virtual uint Jitter { get; private set; }

    // Attributes

    [Attribute]
    public uint KeepAliveTime { get; set; } = 10;

    [Attribute]
    public ExceptionLevel ExceptionLevel { get; set; }
                = ExceptionLevel.Code | ExceptionLevel.Message | ExceptionLevel.Source | ExceptionLevel.Trace;

    [Attribute]
    public Func<AuthorizationRequest, AsyncReply<object>> Authenticator { get; set; }
    //public Func<Map<IIPAuthPacketIAuthHeader, object>, AsyncReply<object>> Authenticator { get; set; }

    [Attribute]
    public bool AutoReconnect { get; set; } = false;

    [Attribute]
    public uint ReconnectInterval { get; set; } = 5;

    [Attribute]
    public string Username { get; set; }

    [Attribute]
    public bool UseWebSocket { get; set; }

    [Attribute]
    public bool SecureWebSocket { get; set; }

    [Attribute]
    public string Password { get; set; }

    [Attribute]
    public string Token { get; set; }

    [Attribute]
    public ulong TokenIndex { get; set; }

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

        Global.Counters["IIP Sent Packets"]++;
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
    public override void Assign(Sockets.ISocket socket)
    {
        base.Assign(socket);

        session.LocalHeaders[IIPAuthPacketHeader.IPAddress] = socket.RemoteEndPoint.Address.GetAddressBytes();

        if (socket.State == SocketState.Established &&
            session.AuthenticationType == AuthenticationType.Client)
        {
            Declare();
        }
    }

    private void Declare()
    {

        if (session.KeyExchanger != null)
        {
            // create key
            var key = session.KeyExchanger.GetPublicKey();
            session.LocalHeaders[IIPAuthPacketHeader.CipherKey] = key;
        }


        if (session.LocalMethod == AuthenticationMethod.Credentials
            && session.RemoteMethod == AuthenticationMethod.None)
        {
            // change to Map<byte, object> for compatibility
            var headers = Codec.Compose(session.LocalHeaders.Select(x => new KeyValuePair<byte, object>((byte)x.Key, x.Value)), this);

            // declare (Credentials -> No Auth, No Enctypt)
            SendParams()
                .AddUInt8((byte)IIPAuthPacketInitialize.CredentialsNoAuth)
                .AddUInt8Array(headers)
                .Done();

        }
        else if (session.LocalMethod == AuthenticationMethod.Token
            && session.RemoteMethod == AuthenticationMethod.None)
        {
            // change to Map<byte, object> for compatibility
            var headers = Codec.Compose(session.LocalHeaders.Select(x => new KeyValuePair<byte, object>((byte)x.Key, x.Value)), this);

            SendParams()
                .AddUInt8((byte)IIPAuthPacketInitialize.TokenNoAuth)
                .AddUInt8Array(headers)
                .Done();
        }
        else if (session.LocalMethod == AuthenticationMethod.None
            && session.RemoteMethod == AuthenticationMethod.None)
        {
            // change to Map<byte, object> for compatibility
            var headers = Codec.Compose(session.LocalHeaders.Select(x => new KeyValuePair<byte, object>((byte)x.Key, x.Value)), this);

            // @REVIEW: MITM Attack can still occure
            SendParams()
                .AddUInt8((byte)IIPAuthPacketInitialize.NoAuthNoAuth)
                .AddUInt8Array(headers)
                .Done();
        }
        else
        {
            throw new NotImplementedException("Authentication method is not implemented.");
        }

    }

    /// <summary>
    /// Create a new distributed connection. 
    /// </summary>
    /// <param name="socket">Socket to transfer data through.</param>
    /// <param name="domain">Working domain.</param>
    /// <param name="username">Username.</param>
    /// <param name="password">Password.</param>
    public DistributedConnection(Sockets.ISocket socket, string domain, string username, string password)
    {
        this.session = new Session();

        session.AuthenticationType = AuthenticationType.Client;
        session.LocalHeaders[IIPAuthPacketHeader.Domain] = domain;
        session.LocalHeaders[IIPAuthPacketHeader.Username] = username;
        session.LocalMethod = AuthenticationMethod.Credentials;
        session.RemoteMethod = AuthenticationMethod.None;


        this.localPasswordOrToken = DC.ToBytes(password);

        init();

        Assign(socket);
    }

    public DistributedConnection(Sockets.ISocket socket, string domain, ulong tokenIndex, string token)
    {
        this.session = new Session();


        session.AuthenticationType = AuthenticationType.Client;
        session.LocalHeaders[IIPAuthPacketHeader.Domain] = domain;
        session.LocalHeaders[IIPAuthPacketHeader.TokenIndex] = tokenIndex;
        session.LocalMethod = AuthenticationMethod.Credentials;
        session.RemoteMethod = AuthenticationMethod.None;
        this.localPasswordOrToken = DC.ToBytes(token);

        init();

        Assign(socket);
    }


    /// <summary>
    /// Create a new instance of a distributed connection
    /// </summary>
    public DistributedConnection()
    {
        session = new Session();
        session.AuthenticationType = AuthenticationType.Host;
        session.LocalMethod = AuthenticationMethod.None;
        init();
    }



    public string Link(IResource resource)
    {
        if (resource is DistributedResource)
        {
            var r = resource as DistributedResource;
            if (r.Instance.Store == this)
                return this.Instance.Name + "/" + r.DistributedResourceInstanceId;
        }

        return null;
    }


    void init()
    {
        //var q = queue;
        queue.Then((x) =>
        {
            if (x.Type == DistributedResourceQueueItem.DistributedResourceQueueItemType.Event)
                x.Resource._EmitEventByIndex(x.Index, x.Value);
            else
                x.Resource._UpdatePropertyByIndex(x.Index, x.Value);
        });

        // set local nonce
        session.LocalHeaders[IIPAuthPacketHeader.Nonce] = Global.GenerateBytes(32);

        keepAliveTimer = new Timer(KeepAliveInterval * 1000);
        keepAliveTimer.Elapsed += KeepAliveTimer_Elapsed;
    }


    private void KeepAliveTimer_Elapsed(object sender, ElapsedEventArgs e)
    {
        if (!IsConnected)
            return;


        keepAliveTimer.Stop();

        var now = DateTime.UtcNow;

        uint interval = lastKeepAliveSent == null ? 0 :
                        (uint)(now - (DateTime)lastKeepAliveSent).TotalMilliseconds;

        lastKeepAliveSent = now;

        SendRequest(IIPPacketRequest.KeepAlive)
                .AddDateTime(now)
                .AddUInt32(interval)
                .Done()
                .Then(x =>
                {

                    Jitter = (uint)x[1];
                    keepAliveTimer.Start();
                    //Console.WriteLine($"Keep Alive Received {Jitter}");
                }).Error(ex =>
                {
                    keepAliveTimer.Stop();
                    Close();
                }).Timeout((int)(KeepAliveTime * 1000), () =>
                {
                    keepAliveTimer.Stop();
                    Close();
                });

        //Console.WriteLine("Keep Alive sent");


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
        if (ready)
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

                if (packet.Command == IIPPacketCommand.Event)
                {
                    switch (packet.Event)
                    {
                        case IIPPacketEvent.ResourceReassigned:
                            IIPEventResourceReassigned(packet.ResourceId, packet.NewResourceId);
                            break;
                        case IIPPacketEvent.ResourceDestroyed:
                            IIPEventResourceDestroyed(packet.ResourceId);
                            break;
                        case IIPPacketEvent.PropertyUpdated:
                            IIPEventPropertyUpdated(packet.ResourceId, packet.MethodIndex, (TransmissionType)packet.DataType, msg); 
                            break;
                        case IIPPacketEvent.EventOccurred:
                            IIPEventEventOccurred(packet.ResourceId, packet.MethodIndex, (TransmissionType)packet.DataType, msg);
                            break;

                        case IIPPacketEvent.ChildAdded:
                            IIPEventChildAdded(packet.ResourceId, packet.ChildId);
                            break;
                        case IIPPacketEvent.ChildRemoved:
                            IIPEventChildRemoved(packet.ResourceId, packet.ChildId);
                            break;
                        case IIPPacketEvent.Renamed:
                            IIPEventRenamed(packet.ResourceId, packet.ResourceLink);
                            break;
                        case IIPPacketEvent.AttributesUpdated:
                            // @TODO: fix this
                            //IIPEventAttributesUpdated(packet.ResourceId, packet.Content);
                            break;
                    }
                }
                else if (packet.Command == IIPPacketCommand.Request)
                {
                    switch (packet.Action)
                    {
                        // Manage
                        case IIPPacketRequest.AttachResource:
                            IIPRequestAttachResource(packet.CallbackId, packet.ResourceId);
                            break;
                        case IIPPacketRequest.ReattachResource:
                            IIPRequestReattachResource(packet.CallbackId, packet.ResourceId, packet.ResourceAge);
                            break;
                        case IIPPacketRequest.DetachResource:
                            IIPRequestDetachResource(packet.CallbackId, packet.ResourceId);
                            break;
                        case IIPPacketRequest.CreateResource:
                            //@TODO : fix this
                            //IIPRequestCreateResource(packet.CallbackId, packet.StoreId, packet.ResourceId, packet.Content);
                            break;
                        case IIPPacketRequest.DeleteResource:
                            IIPRequestDeleteResource(packet.CallbackId, packet.ResourceId);
                            break;
                        case IIPPacketRequest.AddChild:
                            IIPRequestAddChild(packet.CallbackId, packet.ResourceId, packet.ChildId);
                            break;
                        case IIPPacketRequest.RemoveChild:
                            IIPRequestRemoveChild(packet.CallbackId, packet.ResourceId, packet.ChildId);
                            break;
                        case IIPPacketRequest.RenameResource:
                            IIPRequestRenameResource(packet.CallbackId, packet.ResourceId, packet.ResourceName);
                            break;

                        // Inquire
                        case IIPPacketRequest.TemplateFromClassName:
                            IIPRequestTemplateFromClassName(packet.CallbackId, packet.ClassName);
                            break;
                        case IIPPacketRequest.TemplateFromClassId:
                            IIPRequestTemplateFromClassId(packet.CallbackId, packet.ClassId);
                            break;
                        case IIPPacketRequest.TemplateFromResourceId:
                            IIPRequestTemplateFromResourceId(packet.CallbackId, packet.ResourceId);
                            break;
                        case IIPPacketRequest.QueryLink:
                            IIPRequestQueryResources(packet.CallbackId, packet.ResourceLink);
                            break;

                        case IIPPacketRequest.ResourceChildren:
                            IIPRequestResourceChildren(packet.CallbackId, packet.ResourceId);
                            break;
                        case IIPPacketRequest.ResourceParents:
                            IIPRequestResourceParents(packet.CallbackId, packet.ResourceId);
                            break;

                        case IIPPacketRequest.ResourceHistory:
                            IIPRequestInquireResourceHistory(packet.CallbackId, packet.ResourceId, packet.FromDate, packet.ToDate);
                            break;

                        case IIPPacketRequest.LinkTemplates:
                            IIPRequestLinkTemplates(packet.CallbackId, packet.ResourceLink);
                            break;

                        // Invoke
                        case IIPPacketRequest.InvokeFunction:
                            IIPRequestInvokeFunction(packet.CallbackId, packet.ResourceId, packet.MethodIndex, (TransmissionType)packet.DataType, msg);
                            break;

                        //case IIPPacket.IIPPacketAction.InvokeFunctionNamedArguments:
                        //    IIPRequestInvokeFunctionNamedArguments(packet.CallbackId, packet.ResourceId, packet.MethodIndex, (TransmissionType)packet.DataType, msg);
                        //    break;

                        //case IIPPacket.IIPPacketAction.GetProperty:
                        //    IIPRequestGetProperty(packet.CallbackId, packet.ResourceId, packet.MethodIndex);
                        //    break;
                        //case IIPPacket.IIPPacketAction.GetPropertyIfModified:
                        //    IIPRequestGetPropertyIfModifiedSince(packet.CallbackId, packet.ResourceId, packet.MethodIndex, packet.ResourceAge);
                        //    break;

                        case IIPPacketRequest.Listen:
                            IIPRequestListen(packet.CallbackId, packet.ResourceId, packet.MethodIndex);
                            break;

                        case IIPPacketRequest.Unlisten:
                            IIPRequestUnlisten(packet.CallbackId, packet.ResourceId, packet.MethodIndex);
                            break;

                        case IIPPacketRequest.SetProperty:
                            IIPRequestSetProperty(packet.CallbackId, packet.ResourceId, packet.MethodIndex, (TransmissionType)packet.DataType, msg);
                            break;

                        // Attribute
                        case IIPPacketRequest.GetAllAttributes:
                            // @TODO : fix this
                            //IIPRequestGetAttributes(packet.CallbackId, packet.ResourceId, packet.Content, true);
                            break;
                        case IIPPacketRequest.UpdateAllAttributes:
                            // @TODO : fix this
                            //IIPRequestUpdateAttributes(packet.CallbackId, packet.ResourceId, packet.Content, true);
                            break;
                        case IIPPacketRequest.ClearAllAttributes:
                            // @TODO : fix this
                            //IIPRequestClearAttributes(packet.CallbackId, packet.ResourceId, packet.Content, true);
                            break;
                        case IIPPacketRequest.GetAttributes:
                            // @TODO : fix this
                            //IIPRequestGetAttributes(packet.CallbackId, packet.ResourceId, packet.Content, false);
                            break;
                        case IIPPacketRequest.UpdateAttributes:
                            // @TODO : fix this
                            //IIPRequestUpdateAttributes(packet.CallbackId, packet.ResourceId, packet.Content, false);
                            break;
                        case IIPPacketRequest.ClearAttributes:
                            // @TODO : fix this
                            //IIPRequestClearAttributes(packet.CallbackId, packet.ResourceId, packet.Content, false);
                            break;

                        case IIPPacketRequest.KeepAlive:
                            IIPRequestKeepAlive(packet.CallbackId, packet.CurrentTime, packet.Interval);
                            break;

                        case IIPPacketRequest.ProcedureCall:
                            IIPRequestProcedureCall(packet.CallbackId, packet.Procedure, (TransmissionType)packet.DataType, msg);
                            break;

                        case IIPPacketRequest.StaticCall:
                            IIPRequestStaticCall(packet.CallbackId, packet.ClassId, packet.MethodIndex, (TransmissionType)packet.DataType, msg);
                            break;

                    }
                }
                else if (packet.Command == IIPPacketCommand.Reply)
                {
                    switch (packet.Action)
                    {
                        // Manage
                        case IIPPacketRequest.AttachResource:
                            IIPReply(packet.CallbackId, packet.ClassId, packet.ResourceAge, packet.ResourceLink, packet.DataType, msg);
                            break;

                        case IIPPacketRequest.ReattachResource:
                            IIPReply(packet.CallbackId, packet.ResourceAge, packet.DataType, msg);

                            break;
                        case IIPPacketRequest.DetachResource:
                            IIPReply(packet.CallbackId);
                            break;

                        case IIPPacketRequest.CreateResource:
                            IIPReply(packet.CallbackId, packet.ResourceId);
                            break;

                        case IIPPacketRequest.DeleteResource:
                        case IIPPacketRequest.AddChild:
                        case IIPPacketRequest.RemoveChild:
                        case IIPPacketRequest.RenameResource:
                            IIPReply(packet.CallbackId);
                            break;

                        // Inquire

                        case IIPPacketRequest.TemplateFromClassName:
                        case IIPPacketRequest.TemplateFromClassId:
                        case IIPPacketRequest.TemplateFromResourceId:

                            var content = msg.Clip(packet.DataType.Value.Offset, (uint)packet.DataType.Value.ContentLength);
                            IIPReply(packet.CallbackId, TypeTemplate.Parse(content));
                            break;

                        case IIPPacketRequest.QueryLink:
                        case IIPPacketRequest.ResourceChildren:
                        case IIPPacketRequest.ResourceParents:
                        case IIPPacketRequest.ResourceHistory:
                        case IIPPacketRequest.LinkTemplates:
                            IIPReply(packet.CallbackId, (TransmissionType)packet.DataType, msg);// packet.Content);
                            break;

                        // Invoke
                        case IIPPacketRequest.InvokeFunction:
                        case IIPPacketRequest.StaticCall:
                        case IIPPacketRequest.ProcedureCall:
                            IIPReplyInvoke(packet.CallbackId, (TransmissionType)packet.DataType, msg);// packet.Content);
                            break;

                        //case IIPPacket.IIPPacketAction.GetProperty:
                        //    IIPReply(packet.CallbackId, packet.Content);
                        //    break;

                        //case IIPPacket.IIPPacketAction.GetPropertyIfModified:
                        //    IIPReply(packet.CallbackId, packet.Content);
                        //    break;

                        case IIPPacketRequest.Listen:
                        case IIPPacketRequest.Unlisten:
                        case IIPPacketRequest.SetProperty:
                            IIPReply(packet.CallbackId);
                            break;

                        // Attribute
                        case IIPPacketRequest.GetAllAttributes:
                        case IIPPacketRequest.GetAttributes:
                            IIPReply(packet.CallbackId, (TransmissionType)packet.DataType, msg);// packet.Content);
                            break;

                        case IIPPacketRequest.UpdateAllAttributes:
                        case IIPPacketRequest.UpdateAttributes:
                        case IIPPacketRequest.ClearAllAttributes:
                        case IIPPacketRequest.ClearAttributes:
                            IIPReply(packet.CallbackId);
                            break;

                        case IIPPacketRequest.KeepAlive:
                            IIPReply(packet.CallbackId, packet.CurrentTime, packet.Jitter);
                            break;
                    }

                }
                else if (packet.Command == IIPPacketCommand.Report)
                {
                    switch (packet.Report)
                    {
                        case IIPPacketReport.ManagementError:
                            IIPReportError(packet.CallbackId, ErrorType.Management, packet.ErrorCode, null);
                            break;
                        case IIPPacketReport.ExecutionError:
                            IIPReportError(packet.CallbackId, ErrorType.Exception, packet.ErrorCode, packet.ErrorMessage);
                            break;
                        case IIPPacketReport.ProgressReport:
                            IIPReportProgress(packet.CallbackId, ProgressType.Execution, packet.ProgressValue, packet.ProgressMax);
                            break;
                        case IIPPacketReport.ChunkStream:
                            IIPReportChunk(packet.CallbackId, (TransmissionType)packet.DataType, msg); 

                            break;
                    }
                }
            }
        }

        else
        {

            // check if the reqeust through websockets

            if (initialPacket)
            {
                initialPacket = false;

                if (msg.Length > 3 && Encoding.Default.GetString(msg, 0, 3) == "GET")
                {
                    // Parse with http packet
                    var req = new HTTPRequestPacket();
                    var pSize = req.Parse(msg, 0, (uint)msg.Length);
                    if (pSize > 0)
                    {
                        // check for WS upgrade

                        if (HTTPConnection.IsWebsocketRequest(req))
                        {

                            Socket?.Unhold();

                            var res = new HTTPResponsePacket();

                            HTTPConnection.Upgrade(req, res);


                            res.Compose(HTTPComposeOption.AllCalculateLength);
                            Send(res.Data);
                            // replace my socket with websockets
                            var tcpSocket = this.Unassign();
                            var wsSocket = new WSocket(tcpSocket);
                            this.Assign(wsSocket);
                        }
                        else
                        {

                            var res = new HTTPResponsePacket();
                            res.Number = HTTPResponseCode.BadRequest;
                            res.Compose(HTTPComposeOption.AllCalculateLength);
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

                if (session.AuthenticationType == AuthenticationType.Host)
                {
                    ProcessHostAuth(msg);
                }
                else if (session.AuthenticationType == AuthenticationType.Client)
                {
                    ProcessClientAuth(msg);
                }
            }
        }

        return offset;

        //if (offset < ends)
        // processPacket(msg, offset, ends, data, chunkId);
    }

    private void ProcessClientAuth(byte[] data)
    {
        if (authPacket.Command == IIPAuthPacketCommand.Acknowledge)
        {
            // if there is a mismatch in authentication
            if (session.LocalMethod != authPacket.RemoteMethod
                || session.RemoteMethod != authPacket.LocalMethod)
            {
                openReply?.TriggerError(new Exception("Peer refused authentication method."));
                openReply = null;
            }

            // Parse remote headers

            var dataType = authPacket.DataType.Value;

            var (_, parsed) = Codec.Parse(data, dataType.Offset, this, null, dataType);

            var rt = (Map<byte, object>)parsed.Wait();

            session.RemoteHeaders = rt.Select(x => new KeyValuePair<IIPAuthPacketHeader, object>((IIPAuthPacketHeader)x.Key, x.Value));

            if (session.LocalMethod == AuthenticationMethod.None)
            {
                // send establish
                SendParams()
                            .AddUInt8((byte)IIPAuthPacketAction.EstablishNewSession)
                            .Done();
            }
            else if (session.LocalMethod == AuthenticationMethod.Credentials
                    || session.LocalMethod == AuthenticationMethod.Token)
            {
                var remoteNonce = (byte[])session.RemoteHeaders[IIPAuthPacketHeader.Nonce];
                var localNonce = (byte[])session.LocalHeaders[IIPAuthPacketHeader.Nonce];

                // send our hash
                var hashFunc = SHA256.Create();
                // local nonce + password or token + remote nonce
                var challenge = hashFunc.ComputeHash(new BinaryList()
                                                    .AddUInt8Array(localNonce)
                                                    .AddUInt8Array(localPasswordOrToken)
                                                    .AddUInt8Array(remoteNonce)
                                                    .ToArray());

                SendParams()
                    .AddUInt8((byte)IIPAuthPacketAction.AuthenticateHash)
                    .AddUInt8((byte)IIPAuthPacketHashAlgorithm.SHA256)
                    .AddUInt16((ushort)challenge.Length)
                    .AddUInt8Array(challenge)
                    .Done();
            }

        }
        else if (authPacket.Command == IIPAuthPacketCommand.Action)
        {
            if (authPacket.Action == IIPAuthPacketAction.AuthenticateHash)
            {
                var remoteNonce = (byte[])session.RemoteHeaders[IIPAuthPacketHeader.Nonce];
                var localNonce = (byte[])session.LocalHeaders[IIPAuthPacketHeader.Nonce];

                // check if the server knows my password
                var hashFunc = SHA256.Create();

                var challenge = hashFunc.ComputeHash(new BinaryList()
                                                        .AddUInt8Array(remoteNonce)
                                                        .AddUInt8Array(localPasswordOrToken)
                                                        .AddUInt8Array(localNonce)
                                                        .ToArray());


                if (challenge.SequenceEqual(authPacket.Challenge))
                {
                    // send establish request
                    SendParams()
                                .AddUInt8((byte)IIPAuthPacketAction.EstablishNewSession)
                                .Done();
                }
                else
                {
                    SendParams()
                                .AddUInt8((byte)IIPAuthPacketEvent.ErrorTerminate)
                                .AddUInt8((byte)ExceptionCode.ChallengeFailed)
                                .AddUInt16(16)
                                .AddString("Challenge Failed")
                                .Done();

                }
            }
        }
        else if (authPacket.Command == IIPAuthPacketCommand.Event)
        {
            if (authPacket.Event == IIPAuthPacketEvent.ErrorTerminate
                || authPacket.Event == IIPAuthPacketEvent.ErrorMustEncrypt
                || authPacket.Event == IIPAuthPacketEvent.ErrorRetry)
            {
                invalidCredentials = true;
                openReply?.TriggerError(new AsyncException(ErrorType.Management, authPacket.ErrorCode, authPacket.Message));
                openReply = null;
                OnError?.Invoke(this, authPacket.ErrorCode, authPacket.Message);
                Close();
            }
            else if (authPacket.Event == IIPAuthPacketEvent.IndicationEstablished)
            {
                session.Id = authPacket.SessionId;
                session.AuthorizedAccount = authPacket.AccountId.GetString(0, (uint)authPacket.AccountId.Length);

                ready = true;
                Status = ConnectionStatus.Connected;

                
                // put it in the warehouse

                if (this.Instance == null)
                {
                    Warehouse.Put(session.AuthorizedAccount.Replace("/", "_"), this, null, Server).Then(x =>
                    {
                        openReply?.Trigger(true);
                        OnReady?.Invoke(this);
                        openReply = null;


                    }).Error(x =>
                    {
                        openReply?.TriggerError(x);
                        openReply = null;
                    });
                }
                else
                {
                    openReply?.Trigger(true);
                    openReply = null;

                    OnReady?.Invoke(this);
                }

                // start perodic keep alive timer
                keepAliveTimer.Start();

            }
            else if (authPacket.Event == IIPAuthPacketEvent.IAuthPlain)
            {
                var dataType = authPacket.DataType.Value;
                var (_, parsed) = Codec.Parse(data, dataType.Offset, this, null, dataType);
                var rt = (Map<byte, object>)parsed.Wait();

                var headers = rt.Select(x => new KeyValuePair<IIPAuthPacketIAuthHeader, object>((IIPAuthPacketIAuthHeader)x.Key, x.Value));
                var iAuthRequest = new AuthorizationRequest(headers);

                if (Authenticator == null)
                {
                    SendParams()
                     .AddUInt8((byte)IIPAuthPacketEvent.ErrorTerminate)
                     .AddUInt8((byte)ExceptionCode.NotSupported)
                     .AddUInt16(13)
                     .AddString("Not supported")
                     .Done();
                }
                else
                {
                    Authenticator(iAuthRequest).Then(response =>
                    {
                        SendParams()
                            .AddUInt8((byte)IIPAuthPacketAction.IAuthPlain)
                            .AddUInt32((uint)headers[IIPAuthPacketIAuthHeader.Reference])
                            .AddUInt8Array(Codec.Compose(response, this))
                            .Done();
                    })
                    .Timeout(iAuthRequest.Timeout * 1000,
                        () => {
                            SendParams()
                                .AddUInt8((byte)IIPAuthPacketEvent.ErrorTerminate)
                                .AddUInt8((byte)ExceptionCode.Timeout)
                                .AddUInt16(7)
                                .AddString("Timeout")
                                .Done();
                        });
                }
            }
            else if (authPacket.Event == IIPAuthPacketEvent.IAuthHashed)
            {
                var dataType = authPacket.DataType.Value;
                var (_, parsed) = Codec.Parse(data, dataType.Offset, this, null, dataType);
                var rt = (Map<byte, object>)parsed.Wait();


                var headers = rt.Select(x => new KeyValuePair<IIPAuthPacketIAuthHeader, object>((IIPAuthPacketIAuthHeader)x.Key, x.Value));
                var iAuthRequest = new AuthorizationRequest(headers);

                if (Authenticator == null)
                {
                    SendParams()
                     .AddUInt8((byte)IIPAuthPacketEvent.ErrorTerminate)
                     .AddUInt8((byte)ExceptionCode.NotSupported)
                     .AddUInt16(13)
                     .AddString("Not supported")
                     .Done();
                }
                else
                {

                    Authenticator(iAuthRequest).Then(response =>
                    {
                        var sha = SHA256.Create();
                        var hash = sha.ComputeHash(new BinaryList()
                            .AddUInt8Array((byte[])session.LocalHeaders[IIPAuthPacketHeader.Nonce])
                            .AddUInt8Array(Codec.Compose(response, this))
                            .AddUInt8Array((byte[])session.RemoteHeaders[IIPAuthPacketHeader.Nonce])
                            .ToArray());

                        SendParams()
                            .AddUInt8((byte)IIPAuthPacketAction.IAuthHashed)
                            .AddUInt32((uint)headers[IIPAuthPacketIAuthHeader.Reference])
                            .AddUInt8((byte)IIPAuthPacketHashAlgorithm.SHA256)
                            .AddUInt16((ushort)hash.Length)
                            .AddUInt8Array(hash)
                            .Done();
                    })
                    .Timeout(iAuthRequest.Timeout * 1000,
                        () => {
                        SendParams()
                            .AddUInt8((byte)IIPAuthPacketEvent.ErrorTerminate)
                            .AddUInt8((byte)ExceptionCode.Timeout)
                            .AddUInt16(7)
                            .AddString("Timeout")
                            .Done();
                    });
                }
            }
            else if (authPacket.Event == IIPAuthPacketEvent.IAuthEncrypted)
            {
                throw new NotImplementedException("IAuthEncrypted not implemented.");
            }
        }
    }

    private void ProcessHostAuth(byte[] data)
    {
        if (authPacket.Command == IIPAuthPacketCommand.Initialize)
        {
            // Parse headers

            var dataType = authPacket.DataType.Value;

            var (_, parsed) = Codec.Parse(data, dataType.Offset, this, null, dataType);

            var rt = (Map<byte, object>)parsed.Wait();


            session.RemoteHeaders = rt.Select(x => new KeyValuePair<IIPAuthPacketHeader, object>((IIPAuthPacketHeader)x.Key, x.Value));

            session.RemoteMethod = authPacket.LocalMethod;


            if (authPacket.Initialization == IIPAuthPacketInitialize.CredentialsNoAuth)
            {
                try
                {

                    var username = (string)session.RemoteHeaders[IIPAuthPacketHeader.Username];
                    var domain = (string)session.RemoteHeaders[IIPAuthPacketHeader.Domain];
                    //var remoteNonce = (byte[])session.RemoteHeaders[IIPAuthPacketHeader.Nonce];

                    if (Server.Membership == null)
                    {
                        var errMsg = DC.ToBytes("Membership not set.");

                        SendParams()
                            .AddUInt8((byte)IIPAuthPacketEvent.ErrorTerminate)
                            .AddUInt8((byte)ExceptionCode.GeneralFailure)
                            .AddUInt16((ushort)errMsg.Length)
                            .AddUInt8Array(errMsg)
                            .Done();
                    }
                    else Server.Membership.UserExists(username, domain).Then(x =>
                    {
                        if (x != null)
                        {
                            session.AuthorizedAccount = x;

                            var localHeaders = session.LocalHeaders.Select(x => new KeyValuePair<byte, object>((byte)x.Key, x.Value));

                            SendParams()
                                        .AddUInt8((byte)IIPAuthPacketAcknowledge.NoAuthCredentials)
                                        .AddUInt8Array(Codec.Compose(localHeaders, this))
                                        .Done();
                        }
                        else
                        {
                            // Send user not found error
                            SendParams()
                                        .AddUInt8((byte)IIPAuthPacketEvent.ErrorTerminate)
                                        .AddUInt8((byte)ExceptionCode.UserOrTokenNotFound)
                                        .AddUInt16(14)
                                        .AddString("User not found")
                                        .Done();
                        }
                    });
                }
                catch (Exception ex)
                {
                    // Send the server side error
                    var errMsg = DC.ToBytes(ex.Message);

                    SendParams()
                        .AddUInt8((byte)IIPAuthPacketEvent.ErrorTerminate)
                        .AddUInt8((byte)ExceptionCode.GeneralFailure)
                        .AddUInt16((ushort)errMsg.Length)
                        .AddUInt8Array(errMsg)
                        .Done();
                }
            }
            else if (authPacket.Initialization == IIPAuthPacketInitialize.TokenNoAuth)
            {
                try
                {
                    if (Server.Membership == null)
                    {
                        SendParams()
                                            .AddUInt8((byte)IIPAuthPacketEvent.ErrorTerminate)
                                            .AddUInt8((byte)ExceptionCode.UserOrTokenNotFound)
                                            .AddUInt16(15)
                                            .AddString("Token not found")
                                            .Done();
                    }
                    // Check if user and token exists
                    else
                    {
                        var tokenIndex = (ulong)session.RemoteHeaders[IIPAuthPacketHeader.TokenIndex];
                        var domain = (string)session.RemoteHeaders[IIPAuthPacketHeader.Domain];
                        //var nonce = (byte[])session.RemoteHeaders[IIPAuthPacketHeader.Nonce];

                        Server.Membership.TokenExists(tokenIndex, domain).Then(x =>
                        {
                            if (x != null)
                            {
                                session.AuthorizedAccount = x;

                                var localHeaders = session.LocalHeaders.Select(x => new KeyValuePair<byte, object>((byte)x.Key, x.Value));

                                SendParams()
                                            .AddUInt8((byte)IIPAuthPacketAcknowledge.NoAuthToken)
                                            .AddUInt8Array(Codec.Compose(localHeaders, this))
                                            .Done();

                            }
                            else
                            {
                                // Send token not found error.
                                SendParams()
                                            .AddUInt8((byte)IIPAuthPacketEvent.ErrorTerminate)
                                            .AddUInt8((byte)ExceptionCode.UserOrTokenNotFound)
                                            .AddUInt16(15)
                                            .AddString("Token not found")
                                            .Done();
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    // Sender server side error.

                    var errMsg = DC.ToBytes(ex.Message);

                    SendParams()
                        .AddUInt8((byte)IIPAuthPacketEvent.ErrorTerminate)
                        .AddUInt8((byte)ExceptionCode.GeneralFailure)
                        .AddUInt16((ushort)errMsg.Length)
                        .AddUInt8Array(errMsg)
                        .Done();
                }
            }
            else if (authPacket.Initialization == IIPAuthPacketInitialize.NoAuthNoAuth)
            {
                try
                {
                    // Check if guests are allowed
                    if (Server.Membership?.GuestsAllowed ?? true)
                    {
                        var localHeaders = session.LocalHeaders.Select(x => new KeyValuePair<byte, object>((byte)x.Key, x.Value));

                        session.AuthorizedAccount = "g-" + Global.GenerateCode();

                        readyToEstablish = true;

                        SendParams()
                                    .AddUInt8((byte)IIPAuthPacketAcknowledge.NoAuthNoAuth)
                                    .AddUInt8Array(Codec.Compose(localHeaders, this))
                                    .Done();
                    }
                    else
                    {
                        // Send access denied error because the server does not allow guests.
                        SendParams()
                                    .AddUInt8((byte)IIPAuthPacketEvent.ErrorTerminate)
                                    .AddUInt8((byte)ExceptionCode.AccessDenied)
                                    .AddUInt16(18)
                                    .AddString("Guests not allowed")
                                    .Done();
                    }
                }
                catch (Exception ex)
                {
                    // Send the server side error.
                    var errMsg = DC.ToBytes(ex.Message);

                    SendParams()
                        .AddUInt8((byte)IIPAuthPacketEvent.ErrorTerminate)
                        .AddUInt8((byte)ExceptionCode.GeneralFailure)
                        .AddUInt16((ushort)errMsg.Length)
                        .AddUInt8Array(errMsg)
                        .Done();
                }
            }

        }
        else if (authPacket.Command == IIPAuthPacketCommand.Action)
        {
            if (authPacket.Action == IIPAuthPacketAction.AuthenticateHash)
            {
                var remoteHash = authPacket.Challenge;
                AsyncReply<byte[]> reply = null;

                try
                {
                    if (session.RemoteMethod == AuthenticationMethod.Credentials)
                    {
                        reply = Server.Membership.GetPassword((string)session.RemoteHeaders[IIPAuthPacketHeader.Username],
                                                      (string)session.RemoteHeaders[IIPAuthPacketHeader.Domain]);
                    }
                    else if (session.RemoteMethod == AuthenticationMethod.Token)
                    {
                        reply = Server.Membership.GetToken((ulong)session.RemoteHeaders[IIPAuthPacketHeader.TokenIndex],
                                                      (string)session.RemoteHeaders[IIPAuthPacketHeader.Domain]);
                    }
                    else 
                    {
                        throw new NotImplementedException("Authentication method unsupported.");
                    }

                    reply.Then((pw) =>
                    {
                        if (pw != null)
                        {
                            var localNonce = (byte[])session.LocalHeaders[IIPAuthPacketHeader.Nonce];
                            var remoteNonce = (byte[])session.RemoteHeaders[IIPAuthPacketHeader.Nonce];

                            var hashFunc = SHA256.Create();
                            var hash = hashFunc.ComputeHash((new BinaryList())
                                                                .AddUInt8Array(remoteNonce)
                                                                .AddUInt8Array(pw)
                                                                .AddUInt8Array(localNonce)
                                                                .ToArray());

                            if (hash.SequenceEqual(remoteHash))
                            {
                                // send our hash
                                var localHash = hashFunc.ComputeHash((new BinaryList())
                                                    .AddUInt8Array(localNonce)
                                                    .AddUInt8Array(pw)
                                                    .AddUInt8Array(remoteNonce)
                                                    .ToArray());

                                SendParams()
                                    .AddUInt8((byte)IIPAuthPacketAction.AuthenticateHash)
                                    .AddUInt8((byte)IIPAuthPacketHashAlgorithm.SHA256)
                                    .AddUInt16((ushort)localHash.Length)
                                    .AddUInt8Array(localHash)
                                    .Done();

                                readyToEstablish = true;
                            }
                            else
                            {
                                //Global.Log("auth", LogType.Warning, "U:" + RemoteUsername + " IP:" + Socket.RemoteEndPoint.Address.ToString() + " S:DENIED");
                                SendParams()
                                    .AddUInt8((byte)IIPAuthPacketEvent.ErrorTerminate)
                                    .AddUInt8((byte)ExceptionCode.AccessDenied)
                                    .AddUInt16(13)
                                    .AddString("Access Denied")
                                    .Done();
                            }
                        }
                    });
                }
                catch (Exception ex)
                {
                    var errMsg = DC.ToBytes(ex.Message);

                    SendParams()
                        .AddUInt8((byte)IIPAuthPacketEvent.ErrorTerminate)
                        .AddUInt8((byte)ExceptionCode.GeneralFailure)
                        .AddUInt16((ushort)errMsg.Length)
                        .AddUInt8Array(errMsg)
                        .Done();
                }
            }
            else if (authPacket.Action == IIPAuthPacketAction.IAuthPlain)
            {
                var reference = authPacket.Reference;
                var dataType = authPacket.DataType.Value;

                var (_, parsed) = Codec.Parse(data, dataType.Offset, this, null, dataType);

                var value = parsed.Wait();

                Server.Membership.AuthorizePlain(session, reference, value)
                    .Then(x => ProcessAuthorization(x));


            }
            else if (authPacket.Action == IIPAuthPacketAction.IAuthHashed)
            {
                var reference = authPacket.Reference;
                var value = authPacket.Challenge;
                var algorithm = authPacket.HashAlgorithm;

                Server.Membership.AuthorizeHashed(session, reference, algorithm, value)
                    .Then(x => ProcessAuthorization(x));

            }
            else if (authPacket.Action == IIPAuthPacketAction.IAuthEncrypted)
            {
                var reference = authPacket.Reference;
                var value = authPacket.Challenge;
                var algorithm = authPacket.PublicKeyAlgorithm;

                Server.Membership.AuthorizeEncrypted(session, reference, algorithm, value)
                    .Then(x => ProcessAuthorization(x));
            }
            else if (authPacket.Action == IIPAuthPacketAction.EstablishNewSession)
            {
                if (readyToEstablish)
                {

                    if (Server.Membership == null)
                    {
                        ProcessAuthorization(null);
                    }
                    else
                    {
                        Server.Membership?.Authorize(session).Then(x =>
                        {
                            ProcessAuthorization(x);
                        });
                    }

                    //Global.Log("auth", LogType.Warning, "U:" + RemoteUsername + " IP:" + Socket.RemoteEndPoint.Address.ToString() + " S:AUTH");

                }
                else
                {
                    SendParams()
                        .AddUInt8((byte)IIPAuthPacketEvent.ErrorTerminate)
                        .AddUInt8((byte)ExceptionCode.GeneralFailure)
                        .AddUInt16(9)
                        .AddString("Not ready")
                        .Done();
                }
            }
        }
    }

    internal void ProcessAuthorization(AuthorizationResults results)
    {
        if (results == null || results.Response == Security.Membership.AuthorizationResultsResponse.Success)
        {
            var r = new Random();
            session.Id = new byte[32];
            r.NextBytes(session.Id);
            var accountId = session.AuthorizedAccount.ToBytes();
            
            SendParams()
                .AddUInt8((byte)IIPAuthPacketEvent.IndicationEstablished)
                .AddUInt8((byte)session.Id.Length)
                .AddUInt8Array(session.Id)
                .AddUInt8((byte)accountId.Length)
                .AddUInt8Array(accountId)
                .Done();

            if (this.Instance == null)
            {
                Warehouse.Put(this.GetHashCode().ToString().Replace("/", "_"), this, null, Server).Then(x =>
                {
                    ready = true;
                    Status = ConnectionStatus.Connected;
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
                ready = true;
                Status = ConnectionStatus.Connected;

                openReply?.Trigger(true);
                openReply = null;

                OnReady?.Invoke(this);
                Server?.Membership?.Login(session);
            }
        }
        else if (results.Response == Security.Membership.AuthorizationResultsResponse.Failed)
        {
            SendParams()
                .AddUInt8((byte)IIPAuthPacketEvent.ErrorTerminate)
                .AddUInt8((byte)ExceptionCode.ChallengeFailed)
                .AddUInt16(21)
                .AddString("Authentication failed")
                .Done();
        }
        else if (results.Response == Security.Membership.AuthorizationResultsResponse.Expired)
        {
            SendParams()
                .AddUInt8((byte)IIPAuthPacketEvent.ErrorTerminate)
                .AddUInt8((byte)ExceptionCode.Timeout)
                .AddUInt16(22)
                .AddString("Authentication expired")
                .Done();
        }
        else if (results.Response == Security.Membership.AuthorizationResultsResponse.ServiceUnavailable)
        {
            SendParams()
                .AddUInt8((byte)IIPAuthPacketEvent.ErrorTerminate)
                .AddUInt8((byte)ExceptionCode.GeneralFailure)
                .AddUInt16(19)
                .AddString("Service unavailable")
                .Done();
        }
        else if (results.Response == Security.Membership.AuthorizationResultsResponse.IAuthPlain)
        {
            var args = new Map<IIPAuthPacketIAuthHeader, object>()
            {
                [IIPAuthPacketIAuthHeader.Reference] = results.Reference,
                [IIPAuthPacketIAuthHeader.Destination] = results.Destination,
                [IIPAuthPacketIAuthHeader.Trials] = results.Trials,
                [IIPAuthPacketIAuthHeader.Clue] = results.Clue,
                [IIPAuthPacketIAuthHeader.RequiredFormat] = results.RequiredFormat,
            }.Select(m => new KeyValuePair<byte, object>((byte)m.Key, m.Value));

            SendParams()
                .AddUInt8((byte)IIPAuthPacketEvent.IAuthPlain)
                .AddUInt8Array(Codec.Compose(args, this))
                .Done();

        }
        else if (results.Response == Security.Membership.AuthorizationResultsResponse.IAuthHashed)
        {
            var args = new Map<IIPAuthPacketIAuthHeader, object>()
            {
                [IIPAuthPacketIAuthHeader.Reference] = results.Reference,
                [IIPAuthPacketIAuthHeader.Destination] = results.Destination,
                [IIPAuthPacketIAuthHeader.Expire] = results.Expire,
                //[IIPAuthPacketIAuthHeader.Issue] = results.Issue,
                [IIPAuthPacketIAuthHeader.Clue] = results.Clue,
                [IIPAuthPacketIAuthHeader.RequiredFormat] = results.RequiredFormat,
            }.Select(m => new KeyValuePair<byte, object>((byte)m.Key, m.Value));

            SendParams()
                .AddUInt8((byte)IIPAuthPacketEvent.IAuthHashed)
                .AddUInt8Array(Codec.Compose(args, this))
                .Done();

        }
        else if (results.Response == Security.Membership.AuthorizationResultsResponse.IAuthEncrypted)
        {
            var args = new Map<IIPAuthPacketIAuthHeader, object>()
            {
                [IIPAuthPacketIAuthHeader.Destination] = results.Destination,
                [IIPAuthPacketIAuthHeader.Expire] = results.Expire,
                [IIPAuthPacketIAuthHeader.Clue] = results.Clue,
                [IIPAuthPacketIAuthHeader.RequiredFormat] = results.RequiredFormat,
            }.Select(m => new KeyValuePair<byte, object>((byte)m.Key, m.Value));

            SendParams()
                .AddUInt8((byte)IIPAuthPacketEvent.IAuthEncrypted)
                .AddUInt8Array(Codec.Compose(args, this))
                .Done();
        }
    }

    protected override void DataReceived(NetworkBuffer data)
    {
        //Console.WriteLine("DR " + data.Available + " " + RemoteEndPoint.ToString());
        var msg = data.Read();
        uint offset = 0;
        uint ends = (uint)msg.Length;

        var packs = new List<string>();

        var chunkId = (new Random()).Next(1000, 1000000);

        //var list = new List<Map<string, object>>();// double, IIPPacketCommand>();


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

            if (Username != null // Instance.Attributes.ContainsKey("username")
                  && Password != null)/// Instance.Attributes.ContainsKey("password"))
            {
                return Connect(AuthenticationMethod.Credentials, null, address, port, Username, 0, DC.ToBytes(Password), domain);
            }
            else if (Token != null)
            {
                return Connect(AuthenticationMethod.Token, null, address, port, null, TokenIndex, DC.ToBytes(Token), domain);
            }
            else
            {

                return Connect(AuthenticationMethod.None, null, address, port, null, 0, null, domain);

            }
        }

        return new AsyncReply<bool>(true);
    }




    public AsyncReply<bool> Connect(AuthenticationMethod method = AuthenticationMethod.Certificate, Sockets.ISocket socket = null, string hostname = null, ushort port = 0, string username = null, ulong tokenIndex = 0, byte[] passwordOrToken = null, string domain = null)
    {
        if (openReply != null)
            throw new AsyncException(ErrorType.Exception, 0, "Connection in progress");

        Status = ConnectionStatus.Connecting;

        openReply = new AsyncReply<bool>();

        if (hostname != null)
        {
            session = new Session();
            session.AuthenticationType = AuthenticationType.Client;
            session.LocalMethod = method;
            session.RemoteMethod = AuthenticationMethod.None;

            session.LocalHeaders[IIPAuthPacketHeader.Domain] = domain;
            session.LocalHeaders[IIPAuthPacketHeader.Nonce] = Global.GenerateBytes(32);

            if (method == AuthenticationMethod.Credentials)
            {
                session.LocalHeaders[IIPAuthPacketHeader.Username] = username;
            }
            else if (method == AuthenticationMethod.Token)
            {
                session.LocalHeaders[IIPAuthPacketHeader.TokenIndex] = tokenIndex;
            }
            else if (method == AuthenticationMethod.Certificate)
            {
                throw new NotImplementedException("Unsupported authentication method.");
            }

            localPasswordOrToken = passwordOrToken;
            invalidCredentials = false;
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
                Global.Log("DistributedConnection", LogType.Debug, "Reconnecting socket...");
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

                var toBeRestored = new List<DistributedResource>();
                foreach (KeyValuePair<uint, WeakReference<DistributedResource>> kv in suspendedResources)
                {
                    DistributedResource r;
                    if (kv.Value.TryGetTarget(out r))
                        toBeRestored.Add(r);
                }

                foreach (var r in toBeRestored)
                {

                    var link = DC.ToBytes(r.DistributedResourceLink);

                    Global.Log("DistributedConnection", LogType.Debug, "Restoreing " + r.DistributedResourceLink);

                    try
                    {
                        var ar = await SendRequest(IIPPacketRequest.QueryLink)
                                            .AddUInt16((ushort)link.Length)
                                            .AddUInt8Array(link)
                                            .Done();

                        var dataType = (TransmissionType)ar[0];
                        var data = ar[1] as byte[];

                        if (dataType.Identifier == TransmissionTypeIdentifier.ResourceList)
                        {

                            // remove from suspended.
                            suspendedResources.Remove(r.DistributedResourceInstanceId);

                            // parse them as int
                            var id = data.GetUInt32(8, Endian.Little);

                            // id changed ?
                            if (id != r.DistributedResourceInstanceId)
                                r.DistributedResourceInstanceId = id;

                            neededResources[id] = r;

                            await Fetch(id, null);

                            Global.Log("DistributedConnection", LogType.Debug, "Restored " + id);
                        }
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

    //    AsyncReply<bool> connect({ISocket socket, String hostname, int port, String username, DC password, String domain})

    /// <summary>
    /// Store interface.
    /// </summary>
    /// <param name="resource">Resource.</param>
    /// <returns></returns>
    public AsyncReply<bool> Put(IResource resource)
    {
        if (Codec.IsLocalResource(resource, this))
            neededResources.Add((resource as DistributedResource).DistributedResourceInstanceId, (DistributedResource)resource);
        // else ... send it to the peer
        return new AsyncReply<bool>(true);
    }

    public bool Record(IResource resource, string propertyName, object value, ulong? age, DateTime? dateTime)
    {
        // nothing to do
        return true;
    }

    public bool Modify(IResource resource, string propertyName, object value, ulong? age, DateTime? dateTime)
    {
        // nothing to do
        return true;
    }

    AsyncReply<bool> IStore.AddChild(IResource parent, IResource child)
    {
        // not implemented
        throw new NotImplementedException();
    }

    AsyncReply<bool> IStore.RemoveChild(IResource parent, IResource child)
    {
        // not implemeneted
        throw new NotImplementedException();
    }

    public AsyncReply<bool> AddParent(IResource child, IResource parent)
    {
        throw new NotImplementedException();
    }

    public AsyncReply<bool> RemoveParent(IResource child, IResource parent)
    {
        throw new NotImplementedException();
    }


    public AsyncBag<T> Children<T>(IResource resource, string name) where T : IResource
    {
        throw new Exception("Not implemented");

        //if (Codec.IsLocalResource(resource, this))
        //  return new AsyncBag<T>((resource as DistributedResource).children.Where(x => x.GetType() == typeof(T)).Select(x => (T)x));

        //return null;
    }

    public AsyncBag<T> Parents<T>(IResource resource, string name) where T : IResource
    {
        throw new Exception("Not implemented");
        //if (Codec.IsLocalResource(resource, this))
        //  return (resource as DistributedResource).parents.Where(x => x.GetType() == typeof(T)).Select(x => (T)x);

    }


    protected override void Connected()
    {
        if (session.AuthenticationType == AuthenticationType.Client)
            Declare();
    }

    protected override void Disconencted()
    {
        // clean up
        ready = false;
        readyToEstablish = false;
        Status = ConnectionStatus.Closed;

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

        foreach (var x in templateRequests.Values)
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
        templateRequests.Clear();


        foreach (var x in attachedResources.Values)
        {
            DistributedResource r;
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
            Warehouse.Remove(this);

            if (ready)
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

}
