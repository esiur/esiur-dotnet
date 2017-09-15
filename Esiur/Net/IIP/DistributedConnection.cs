using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using Esiur.Net.Sockets;
using Esiur.Data;
using Esiur.Misc;
using Esiur.Engine;
using Esiur.Net.Packets;
using Esiur.Resource;
using Esiur.Security.Authority;
using Esiur.Resource.Template;
using System.Linq;
using System.Diagnostics;

namespace Esiur.Net.IIP
{
    public partial class DistributedConnection : NetworkConnection, IStore
    {
        public delegate void ReadyEvent(DistributedConnection sender);
        public delegate void ErrorEvent(DistributedConnection sender, byte errorCode, string errorMessage);

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

        byte[] sessionId;
        AuthenticationType hostType;
        string domain;
        string localUsername, remoteUsername;
        byte[] localPassword;

        byte[] localNonce, remoteNonce;

        bool ready, readyToEstablish;

        DateTime loginDate;
        KeyList<string, object> variables = new KeyList<string, object>();

        /// <summary>
        /// Local username to authenticate ourselves.  
        /// </summary>
        public string LocalUsername { get; set; }

        /// <summary>
        /// Peer's username.
        /// </summary>
        public string RemoteUsername { get; set; }

        /// <summary>
        /// Working domain.
        /// </summary>
        public string Domain { get { return domain; } }


        /// <summary>
        /// Distributed server responsible for this connection, usually for incoming connections.
        /// </summary>
        public DistributedServer Server
        {
            get;
            set;
        }

        /// <summary>
        /// Send data to the other end as parameters
        /// </summary>
        /// <param name="values">Values will be converted to bytes then sent.</param>
        internal void SendParams(params object[] values)
        {
            var ar = BinaryList.ToBytes(values);
            Send(ar);

            //StackTrace stackTrace = new StackTrace(;

            // Get calling method name

            //Console.WriteLine("TX " + hostType + " " + ar.Length + " " + stackTrace.GetFrame(1).GetMethod().ToString());
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
        public KeyList<string, object> Variables
        {
            get
            {
                return variables;
            }
        }

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


            if (hostType == AuthenticationType.Client)
            {
                // declare (Credentials -> No Auth, No Enctypt)
                var un = DC.ToBytes(localUsername);
                var dmn = DC.ToBytes(domain);

                if (socket.State == SocketState.Established)
                {
                    SendParams((byte)0x60, (byte)dmn.Length, dmn, localNonce, (byte)un.Length, un);
                }
                else
                {
                    socket.OnConnect += () =>
                    {   // declare (Credentials -> No Auth, No Enctypt)
                         SendParams((byte)0x60, (byte)dmn.Length, dmn, localNonce, (byte)un.Length, un);
                    };
                }
            }
        }


        /// <summary>
        /// Create a new distributed connection. 
        /// </summary>
        /// <param name="socket">Socket to transfer data through.</param>
        /// <param name="domain">Working domain.</param>
        /// <param name="username">Username.</param>
        /// <param name="password">Password.</param>
        public DistributedConnection(ISocket socket, string domain, string username, string password)
        {
            //Instance.Name = Global.GenerateCode(12);
            this.hostType = AuthenticationType.Client;
            this.domain = domain;
            this.localUsername = username;
            this.localPassword = DC.ToBytes(password);

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
            init();
        }



        public string Link(IResource resource)
        {
            if (resource is DistributedConnection)
            {
                var r = resource as DistributedResource;
                if (r.Instance.Store == this)
                    return this.Instance.Name + "/" + r.Id;
            }

            return null;
        }


        void init()
        {
            queue.Then((x) =>
            {
                if (x.Type == DistributedResourceQueueItem.DistributedResourceQueueItemType.Event)
                    x.Resource._EmitEventByIndex(x.Index, (object[])x.Value);
                else
                    x.Resource.UpdatePropertyByIndex(x.Index, x.Value);
            });

            var r = new Random();
            localNonce = new byte[32];
            r.NextBytes(localNonce);
        }



        protected override void DataReceived(NetworkBuffer data)
        {
            // Console.WriteLine("DR " + hostType + " " + data.Available + " " + RemoteEndPoint.ToString());
            var msg = data.Read();
            uint offset = 0;
            uint ends = (uint)msg.Length;
            while (offset < ends)
            {

                if (ready)
                {
                    var rt = packet.Parse(msg, offset, ends);
                    if (rt <= 0)
                    {
                        data.HoldFor(msg, offset, ends - offset, (uint)(-rt));
                        return;
                    }
                    else
                    {
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
                                    IIPEventPropertyUpdated(packet.ResourceId, packet.MethodIndex, packet.Content);
                                    break;
                                case IIPPacket.IIPPacketEvent.EventOccured:
                                    IIPEventEventOccured(packet.ResourceId, packet.MethodIndex, packet.Content);
                                    break;
                            }
                        }
                        else if (packet.Command == IIPPacket.IIPPacketCommand.Request)
                        {
                            switch (packet.Action)
                            {
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
                                    IIPRequestCreateResource(packet.CallbackId, packet.ClassName);
                                    break;
                                case IIPPacket.IIPPacketAction.DeleteResource:
                                    IIPRequestDeleteResource(packet.CallbackId, packet.ResourceId);
                                    break;
                                case IIPPacket.IIPPacketAction.TemplateFromClassName:
                                    IIPRequestTemplateFromClassName(packet.CallbackId, packet.ClassName);
                                    break;
                                case IIPPacket.IIPPacketAction.TemplateFromClassId:
                                    IIPRequestTemplateFromClassId(packet.CallbackId, packet.ClassId);
                                    break;
                                case IIPPacket.IIPPacketAction.TemplateFromResourceLink:
                                    IIPRequestTemplateFromResourceLink(packet.CallbackId, packet.ResourceLink);
                                    break;
                                case IIPPacket.IIPPacketAction.TemplateFromResourceId:
                                    IIPRequestTemplateFromResourceId(packet.CallbackId, packet.ResourceId);
                                    break;
                                case IIPPacket.IIPPacketAction.ResourceIdFromResourceLink:
                                    IIPRequestResourceIdFromResourceLink(packet.CallbackId, packet.ResourceLink);
                                    break;
                                case IIPPacket.IIPPacketAction.InvokeFunction:
                                    IIPRequestInvokeFunction(packet.CallbackId, packet.ResourceId, packet.MethodIndex, packet.Content);
                                    break;
                                case IIPPacket.IIPPacketAction.GetProperty:
                                    IIPRequestGetProperty(packet.CallbackId, packet.ResourceId, packet.MethodIndex);
                                    break;
                                case IIPPacket.IIPPacketAction.GetPropertyIfModified:
                                    IIPRequestGetPropertyIfModifiedSince(packet.CallbackId, packet.ResourceId, packet.MethodIndex, packet.ResourceAge);
                                    break;
                                case IIPPacket.IIPPacketAction.SetProperty:
                                    IIPRequestSetProperty(packet.CallbackId, packet.ResourceId, packet.MethodIndex, packet.Content);
                                    break;
                            }
                        }
                        else if (packet.Command == IIPPacket.IIPPacketCommand.Reply)
                        {
                            switch (packet.Action)
                            {
                                case IIPPacket.IIPPacketAction.AttachResource:
                                    IIPReply(packet.CallbackId, packet.ClassId, packet.ResourceAge, packet.ResourceLink, packet.Content);

                                    //IIPReplyAttachResource(packet.CallbackId, packet.ResourceAge, Codec.ParseValues(packet.Content));
                                    break;
                                case IIPPacket.IIPPacketAction.ReattachResource:
                                    //IIPReplyReattachResource(packet.CallbackId, packet.ResourceAge, Codec.ParseValues(packet.Content));
                                    IIPReply(packet.CallbackId, packet.ResourceAge, packet.Content);

                                    break;
                                case IIPPacket.IIPPacketAction.DetachResource:
                                    //IIPReplyDetachResource(packet.CallbackId);
                                    IIPReply(packet.CallbackId);
                                    break;
                                case IIPPacket.IIPPacketAction.CreateResource:
                                    //IIPReplyCreateResource(packet.CallbackId, packet.ClassId, packet.ResourceId);
                                    IIPReply(packet.CallbackId, packet.ClassId, packet.ResourceId);
                                    break;
                                case IIPPacket.IIPPacketAction.DeleteResource:
                                    //IIPReplyDeleteResource(packet.CallbackId);
                                    IIPReply(packet.CallbackId);
                                    break;
                                case IIPPacket.IIPPacketAction.TemplateFromClassName:
                                    //IIPReplyTemplateFromClassName(packet.CallbackId, ResourceTemplate.Parse(packet.Content));
                                    IIPReply(packet.CallbackId, ResourceTemplate.Parse(packet.Content));
                                    break;
                                case IIPPacket.IIPPacketAction.TemplateFromClassId:
                                    //IIPReplyTemplateFromClassId(packet.CallbackId, ResourceTemplate.Parse(packet.Content));
                                    IIPReply(packet.CallbackId, ResourceTemplate.Parse(packet.Content));
                                    break;
                                case IIPPacket.IIPPacketAction.TemplateFromResourceLink:
                                    //IIPReplyTemplateFromResourceLink(packet.CallbackId, ResourceTemplate.Parse(packet.Content));
                                    IIPReply(packet.CallbackId, ResourceTemplate.Parse(packet.Content));
                                    break;
                                case IIPPacket.IIPPacketAction.TemplateFromResourceId:
                                    //IIPReplyTemplateFromResourceId(packet.CallbackId, ResourceTemplate.Parse(packet.Content));
                                    IIPReply(packet.CallbackId, ResourceTemplate.Parse(packet.Content));
                                    break;
                                case IIPPacket.IIPPacketAction.ResourceIdFromResourceLink:
                                    //IIPReplyResourceIdFromResourceLink(packet.CallbackId, packet.ClassId, packet.ResourceId, packet.ResourceAge);
                                    IIPReply(packet.CallbackId, packet.ClassId, packet.ResourceId, packet.ResourceAge);
                                    break;
                                case IIPPacket.IIPPacketAction.InvokeFunction:
                                    //IIPReplyInvokeFunction(packet.CallbackId, Codec.Parse(packet.Content, 0));
                                    IIPReply(packet.CallbackId, packet.Content);
                                    break;
                                case IIPPacket.IIPPacketAction.GetProperty:
                                    //IIPReplyGetProperty(packet.CallbackId, Codec.Parse(packet.Content, 0));
                                    IIPReply(packet.CallbackId, packet.Content);
                                    break;
                                case IIPPacket.IIPPacketAction.GetPropertyIfModified:
                                    //IIPReplyGetPropertyIfModifiedSince(packet.CallbackId, Codec.Parse(packet.Content, 0));
                                    IIPReply(packet.CallbackId, packet.Content);
                                    break;
                                case IIPPacket.IIPPacketAction.SetProperty:
                                    //IIPReplySetProperty(packet.CallbackId);
                                    IIPReply(packet.CallbackId);
                                    break;
                            }

                        }

                    }
                }

                else
                {
                    var rt = authPacket.Parse(msg, offset, ends);

                    Console.WriteLine(hostType.ToString() + " " + offset + " " + ends + " " + rt + " " + authPacket.ToString());

                    if (rt <= 0)
                    {
                        data.HoldFor(msg, ends + (uint)(-rt));
                        return;
                    }
                    else
                    {
                        offset += (uint)rt;

                        if (hostType == AuthenticationType.Host)
                        {
                            if (authPacket.Command == IIPAuthPacket.IIPAuthPacketCommand.Declare)
                            {
                                if (authPacket.RemoteMethod == IIPAuthPacket.IIPAuthPacketMethod.Credentials && authPacket.LocalMethod == IIPAuthPacket.IIPAuthPacketMethod.None)
                                {
                                    remoteUsername = authPacket.RemoteUsername;
                                    remoteNonce = authPacket.RemoteNonce;
                                    domain = authPacket.Domain;
                                    SendParams((byte)0xa0, localNonce);
                                }
                            }
                            else if (authPacket.Command == IIPAuthPacket.IIPAuthPacketCommand.Action)
                            {
                                if (authPacket.Action == IIPAuthPacket.IIPAuthPacketAction.AuthenticateHash)
                                {
                                    var remoteHash = authPacket.Hash;

                                    Server.Membership.GetPassword(remoteUsername, domain).Then((pw) =>
                                    {


                                        if (pw != null)
                                        {
                                            var hashFunc = SHA256.Create();
                                            var hash = hashFunc.ComputeHash(BinaryList.ToBytes(pw, remoteNonce, localNonce));
                                            if (hash.SequenceEqual(remoteHash))
                                            {
                                                // send our hash
                                                var localHash = hashFunc.ComputeHash(BinaryList.ToBytes(localNonce, remoteNonce, pw));

                                                SendParams((byte)0, localHash);

                                                readyToEstablish = true;
                                            }
                                            else
                                            {
                                                Console.WriteLine("Incorrect password");
                                                SendParams((byte)0xc0, (byte)1, (ushort)5, DC.ToBytes("Error"));
                                            }
                                        }
                                    });
                                }
                                else if (authPacket.Action == IIPAuthPacket.IIPAuthPacketAction.NewConnection)
                                {
                                    if (readyToEstablish)
                                    {
                                        var r = new Random();
                                        sessionId = new byte[32];
                                        r.NextBytes(sessionId);
                                        SendParams((byte)0x28, sessionId);
                                        ready = true;
                                        OnReady?.Invoke(this);
                                    }
                                }
                            }
                        }
                        else if (hostType == AuthenticationType.Client)
                        {
                            if (authPacket.Command == IIPAuthPacket.IIPAuthPacketCommand.Acknowledge)
                            {
                                remoteNonce = authPacket.RemoteNonce;

                                // send our hash
                                var hashFunc = SHA256.Create();
                                var localHash = hashFunc.ComputeHash(BinaryList.ToBytes(localPassword, localNonce, remoteNonce));

                                SendParams((byte)0, localHash);
                            }
                            else if (authPacket.Command == IIPAuthPacket.IIPAuthPacketCommand.Action)
                            {
                                if (authPacket.Action == IIPAuthPacket.IIPAuthPacketAction.AuthenticateHash)
                                {
                                    // check if the server knows my password
                                    var hashFunc = SHA256.Create();
                                    var remoteHash = hashFunc.ComputeHash(BinaryList.ToBytes(remoteNonce, localNonce, localPassword));

                                    if (remoteHash.SequenceEqual(authPacket.Hash))
                                    {
                                        // send establish request
                                        SendParams((byte)0x20, (ushort)0);
                                    }
                                    else
                                    {
                                        SendParams((byte)0xc0, 1, (ushort)5, DC.ToBytes("Error"));
                                    }
                                }
                                else if (authPacket.Action == IIPAuthPacket.IIPAuthPacketAction.ConnectionEstablished)
                                {
                                    sessionId = authPacket.SessionId;
                                    ready = true;
                                    OnReady?.Invoke(this);
                                }
                            }
                            else if (authPacket.Command == IIPAuthPacket.IIPAuthPacketCommand.Error)
                            {
                                OnError?.Invoke(this, authPacket.ErrorCode, authPacket.ErrorMessage);
                                Close();
                            }
                        }
                    }
                }
            }

        }

        /// <summary>
        /// Resource interface
        /// </summary>
        /// <param name="trigger">Resource trigger.</param>
        /// <returns></returns>
        public AsyncReply<bool> Trigger(ResourceTrigger trigger)
        {
            return new AsyncReply<bool>();
        }

        /// <summary>
        /// Store interface.
        /// </summary>
        /// <param name="resource">Resource.</param>
        /// <returns></returns>
        public bool Put(IResource resource)
        {
            resources.Add(Convert.ToUInt32(resource.Instance.Name), (DistributedResource)resource);
            return true;
        }
    }
}
