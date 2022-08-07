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
using Esiur.Net.Packets;
using Esiur.Resource;
using Esiur.Security.Authority;
using Esiur.Resource.Template;
using System.Linq;
using System.Diagnostics;
using static Esiur.Net.Packets.IIPPacket;
using Esiur.Net.HTTP;
using System.Timers;

namespace Esiur.Net.IIP;
public partial class DistributedConnection : NetworkConnection, IStore
{
    public delegate void ReadyEvent(DistributedConnection sender);
    public delegate void ErrorEvent(DistributedConnection sender, byte errorCode, string errorMessage);


    Timer keepAliveTimer;

    /// <summary>
    /// Ready event is raised when the connection is fully established.
    /// </summary>
    public event ReadyEvent OnReady;

    /// <summary>
    /// Error event
    /// </summary>
    public event ErrorEvent OnError;



    IIPPacket packet = new IIPPacket();
    IIPAuthPacket authPacket = new IIPAuthPacket();

    Session session;

    AsyncReply<bool> openReply;

    byte[] localPasswordOrToken;
    byte[] localNonce, remoteNonce;

    bool ready, readyToEstablish;

    string _hostname;
    ushort _port;

    bool initialPacket = true;

    DateTime loginDate;



    /// <summary>
    /// Local username to authenticate ourselves.  
    /// </summary>
    public string LocalUsername => session.LocalAuthentication.Username;// { get; set; }

    /// <summary>
    /// Peer's username.
    /// </summary>
    public string RemoteUsername => session.RemoteAuthentication.Username;// { get; set; }

    /// <summary>
    /// Working domain.
    /// </summary>
    //public string Domain { get { return domain; } }


    /// <summary>
    /// The session related to this connection.
    /// </summary>
    public Session Session => session;

    /// <summary>
    /// Distributed server responsible for this connection, usually for incoming connections.
    /// </summary>
    public DistributedServer Server { get; internal set; }

    public bool Remove(IResource resource)
    {
        // nothing to do
        return true;
    }

    /// <summary>
    /// Send data to the other end as parameters
    /// </summary>
    /// <param name="values">Values will be converted to bytes then sent.</param>
    internal SendList SendParams(AsyncReply<object[]> reply = null)//params object[] values)
    {
        return new SendList(this, reply);

        /*
        var data = BinaryList.ToBytes(values);

        if (ready)
        {
            var cmd = (IIPPacketCommand)(data[0] >> 6);

            if (cmd == IIPPacketCommand.Event)
            {
                var evt = (IIPPacketEvent)(data[0] & 0x3f);
                //Console.Write("Sent: " + cmd.ToString() + " " + evt.ToString());
            }
            else if (cmd == IIPPacketCommand.Report)
            {
                var r = (IIPPacketReport)(data[0] & 0x3f);
                //Console.Write("Sent: " + cmd.ToString() + " " + r.ToString());

            }
            else
            {
                var act = (IIPPacketAction)(data[0] & 0x3f);
                //Console.Write("Sent: " + cmd.ToString() + " " + act.ToString());

            }

            //foreach (var param in values)
            //    Console.Write(", " + param);

            //Console.WriteLine();
        }


        Send(data);

        //StackTrace stackTrace = new StackTrace(;

        // Get calling method name

        //Console.WriteLine("TX " + hostType + " " + ar.Length + " " + stackTrace.GetFrame(1).GetMethod().ToString());
        */
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

        session.RemoteAuthentication.Source.Attributes[SourceAttributeType.IPv4] = socket.RemoteEndPoint.Address;
        session.RemoteAuthentication.Source.Attributes[SourceAttributeType.Port] = socket.RemoteEndPoint.Port;
        session.LocalAuthentication.Source.Attributes[SourceAttributeType.IPv4] = socket.LocalEndPoint.Address;
        session.LocalAuthentication.Source.Attributes[SourceAttributeType.Port] = socket.LocalEndPoint.Port;

        if (socket.State == SocketState.Established &&
            session.LocalAuthentication.Type == AuthenticationType.Client)
        {
            Declare();
        }
    }

    private void Declare()
    {
        var dmn = DC.ToBytes(session.LocalAuthentication.Domain);// domain);

        if (session.LocalAuthentication.Method == AuthenticationMethod.Credentials)
        {
            // declare (Credentials -> No Auth, No Enctypt)

            var un = DC.ToBytes(session.LocalAuthentication.Username);

            SendParams()
                .AddUInt8(0x60)
                .AddUInt8((byte)dmn.Length)
                .AddUInt8Array(dmn)
                .AddUInt8Array(localNonce)
                .AddUInt8((byte)un.Length)
                .AddUInt8Array(un)
                .Done();//, dmn, localNonce, (byte)un.Length, un);
        }
        else if (session.LocalAuthentication.Method == AuthenticationMethod.Token)
        {

            SendParams()
                .AddUInt8(0x70)
                .AddUInt8((byte)dmn.Length)
                .AddUInt8Array(dmn)
                .AddUInt8Array(localNonce)
                .AddUInt64(session.LocalAuthentication.TokenIndex)
                .Done();//, dmn, localNonce, token

        }
        else if (session.LocalAuthentication.Method == AuthenticationMethod.None)
        {
            SendParams()
                .AddUInt8(0x40)
                .AddUInt8((byte)dmn.Length)
                .AddUInt8Array(dmn)
                .Done();//, dmn, localNonce, token
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
        this.session = new Session(new ClientAuthentication()
                                    , new HostAuthentication());
        //Instance.Name = Global.GenerateCode(12);
        //this.hostType = AuthenticationType.Client;
        //this.domain = domain;
        //this.localUsername = username;
        session.LocalAuthentication.Domain = domain;
        session.LocalAuthentication.Username = username;
        session.LocalAuthentication.Method = AuthenticationMethod.Credentials;
        this.localPasswordOrToken = DC.ToBytes(password);

        init();

        Assign(socket);
    }

    public DistributedConnection(Sockets.ISocket socket, string domain, ulong tokenIndex, string token)
    {
        this.session = new Session(new ClientAuthentication()
                                    , new HostAuthentication());
        //Instance.Name = Global.GenerateCode(12);
        //this.hostType = AuthenticationType.Client;
        //this.domain = domain;
        //this.localUsername = username;
        session.LocalAuthentication.Domain = domain;
        session.LocalAuthentication.TokenIndex = tokenIndex;
        session.LocalAuthentication.Method = AuthenticationMethod.Token;

        this.localPasswordOrToken = DC.ToBytes(token);

        init();

        Assign(socket);
    }


    /// <summary>
    /// Create a new instance of a distributed connection
    /// </summary>
    public DistributedConnection()
    {
        //myId = Global.GenerateCode(12);
        // localParams.Host = DistributedParameters.HostType.Host;
        session = new Session(new HostAuthentication(), new ClientAuthentication());
        init();
    }



    public string Link(IResource resource)
    {
        if (resource is DistributedResource)
        {
            var r = resource as DistributedResource;
            if (r.Instance.Store == this)
                return this.Instance.Name + "/" + r.Id;
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


        var r = new Random();
        localNonce = new byte[32];
        r.NextBytes(localNonce);

        keepAliveTimer = new Timer(KeepAliveInterval * 1000);
        keepAliveTimer.Elapsed += KeepAliveTimer_Elapsed;
    }

    [Public] public virtual uint Jitter { get; set; }

    public uint KeepAliveTime { get; set; } = 10;

    DateTime? lastKeepAliveSent;

    private void KeepAliveTimer_Elapsed(object sender, ElapsedEventArgs e)
    {
        if (!IsConnected)
            return;


        keepAliveTimer.Stop();

        var now = DateTime.UtcNow;

        uint interval = lastKeepAliveSent == null ? 0 :
                        (uint)(now - (DateTime)lastKeepAliveSent).TotalMilliseconds;

        lastKeepAliveSent = now;

        SendRequest(IIPPacketAction.KeepAlive)
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
        //var packet = new IIPPacket();



        // packets++;

        if (ready)
        {
            var rt = packet.Parse(msg, offset, ends);
            //Console.WriteLine("Rec: " + chunkId + " " + packet.ToString());

            /*
            if (packet.Command == IIPPacketCommand.Event)
                Console.WriteLine("Rec: " + packet.Command.ToString() + " " + packet.Event.ToString());
            else if (packet.Command == IIPPacketCommand.Report)
                Console.WriteLine("Rec: " + packet.Command.ToString() + " " + packet.Report.ToString());
            else
                Console.WriteLine("Rec: " + packet.Command.ToString() + " " + packet.Action.ToString() + " " + packet.ResourceId + " " + offset + "/" + ends);
              */


            //packs.Add(packet.Command.ToString() + " " + packet.Action.ToString() + " " + packet.Event.ToString());

            //if (packs.Count > 1)
            //  Console.WriteLine("P2");

            //Console.WriteLine("");

            if (rt <= 0)
            {
                //Console.WriteLine("Hold");
                var size = ends - offset;
                data.HoldFor(msg, offset, size, size + (uint)(-rt));
                return ends;
            }
            else
            {

                //Console.WriteLine($"CMD {packet.Command} {offset} {ends}");

                offset += (uint)rt;

                if (packet.Command == IIPPacket.IIPPacketCommand.Event)
                {
                    switch (packet.Event)
                    {
                        case IIPPacket.IIPPacketEvent.ResourceReassigned:
                            IIPEventResourceReassigned(packet.ResourceId, packet.NewResourceId);
                            break;
                        case IIPPacket.IIPPacketEvent.ResourceDestroyed:
                            IIPEventResourceDestroyed(packet.ResourceId);
                            break;
                        case IIPPacket.IIPPacketEvent.PropertyUpdated:
                            IIPEventPropertyUpdated(packet.ResourceId, packet.MethodIndex, (TransmissionType)packet.DataType, msg);// packet.Content);
                            break;
                        case IIPPacket.IIPPacketEvent.EventOccurred:
                            IIPEventEventOccurred(packet.ResourceId, packet.MethodIndex, (TransmissionType)packet.DataType, msg);//packet.Content);
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
                else if (packet.Command == IIPPacket.IIPPacketCommand.Request)
                {
                    switch (packet.Action)
                    {
                        // Manage
                        case IIPPacket.IIPPacketAction.AttachResource:
                            IIPRequestAttachResource(packet.CallbackId, packet.ResourceId);
                            break;
                        case IIPPacket.IIPPacketAction.ReattachResource:
                            IIPRequestReattachResource(packet.CallbackId, packet.ResourceId, packet.ResourceAge);
                            break;
                        case IIPPacket.IIPPacketAction.DetachResource:
                            IIPRequestDetachResource(packet.CallbackId, packet.ResourceId);
                            break;
                        case IIPPacket.IIPPacketAction.CreateResource:
                            //@TODO : fix this
                            //IIPRequestCreateResource(packet.CallbackId, packet.StoreId, packet.ResourceId, packet.Content);
                            break;
                        case IIPPacket.IIPPacketAction.DeleteResource:
                            IIPRequestDeleteResource(packet.CallbackId, packet.ResourceId);
                            break;
                        case IIPPacketAction.AddChild:
                            IIPRequestAddChild(packet.CallbackId, packet.ResourceId, packet.ChildId);
                            break;
                        case IIPPacketAction.RemoveChild:
                            IIPRequestRemoveChild(packet.CallbackId, packet.ResourceId, packet.ChildId);
                            break;
                        case IIPPacketAction.RenameResource:
                            IIPRequestRenameResource(packet.CallbackId, packet.ResourceId, packet.ResourceName);
                            break;

                        // Inquire
                        case IIPPacket.IIPPacketAction.TemplateFromClassName:
                            IIPRequestTemplateFromClassName(packet.CallbackId, packet.ClassName);
                            break;
                        case IIPPacket.IIPPacketAction.TemplateFromClassId:
                            IIPRequestTemplateFromClassId(packet.CallbackId, packet.ClassId);
                            break;
                        case IIPPacket.IIPPacketAction.TemplateFromResourceId:
                            IIPRequestTemplateFromResourceId(packet.CallbackId, packet.ResourceId);
                            break;
                        case IIPPacketAction.QueryLink:
                            IIPRequestQueryResources(packet.CallbackId, packet.ResourceLink);
                            break;

                        case IIPPacketAction.ResourceChildren:
                            IIPRequestResourceChildren(packet.CallbackId, packet.ResourceId);
                            break;
                        case IIPPacketAction.ResourceParents:
                            IIPRequestResourceParents(packet.CallbackId, packet.ResourceId);
                            break;

                        case IIPPacket.IIPPacketAction.ResourceHistory:
                            IIPRequestInquireResourceHistory(packet.CallbackId, packet.ResourceId, packet.FromDate, packet.ToDate);
                            break;

                        case IIPPacketAction.LinkTemplates:
                            IIPRequestLinkTemplates(packet.CallbackId, packet.ResourceLink);
                            break;

                        // Invoke
                        case IIPPacket.IIPPacketAction.InvokeFunction:
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

                        case IIPPacket.IIPPacketAction.Listen:
                            IIPRequestListen(packet.CallbackId, packet.ResourceId, packet.MethodIndex);
                            break;

                        case IIPPacket.IIPPacketAction.Unlisten:
                            IIPRequestUnlisten(packet.CallbackId, packet.ResourceId, packet.MethodIndex);
                            break;

                        case IIPPacket.IIPPacketAction.SetProperty:
                            IIPRequestSetProperty(packet.CallbackId, packet.ResourceId, packet.MethodIndex, (TransmissionType)packet.DataType, msg);
                            break;

                        // Attribute
                        case IIPPacketAction.GetAllAttributes:
                            // @TODO : fix this
                            //IIPRequestGetAttributes(packet.CallbackId, packet.ResourceId, packet.Content, true);
                            break;
                        case IIPPacketAction.UpdateAllAttributes:
                            // @TODO : fix this
                            //IIPRequestUpdateAttributes(packet.CallbackId, packet.ResourceId, packet.Content, true);
                            break;
                        case IIPPacketAction.ClearAllAttributes:
                            // @TODO : fix this
                            //IIPRequestClearAttributes(packet.CallbackId, packet.ResourceId, packet.Content, true);
                            break;
                        case IIPPacketAction.GetAttributes:
                            // @TODO : fix this
                            //IIPRequestGetAttributes(packet.CallbackId, packet.ResourceId, packet.Content, false);
                            break;
                        case IIPPacketAction.UpdateAttributes:
                            // @TODO : fix this
                            //IIPRequestUpdateAttributes(packet.CallbackId, packet.ResourceId, packet.Content, false);
                            break;
                        case IIPPacketAction.ClearAttributes:
                            // @TODO : fix this
                            //IIPRequestClearAttributes(packet.CallbackId, packet.ResourceId, packet.Content, false);
                            break;

                        case IIPPacketAction.KeepAlive:
                            IIPRequestKeepAlive(packet.CallbackId, packet.CurrentTime, packet.Interval);
                            break;

                        case IIPPacketAction.ProcedureCall:
                            IIPRequestProcedureCall(packet.CallbackId, packet.Procedure, (TransmissionType)packet.DataType, msg);
                            break;

                        case IIPPacketAction.StaticCall:
                            IIPRequestStaticCall(packet.CallbackId, packet.ClassId, packet.MethodIndex, (TransmissionType)packet.DataType, msg);
                            break;

                    }
                }
                else if (packet.Command == IIPPacket.IIPPacketCommand.Reply)
                {
                    switch (packet.Action)
                    {
                        // Manage
                        case IIPPacket.IIPPacketAction.AttachResource:
                            IIPReply(packet.CallbackId, packet.ClassId, packet.ResourceAge, packet.ResourceLink, packet.DataType, msg);
                            break;

                        case IIPPacket.IIPPacketAction.ReattachResource:
                            IIPReply(packet.CallbackId, packet.ResourceAge, packet.DataType, msg);

                            break;
                        case IIPPacket.IIPPacketAction.DetachResource:
                            IIPReply(packet.CallbackId);
                            break;

                        case IIPPacket.IIPPacketAction.CreateResource:
                            IIPReply(packet.CallbackId, packet.ResourceId);
                            break;

                        case IIPPacket.IIPPacketAction.DeleteResource:
                        case IIPPacketAction.AddChild:
                        case IIPPacketAction.RemoveChild:
                        case IIPPacketAction.RenameResource:
                            IIPReply(packet.CallbackId);
                            break;

                        // Inquire

                        case IIPPacket.IIPPacketAction.TemplateFromClassName:
                        case IIPPacket.IIPPacketAction.TemplateFromClassId:
                        case IIPPacket.IIPPacketAction.TemplateFromResourceId:

                            var content = msg.Clip(packet.DataType.Value.Offset, (uint)packet.DataType.Value.ContentLength);
                            IIPReply(packet.CallbackId, TypeTemplate.Parse(content));
                            break;

                        case IIPPacketAction.QueryLink:
                        case IIPPacketAction.ResourceChildren:
                        case IIPPacketAction.ResourceParents:
                        case IIPPacketAction.ResourceHistory:
                        case IIPPacketAction.LinkTemplates:
                            IIPReply(packet.CallbackId, (TransmissionType)packet.DataType, msg);// packet.Content);
                            break;

                        // Invoke
                        case IIPPacketAction.InvokeFunction:
                        case IIPPacketAction.StaticCall:
                        case IIPPacketAction.ProcedureCall:
                            IIPReplyInvoke(packet.CallbackId, (TransmissionType)packet.DataType, msg);// packet.Content);
                            break;

                        //case IIPPacket.IIPPacketAction.GetProperty:
                        //    IIPReply(packet.CallbackId, packet.Content);
                        //    break;

                        //case IIPPacket.IIPPacketAction.GetPropertyIfModified:
                        //    IIPReply(packet.CallbackId, packet.Content);
                        //    break;

                        case IIPPacketAction.Listen:
                        case IIPPacketAction.Unlisten:
                        case IIPPacketAction.SetProperty:
                            IIPReply(packet.CallbackId);
                            break;

                        // Attribute
                        case IIPPacketAction.GetAllAttributes:
                        case IIPPacketAction.GetAttributes:
                            IIPReply(packet.CallbackId, (TransmissionType)packet.DataType, msg);// packet.Content);
                            break;

                        case IIPPacketAction.UpdateAllAttributes:
                        case IIPPacketAction.UpdateAttributes:
                        case IIPPacketAction.ClearAllAttributes:
                        case IIPPacketAction.ClearAttributes:
                            IIPReply(packet.CallbackId);
                            break;

                        case IIPPacketAction.KeepAlive:
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
                            IIPReportChunk(packet.CallbackId, (TransmissionType)packet.DataType, msg);// packet.Content);

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


                            res.Compose(HTTPResponsePacket.ComposeOptions.AllCalculateLength);
                            Send(res.Data);
                            // replace my socket with websockets
                            var tcpSocket = this.Unassign();
                            var wsSocket = new WSocket(tcpSocket);
                            this.Assign(wsSocket);
                        }
                        else
                        {

                            var res = new HTTPResponsePacket();
                            res.Number = HTTPResponsePacket.ResponseCode.BadRequest;
                            res.Compose(HTTPResponsePacket.ComposeOptions.AllCalculateLength);
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

            //Console.WriteLine(msg.GetString(offset, ends - offset));

            var rt = authPacket.Parse(msg, offset, ends);

            //Console.WriteLine(session.LocalAuthentication.Type.ToString() + " " + offset + " " + ends + " " + rt + " " + authPacket.ToString());

            if (rt <= 0)
            {
                data.HoldFor(msg, ends + (uint)(-rt));
                return ends;
            }
            else
            {
                offset += (uint)rt;

                if (session.LocalAuthentication.Type == AuthenticationType.Host)
                {
                    if (authPacket.Command == IIPAuthPacket.IIPAuthPacketCommand.Declare)
                    {
                        session.RemoteAuthentication.Method = authPacket.RemoteMethod;

                        if (authPacket.RemoteMethod == AuthenticationMethod.Credentials && authPacket.LocalMethod == AuthenticationMethod.None)
                        {
                            try
                            {
                                if (Server.Membership == null)
                                {
                                    var errMsg = DC.ToBytes("Membership not set.");

                                    SendParams().AddUInt8(0xc0)
                                        .AddUInt8((byte)ExceptionCode.GeneralFailure)
                                        .AddUInt16((ushort)errMsg.Length)
                                        .AddUInt8Array(errMsg).Done();
                                }
                                else Server.Membership.UserExists(authPacket.RemoteUsername, authPacket.Domain).Then(x =>
                                {
                                    if (x)
                                    {
                                        session.RemoteAuthentication.Username = authPacket.RemoteUsername;
                                        remoteNonce = authPacket.RemoteNonce;
                                        session.RemoteAuthentication.Domain = authPacket.Domain;
                                        SendParams()
                                                    .AddUInt8(0xa0)
                                                    .AddUInt8Array(localNonce)
                                                    .Done();
                                        //SendParams((byte)0xa0, localNonce);
                                    }
                                    else
                                    {
                                        //Console.WriteLine("User not found");
                                        SendParams().AddUInt8(0xc0)
                                                    .AddUInt8((byte)ExceptionCode.UserOrTokenNotFound)
                                                    .AddUInt16(14)
                                                    .AddString("User not found").Done();
                                    }
                                });
                            }
                            catch (Exception ex)
                            {
                                var errMsg = DC.ToBytes(ex.Message);

                                SendParams().AddUInt8(0xc0)
                                    .AddUInt8((byte)ExceptionCode.GeneralFailure)
                                    .AddUInt16((ushort)errMsg.Length)
                                    .AddUInt8Array(errMsg).Done();
                            }
                        }
                        else if (authPacket.RemoteMethod == AuthenticationMethod.Token && authPacket.LocalMethod == AuthenticationMethod.None)
                        {
                            try
                            {
                                if (Server.Membership == null)
                                {
                                    SendParams()
                                                        .AddUInt8(0xc0)
                                                        .AddUInt8((byte)ExceptionCode.UserOrTokenNotFound)
                                                        .AddUInt16(15)
                                                        .AddString("Token not found")
                                                        .Done();
                                }
                                // Check if user and token exists
                                else
                                {
                                    Server.Membership.TokenExists(authPacket.RemoteTokenIndex, authPacket.Domain).Then(x =>
                                    {
                                        if (x != null)
                                        {
                                            session.RemoteAuthentication.Username = x;
                                            session.RemoteAuthentication.TokenIndex = authPacket.RemoteTokenIndex;
                                            remoteNonce = authPacket.RemoteNonce;
                                            session.RemoteAuthentication.Domain = authPacket.Domain;
                                            SendParams()
                                                        .AddUInt8(0xa0)
                                                        .AddUInt8Array(localNonce)
                                                        .Done();
                                        }
                                        else
                                        {
                                            //Console.WriteLine("User not found");
                                            SendParams()
                                                        .AddUInt8(0xc0)
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
                                var errMsg = DC.ToBytes(ex.Message);

                                SendParams()
                                    .AddUInt8(0xc0)
                                    .AddUInt8((byte)ExceptionCode.GeneralFailure)
                                    .AddUInt16((ushort)errMsg.Length)
                                    .AddUInt8Array(errMsg)
                                    .Done();
                            }
                        }
                        else if (authPacket.RemoteMethod == AuthenticationMethod.None && authPacket.LocalMethod == AuthenticationMethod.None)
                        {
                            try
                            {
                                // Check if guests are allowed
                                if (Server.Membership?.GuestsAllowed ?? true)
                                {
                                    session.RemoteAuthentication.Username = "g-" + Global.GenerateCode();
                                    session.RemoteAuthentication.Domain = authPacket.Domain;
                                    readyToEstablish = true;
                                    SendParams()
                                                .AddUInt8(0x80)
                                                .Done();
                                }
                                else
                                {
                                    SendParams()
                                                .AddUInt8(0xc0)
                                                .AddUInt8((byte)ExceptionCode.AccessDenied)
                                                .AddUInt16(18)
                                                .AddString("Guests not allowed")
                                                .Done();
                                }
                            }
                            catch (Exception ex)
                            {
                                var errMsg = DC.ToBytes(ex.Message);

                                SendParams().AddUInt8(0xc0)
                                    .AddUInt8((byte)ExceptionCode.GeneralFailure)
                                    .AddUInt16((ushort)errMsg.Length)
                                    .AddUInt8Array(errMsg).Done();
                            }
                        }

                    }
                    else if (authPacket.Command == IIPAuthPacket.IIPAuthPacketCommand.Action)
                    {
                        if (authPacket.Action == IIPAuthPacket.IIPAuthPacketAction.AuthenticateHash)
                        {
                            var remoteHash = authPacket.Hash;
                            AsyncReply<byte[]> reply = null;

                            try
                            {
                                if (session.RemoteAuthentication.Method == AuthenticationMethod.Credentials)
                                {
                                    reply = Server.Membership.GetPassword(session.RemoteAuthentication.Username,
                                                                  session.RemoteAuthentication.Domain);
                                }
                                else if (session.RemoteAuthentication.Method == AuthenticationMethod.Token)
                                {
                                    reply = Server.Membership.GetToken(session.RemoteAuthentication.TokenIndex,
                                                                  session.RemoteAuthentication.Domain);
                                }
                                else
                                {
                                    // Error
                                }

                                reply.Then((pw) =>
                                {
                                    if (pw != null)
                                    {
                                        var hashFunc = SHA256.Create();
                                        //var hash = hashFunc.ComputeHash(BinaryList.ToBytes(pw, remoteNonce, localNonce));
                                        var hash = hashFunc.ComputeHash((new BinaryList())
                                                                            .AddUInt8Array(pw)
                                                                            .AddUInt8Array(remoteNonce)
                                                                            .AddUInt8Array(localNonce)
                                                                            .ToArray());
                                        if (hash.SequenceEqual(remoteHash))
                                        {
                                            // send our hash
                                            //var localHash = hashFunc.ComputeHash(BinaryList.ToBytes(localNonce, remoteNonce, pw));
                                            //SendParams((byte)0, localHash);

                                            var localHash = hashFunc.ComputeHash((new BinaryList()).AddUInt8Array(localNonce).AddUInt8Array(remoteNonce).AddUInt8Array(pw).ToArray());
                                            SendParams().AddUInt8(0).AddUInt8Array(localHash).Done();

                                            readyToEstablish = true;
                                        }
                                        else
                                        {
                                            //Global.Log("auth", LogType.Warning, "U:" + RemoteUsername + " IP:" + Socket.RemoteEndPoint.Address.ToString() + " S:DENIED");
                                            SendParams().AddUInt8(0xc0)
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

                                SendParams().AddUInt8(0xc0)
                                    .AddUInt8((byte)ExceptionCode.GeneralFailure)
                                    .AddUInt16((ushort)errMsg.Length)
                                    .AddUInt8Array(errMsg).Done();
                            }
                        }
                        else if (authPacket.Action == IIPAuthPacket.IIPAuthPacketAction.NewConnection)
                        {
                            if (readyToEstablish)
                            {
                                var r = new Random();
                                session.Id = new byte[32];
                                r.NextBytes(session.Id);
                                //SendParams((byte)0x28, session.Id);
                                SendParams().AddUInt8(0x28)
                                            .AddUInt8Array(session.Id)
                                            .Done();

                                if (this.Instance == null)
                                {
                                    Warehouse.Put(this.RemoteUsername, this, null, Server).Then(x =>
                                    {
                                        ready = true;
                                        openReply?.Trigger(true);
                                        OnReady?.Invoke(this);

                                        Server?.Membership?.Login(session);
                                        loginDate = DateTime.Now;

                                    }).Error(x =>
                                    {
                                        openReply?.TriggerError(x);
                                    });
                                }
                                else
                                {
                                    ready = true;
                                    openReply?.Trigger(true);
                                    OnReady?.Invoke(this);
                                    Server?.Membership?.Login(session);
                                }

                                //Global.Log("auth", LogType.Warning, "U:" + RemoteUsername + " IP:" + Socket.RemoteEndPoint.Address.ToString() + " S:AUTH");

                            }
                            else
                            {
                                SendParams()
                                    .AddUInt8(0xc0)
                                    .AddUInt8((byte)ExceptionCode.GeneralFailure)
                                    .AddUInt16(9)
                                    .AddString("Not ready")
                                    .Done();
                            }
                        }
                    }
                }
                else if (session.LocalAuthentication.Type == AuthenticationType.Client)
                {
                    if (authPacket.Command == IIPAuthPacket.IIPAuthPacketCommand.Acknowledge)
                    {
                        if (authPacket.RemoteMethod == AuthenticationMethod.None)
                        {
                            // send establish
                            SendParams()
                                        .AddUInt8(0x20)
                                        .AddUInt16(0)
                                        .Done();
                        }
                        else if (authPacket.RemoteMethod == AuthenticationMethod.Credentials
                                || authPacket.RemoteMethod == AuthenticationMethod.Token)
                        {
                            remoteNonce = authPacket.RemoteNonce;

                            // send our hash
                            var hashFunc = SHA256.Create();
                            //var localHash = hashFunc.ComputeHash(BinaryList.ToBytes(localPassword, localNonce, remoteNonce));
                            var localHash = hashFunc.ComputeHash(new BinaryList()
                                                                .AddUInt8Array(localPasswordOrToken)
                                                                .AddUInt8Array(localNonce)
                                                                .AddUInt8Array(remoteNonce)
                                                                .ToArray());

                            SendParams()
                                .AddUInt8(0)
                                .AddUInt8Array(localHash)
                                .Done();
                        }
                        //SendParams((byte)0, localHash);
                    }
                    else if (authPacket.Command == IIPAuthPacket.IIPAuthPacketCommand.Action)
                    {
                        if (authPacket.Action == IIPAuthPacket.IIPAuthPacketAction.AuthenticateHash)
                        {
                            // check if the server knows my password
                            var hashFunc = SHA256.Create();
                            //var remoteHash = hashFunc.ComputeHash(BinaryList.ToBytes(remoteNonce, localNonce, localPassword));
                            var remoteHash = hashFunc.ComputeHash(new BinaryList()
                                                                    .AddUInt8Array(remoteNonce)
                                                                    .AddUInt8Array(localNonce)
                                                                    .AddUInt8Array(localPasswordOrToken)
                                                                    .ToArray());


                            if (remoteHash.SequenceEqual(authPacket.Hash))
                            {
                                // send establish request
                                SendParams()
                                            .AddUInt8(0x20)
                                            .AddUInt16(0)
                                            .Done();
                            }
                            else
                            {
                                SendParams()
                                            .AddUInt8(0xc0)
                                            .AddUInt8((byte)ExceptionCode.ChallengeFailed)
                                            .AddUInt16(16)
                                            .AddString("Challenge Failed")
                                            .Done();

                                //SendParams((byte)0xc0, 1, (ushort)5, DC.ToBytes("Error"));
                            }
                        }
                        else if (authPacket.Action == IIPAuthPacket.IIPAuthPacketAction.ConnectionEstablished)
                        {
                            session.Id = authPacket.SessionId;

                            ready = true;
                            // put it in the warehouse

                            if (this.Instance == null)
                            {
                                Warehouse.Put(this.LocalUsername, this, null, Server).Then(x =>
                                {
                                    openReply?.Trigger(true);
                                    OnReady?.Invoke(this);

                                }).Error(x => openReply?.TriggerError(x));
                            }
                            else
                            {
                                openReply?.Trigger(true);
                                OnReady?.Invoke(this);
                            }

                            // start perodic keep alive timer
                            keepAliveTimer.Start();
                        }
                    }
                    else if (authPacket.Command == IIPAuthPacket.IIPAuthPacketCommand.Error)
                    {
                        openReply?.TriggerError(new AsyncException(ErrorType.Management, authPacket.ErrorCode, authPacket.ErrorMessage));
                        OnError?.Invoke(this, authPacket.ErrorCode, authPacket.ErrorMessage);
                        Close();
                    }
                }
            }
        }

        return offset;

        //if (offset < ends)
        //  processPacket(msg, offset, ends, data, chunkId);
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
            Console.WriteLine(ex);
        }
        finally
        {
            this.Socket?.Unhold();
        }
    }


    [Attribute]
    public string Username { get; set; }

    [Attribute]
    public string Password { get; set; }

    [Attribute]
    public string Token { get; set; }

    [Attribute]
    public ulong TokenIndex { get; set; }

    [Attribute]
    public string Domain { get; set; }
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

        openReply = new AsyncReply<bool>();

        if (hostname != null)
        {
            session = new Session(new ClientAuthentication()
                                      , new HostAuthentication());

            session.LocalAuthentication.Method = method;
            session.LocalAuthentication.TokenIndex = tokenIndex;
            session.LocalAuthentication.Domain = domain;
            session.LocalAuthentication.Username = username;
            localPasswordOrToken = passwordOrToken;
            //localPassword = password;
        }

        if (session == null)
            throw new AsyncException(ErrorType.Exception, 0, "Session not initialized");

        if (socket == null)
            socket = new TCPSocket();

        if (port > 0)
            this._port = port;
        if (hostname != null)
            this._hostname = hostname;

        socket.Connect(this._hostname, this._port).Then(x =>
        {
            Assign(socket);
        }).Error((x) =>
        {
            openReply.TriggerError(x);
            openReply = null;
        });

        return openReply;
    }

    public async AsyncReply<bool> Reconnect()
    {
        try
        {
            if (await Connect())
            {
                try
                {
                    var bag = new AsyncBag<IResource>();

                    for (var i = 0; i < resources.Keys.Count; i++)
                    {
                        var index = resources.Keys.ElementAt(i);
                        bag.Add(Fetch(index, null));
                    }

                    bag.Seal();
                    await bag;
                }
                catch (Exception ex)
                {
                    Global.Log(ex);
                    //print(ex.toString());
                }
            }
        }
        catch
        {
            return false;
        }

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
            resources.Add((resource as DistributedResource).Id, (DistributedResource)resource);
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
        if (session.LocalAuthentication.Type == AuthenticationType.Client)
            Declare();
    }

    protected override void Disconencted()
    {
        // clean up
        readyToEstablish = false;

        keepAliveTimer.Stop();


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
                x.TriggerError(new AsyncException(ErrorType.Management, 0, "Connection closed"));
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



        if (Server != null) {
            foreach (var x in resources.Values)
                x.Suspend();
            UnsubscribeAll();
            Warehouse.Remove(this);

            if (ready)
                Server.Membership?.Logout(session);
        };


        ready = false;
    }

    /*
    public AsyncBag<T> Children<T>(IResource resource)
    {
        if (Codec.IsLocalReso turce(resource, this))
            return (resource as DistributedResource).children.Where(x => x.GetType() == typeof(T)).Select(x => (T)x);

        return null;
    }

    public AsyncBag<T> Parents<T>(IResource resource)
    {
        if (Codec.IsLocalResource(resource, this))
            return (resource as DistributedResource).parents.Where(x => x.GetType() == typeof(T)).Select(x => (T)x);

        return null;
    }
    */

}
