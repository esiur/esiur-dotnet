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

        Session session;

        AsyncReply<bool> openReply;

        byte[] localPassword;
        byte[] localNonce, remoteNonce;

        bool ready, readyToEstablish;

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
        public DistributedServer Server
        {
            get;
            set;
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
        internal SendList SendParams(IAsyncReply<object[]> reply = null)//params object[] values)
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
        public override void Assign(ISocket socket)
        {
            base.Assign(socket);

            session.RemoteAuthentication.Source.Attributes.Add(SourceAttributeType.IPv4, socket.RemoteEndPoint.Address);
            session.RemoteAuthentication.Source.Attributes.Add(SourceAttributeType.Port, socket.RemoteEndPoint.Port);
            session.LocalAuthentication.Source.Attributes.Add(SourceAttributeType.IPv4, socket.LocalEndPoint.Address);
            session.LocalAuthentication.Source.Attributes.Add(SourceAttributeType.Port, socket.LocalEndPoint.Port);

            if (session.LocalAuthentication.Type == AuthenticationType.Client)
            {
                // declare (Credentials -> No Auth, No Enctypt)
 
                var un = DC.ToBytes(session.LocalAuthentication.Username);
                var dmn = DC.ToBytes(session.LocalAuthentication.Domain);// domain);

                if (socket.State == SocketState.Established)
                {
                    SendParams()
                        .AddUInt8(0x60)
                        .AddUInt8((byte)dmn.Length)
                        .AddUInt8Array(dmn)
                        .AddUInt8Array(localNonce)
                        .AddUInt8((byte)un.Length)
                        .AddUInt8Array(un)
                        .Done();//, dmn, localNonce, (byte)un.Length, un);
                }
                else
                {
                    socket.OnConnect += () =>
                    {   // declare (Credentials -> No Auth, No Enctypt)
                        //SendParams((byte)0x60, (byte)dmn.Length, dmn, localNonce, (byte)un.Length, un);
                        SendParams()
                       .AddUInt8(0x60)
                       .AddUInt8((byte)dmn.Length)
                       .AddUInt8Array(dmn)
                       .AddUInt8Array(localNonce)
                       .AddUInt8((byte)un.Length)
                       .AddUInt8Array(un)
                       .Done();
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
            this.session = new Session(   new ClientAuthentication()
                                        , new HostAuthentication());
            //Instance.Name = Global.GenerateCode(12);
            //this.hostType = AuthenticationType.Client;
            //this.domain = domain;
            //this.localUsername = username;
            session.LocalAuthentication.Domain = domain;
            session.LocalAuthentication.Username = username;
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
            var q = queue;
            q.Then((x) =>
            {
                if (x.Type == DistributedResourceQueueItem.DistributedResourceQueueItemType.Event)
                    x.Resource._EmitEventByIndex(x.Index, (object[])x.Value);
                else
                    x.Resource._UpdatePropertyByIndex(x.Index, x.Value);
            });
            //q.timeout?.Dispose();

            var r = new Random();
            localNonce = new byte[32];
            r.NextBytes(localNonce);
        }



        private uint processPacket(byte[] msg, uint offset, uint ends, NetworkBuffer data, int chunkId)
        {
            var packet = new IIPPacket();

            

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
                                IIPEventPropertyUpdated(packet.ResourceId, packet.MethodIndex, packet.Content);
                                break;
                            case IIPPacket.IIPPacketEvent.EventOccurred:
                                IIPEventEventOccurred(packet.ResourceId, packet.MethodIndex, packet.Content);
                                break;

                            case IIPPacketEvent.ChildAdded:
                                IIPEventChildAdded(packet.ResourceId, packet.ChildId);
                                break;
                            case IIPPacketEvent.ChildRemoved:
                                IIPEventChildRemoved(packet.ResourceId, packet.ChildId);
                                break;
                            case IIPPacketEvent.Renamed:
                                IIPEventRenamed(packet.ResourceId, packet.Content);
                                break;
                            case IIPPacketEvent.AttributesUpdated:
                                IIPEventAttributesUpdated(packet.ResourceId, packet.Content);
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
                                IIPRequestCreateResource(packet.CallbackId, packet.StoreId, packet.ResourceId, packet.Content);
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
                                IIPRequestRenameResource(packet.CallbackId, packet.ResourceId, packet.Content);
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

                            // Invoke
                            case IIPPacket.IIPPacketAction.InvokeFunctionArrayArguments:
                                IIPRequestInvokeFunctionArrayArguments(packet.CallbackId, packet.ResourceId, packet.MethodIndex, packet.Content);
                                break;

                            case IIPPacket.IIPPacketAction.InvokeFunctionNamedArguments:
                                IIPRequestInvokeFunctionNamedArguments(packet.CallbackId, packet.ResourceId, packet.MethodIndex, packet.Content);
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

                            // Attribute
                            case IIPPacketAction.GetAllAttributes:
                                IIPRequestGetAttributes(packet.CallbackId, packet.ResourceId, packet.Content, true);
                                break;
                            case IIPPacketAction.UpdateAllAttributes:
                                IIPRequestUpdateAttributes(packet.CallbackId, packet.ResourceId, packet.Content, true);
                                break;
                            case IIPPacketAction.ClearAllAttributes:
                                IIPRequestClearAttributes(packet.CallbackId, packet.ResourceId, packet.Content, true);
                                break;
                            case IIPPacketAction.GetAttributes:
                                IIPRequestGetAttributes(packet.CallbackId, packet.ResourceId, packet.Content, false);
                                break;
                            case IIPPacketAction.UpdateAttributes:
                                IIPRequestUpdateAttributes(packet.CallbackId, packet.ResourceId, packet.Content, false);
                                break;
                            case IIPPacketAction.ClearAttributes:
                                IIPRequestClearAttributes(packet.CallbackId, packet.ResourceId, packet.Content, false);
                                break;
                        }
                    }
                    else if (packet.Command == IIPPacket.IIPPacketCommand.Reply)
                    {
                        switch (packet.Action)
                        {
                            // Manage
                            case IIPPacket.IIPPacketAction.AttachResource:
                                IIPReply(packet.CallbackId, packet.ClassId, packet.ResourceAge, packet.ResourceLink, packet.Content);
                                break;

                            case IIPPacket.IIPPacketAction.ReattachResource:
                                IIPReply(packet.CallbackId, packet.ResourceAge, packet.Content);

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
                                IIPReply(packet.CallbackId, ResourceTemplate.Parse(packet.Content));
                                break;

                            case IIPPacketAction.QueryLink:
                            case IIPPacketAction.ResourceChildren:
                            case IIPPacketAction.ResourceParents:
                            case IIPPacketAction.ResourceHistory:
                                IIPReply(packet.CallbackId, packet.Content);
                                break;

                            // Invoke
                            case IIPPacket.IIPPacketAction.InvokeFunctionArrayArguments:
                            case IIPPacket.IIPPacketAction.InvokeFunctionNamedArguments:
                                IIPReplyInvoke(packet.CallbackId, packet.Content);
                                break;

                            case IIPPacket.IIPPacketAction.GetProperty:
                                IIPReply(packet.CallbackId, packet.Content);
                                break;

                            case IIPPacket.IIPPacketAction.GetPropertyIfModified:
                                IIPReply(packet.CallbackId, packet.Content);
                                break;
                            case IIPPacket.IIPPacketAction.SetProperty:
                                IIPReply(packet.CallbackId);
                                break;

                            // Attribute
                            case IIPPacketAction.GetAllAttributes:
                            case IIPPacketAction.GetAttributes:
                                IIPReply(packet.CallbackId, packet.Content);
                                break;

                            case IIPPacketAction.UpdateAllAttributes:
                            case IIPPacketAction.UpdateAttributes:
                            case IIPPacketAction.ClearAllAttributes:
                            case IIPPacketAction.ClearAttributes:
                                IIPReply(packet.CallbackId);
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
                                IIPReportChunk(packet.CallbackId, packet.Content);

                                break;
                        }
                    }
                }
            }

            else
            {
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
                            if (authPacket.RemoteMethod == IIPAuthPacket.IIPAuthPacketMethod.Credentials && authPacket.LocalMethod == IIPAuthPacket.IIPAuthPacketMethod.None)
                            {
                                Server.Membership.UserExists(authPacket.RemoteUsername, authPacket.Domain).Then(x =>
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
                                        //SendParams((byte)0xc0, (byte)1, (ushort)14, DC.ToBytes("User not found"));
                                        SendParams().AddUInt8(0xc0).AddUInt8(1).AddUInt16(14).AddString("User not found").Done();
                                    }
                                });

                            }
                        }
                        else if (authPacket.Command == IIPAuthPacket.IIPAuthPacketCommand.Action)
                        {
                            if (authPacket.Action == IIPAuthPacket.IIPAuthPacketAction.AuthenticateHash)
                            {
                                var remoteHash = authPacket.Hash;

                                Server.Membership.GetPassword(session.RemoteAuthentication.Username,
                                                              session.RemoteAuthentication.Domain).Then((pw) =>
                                                              {
                                                                  if (pw != null)
                                                                  {
                                                                      var hashFunc = SHA256.Create();
                                                                      //var hash = hashFunc.ComputeHash(BinaryList.ToBytes(pw, remoteNonce, localNonce));
                                                                      var hash = hashFunc.ComputeHash((new BinaryList())
                                                                                                        .AddUInt8Array(pw)
                                                                                                        .AddUInt8Array( remoteNonce)
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
                                                                          //Console.WriteLine("Incorrect password");
                                                                          //SendParams((byte)0xc0, (byte)1, (ushort)5, DC.ToBytes("Error"));
                                                                          SendParams().AddUInt8(0xc0).AddUInt8(1).AddUInt16(5).AddString("Error").Done();
                                                                      }
                                                                  }
                                                              });
                            }
                            else if (authPacket.Action == IIPAuthPacket.IIPAuthPacketAction.NewConnection)
                            {
                                if (readyToEstablish)
                                {
                                    var r = new Random();
                                    session.Id = new byte[32];
                                    r.NextBytes(session.Id);
                                    //SendParams((byte)0x28, session.Id);
                                    SendParams()
                                        .AddUInt8(0x28)
                                        .AddUInt8Array(session.Id)
                                        .Done();

                                    ready = true;
                                    openReply?.Trigger(true);
                                    OnReady?.Invoke(this);
                                    Server.Membership.Login(session);

                                    //Global.Log("auth", LogType.Warning, "U:" + RemoteUsername + " IP:" + Socket.RemoteEndPoint.Address.ToString() + " S:AUTH");

                                }
                            }
                        }
                    }
                    else if (session.LocalAuthentication.Type == AuthenticationType.Client)
                    {
                        if (authPacket.Command == IIPAuthPacket.IIPAuthPacketCommand.Acknowledge)
                        {
                            remoteNonce = authPacket.RemoteNonce;

                            // send our hash
                            var hashFunc = SHA256.Create();
                            //var localHash = hashFunc.ComputeHash(BinaryList.ToBytes(localPassword, localNonce, remoteNonce));
                            var localHash = hashFunc.ComputeHash(new BinaryList()
                                                                .AddUInt8Array(localPassword)
                                                                .AddUInt8Array(localNonce)
                                                                .AddUInt8Array(remoteNonce)
                                                                .ToArray());

                            SendParams()
                                .AddUInt8(0)
                                .AddUInt8Array(localHash)
                                .Done();

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
                                                                        .AddUInt8Array(localPassword)
                                                                        .ToArray());

                                
                                if (remoteHash.SequenceEqual(authPacket.Hash))
                                {
                                    // send establish request
                                    //SendParams((byte)0x20, (ushort)0);
                                    SendParams()
                                                .AddUInt8(0x20)
                                                .AddUInt16(0)
                                                .Done();
                                }
                                else
                                {
                                    SendParams()
                                                .AddUInt8(0xc0)
                                                .AddUInt8(1)
                                                .AddUInt16(5)
                                                .AddString("Error")
                                                .Done();

                                    //SendParams((byte)0xc0, 1, (ushort)5, DC.ToBytes("Error"));
                                }
                            }
                            else if (authPacket.Action == IIPAuthPacket.IIPAuthPacketAction.ConnectionEstablished)
                            {
                                session.Id = authPacket.SessionId;

                                ready = true;
                                openReply?.Trigger(true);
                                OnReady?.Invoke(this);

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
            // Console.WriteLine("DR " + hostType + " " + data.Available + " " + RemoteEndPoint.ToString());
            var msg = data.Read();
            uint offset = 0;
            uint ends = (uint)msg.Length;
 
            var packs = new List<string>();

            var chunkId = (new Random()).Next(1000, 1000000);

            

            while (offset < ends)
            {
                offset = processPacket(msg, offset, ends, data, chunkId);
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
                if (Instance.Attributes.ContainsKey("username")
                      && Instance.Attributes.ContainsKey("password"))
                {
                    //var hostname = String.Join("://", Instance.Name.Split(new string[] { "://" }, StringSplitOptions.None).Skip(1)).Split('/')[0];
                    // assign domain from hostname if not provided

                    var host = Instance.Name.Split(':');

                    var address = host[0];// hostname.Split(':')[0];
                    var port = ushort.Parse(host[1]);// hostname.Split(':')[1]);
                    var username = Instance.Attributes["username"].ToString();

                    var domain = Instance.Attributes.ContainsKey("domain") ? Instance.Attributes["domain"].ToString() : address;

                    session = new Session(new ClientAuthentication()
                                                , new HostAuthentication());

                    session.LocalAuthentication.Domain = domain;
                    session.LocalAuthentication.Username = username;
                    localPassword = DC.ToBytes(Instance.Attributes["password"].ToString());

                    openReply = new AsyncReply<bool>();
                    var sock = new TCPSocket();


                    sock.Connect(domain, port).Then((x)=> {
                        Assign(sock);
                        //rt.trigger(true);
                    }).Error((x) => 
                        openReply.TriggerError(x)
                        );

                    return openReply;
                }
            }

            return new AsyncReply<bool>();
        }

        /// <summary>
        /// Store interface.
        /// </summary>
        /// <param name="resource">Resource.</param>
        /// <returns></returns>
        public bool Put(IResource resource)
        {
            if (Codec.IsLocalResource(resource, this))
                resources.Add((resource as DistributedResource).Id, (DistributedResource)resource);
            // else ... send it to the peer
            return true;
        }

        public bool Record(IResource resource, string propertyName, object value, ulong age, DateTime dateTime)
        {
            // nothing to do
            return true;
        }

        public bool Modify(IResource resource, string propertyName, object value, ulong age, DateTime dateTime)
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
            throw new Exception("SS");

            //if (Codec.IsLocalResource(resource, this))
              //  return new AsyncBag<T>((resource as DistributedResource).children.Where(x => x.GetType() == typeof(T)).Select(x => (T)x));

            return null;
        }

        public AsyncBag<T> Parents<T>(IResource resource, string name) where T : IResource
        {
            throw new Exception("SS");
            //if (Codec.IsLocalResource(resource, this))
              //  return (resource as DistributedResource).parents.Where(x => x.GetType() == typeof(T)).Select(x => (T)x);

            return null;
        }

        /*
        public AsyncBag<T> Children<T>(IResource resource)
        {
            if (Codec.IsLocalResource(resource, this))
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
}
