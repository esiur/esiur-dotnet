﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Esiur.Net.Sockets;
using Esiur.Misc;
using System.Threading;
using Esiur.Data;
using Esiur.Engine;
using System.Net;
using Esiur.Resource;

namespace Esiur.Net.TCP
{
    public class TCPServer : NetworkServer<TCPConnection>, IResource
    {

        [Storable]
        string ip
        {
            get;
            set;
        }
        [Storable]
        ushort port
        {
            get;
            set;
        }
        [Storable]
        uint timeout
        {
            get;
            set;
        }
        [Storable]
        uint clock
        {
            get;
            set;
        }
        public Instance Instance { get; set; }

        public AsyncReply<bool> Trigger(ResourceTrigger trigger)
        {
            if (trigger == ResourceTrigger.Initialize)
            {
                TCPSocket listener;

                if (ip != null)
                    listener =new TCPSocket(new IPEndPoint(IPAddress.Parse(ip), port));
                else
                    listener = new TCPSocket(new IPEndPoint(IPAddress.Any, port));

                Start(listener, timeout, clock);
            }
            else if (trigger == ResourceTrigger.Terminate)
            {
                Stop();
            }
            else if (trigger == ResourceTrigger.SystemReload)
            {
                Trigger(ResourceTrigger.Terminate);
                Trigger(ResourceTrigger.Initialize);
            }

            return new AsyncReply<bool>(true);
        }



        protected override void DataReceived(TCPConnection sender, NetworkBuffer data)
        {
            //throw new NotImplementedException();
            var msg = data.Read();

            foreach (Instance instance in Instance.Children)
            {
                var f = instance.Resource as TCPFilter;
                if (f.Execute(msg, data, sender))
                    return;
            }
        }

        private void SessionModified(TCPConnection Session, string Key, object NewValue)
        {

        }
 
        /*
        public TCPServer(string IP, int Port, int Timeout, int Clock)
            : base(IP, Port, Timeout, Clock)
        {
            if (Timeout > 0 && Clock > 0)
            {
                mTimer = new Timer(OnlineThread, null, 0, Clock * 1000);// TimeSpan.FromSeconds(Clock));
                mTimeout = Timeout;
            }
        }
         */ 

        /*
        private void OnlineThread(object state)
        {
            List<TCPConnection> ToBeClosed = null;
            //Console.WriteLine("Minute Thread");

            if (Connections.Count > 0)
            {
                Global.Log("TCPServer:OnlineThread", LogType.Debug,
                    //"Tick:" + DateTime.Now.Subtract(Connections[0].LastAction).TotalSeconds + ":" + mTimeout + ":" +
                    "Tick | Connections: " + Connections.Count + " Threads:" + System.Diagnostics.Process.GetCurrentProcess().Threads.Count);
            }


            try
            {
                foreach (TCPConnection c in Connections)//.Values)
                {
                    if (DateTime.Now.Subtract(c.LastAction).TotalSeconds >= mTimeout)
                    {
                        if (ToBeClosed == null)
                            ToBeClosed = new List<TCPConnection>();
                        ToBeClosed.Add(c);
                    }
                }

                if (ToBeClosed != null)
                {

                    Global.Log("TCPServer:OnlineThread", LogType.Debug, "Inactive Closed:" + ToBeClosed.Count);

                    foreach (TCPConnection c in ToBeClosed)
                        c.Close();

                    ToBeClosed.Clear();
                    ToBeClosed = null;


                }
            }
            catch (Exception ex)
            {
                Global.Log("TCPServer:OnlineThread", LogType.Debug, ex.ToString());
            }
        }
         */ 

        //~TCPServer()
        //{
          //  StopServer();
        //}

 


        protected override void ClientConnected(TCPConnection sender)
        {
            //Console.WriteLine("TCP Client Connected");

           // Global.Log("TCPServer",
           //     LogType.Debug,  
           //     "Connected:" + Connections.Count 
           //     + ":" + sender.RemoteEndPoint.ToString());

            foreach (Instance instance in Instance.Children)
            {
                var f = instance.Resource as TCPFilter;
                f.Connected(sender);
            }
        }

        protected override void ClientDisconnected(TCPConnection sender)
        {
            //Console.WriteLine("TCP Client Disconnected");

            // Global.Log("TCPServer", LogType.Debug, "Disconnected:" + Connections.Count);// + ":" + sender.RemoteEndPoint.ToString());

            foreach (Instance instance in Instance.Children)
            {
                try
                {
                    var f = instance.Resource as TCPFilter;
                    f.Disconnected(sender);
                }
                catch(Exception ex)
                {
                    Global.Log(ex);
                }
            }
        }


    }
}
