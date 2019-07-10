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
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Net;
using System.Collections;
using System.Collections.Generic;
using Esiur.Net.Sockets;
using Esiur.Data;
using Esiur.Misc;
using Esiur.Engine;
using Esiur.Net.Packets;
using System.Security.Cryptography.X509Certificates;
using Esiur.Resource;

namespace Esiur.Net.HTTP
{
    public class HTTPServer : NetworkServer<HTTPConnection>, IResource
    {
        Dictionary<string, HTTPSession> sessions= new Dictionary<string, HTTPSession>();

        public Instance Instance
        {
            get;
            set;
        }

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

        [Storable]
        uint maxPost
        {
            get;
            set;
        }

        [Storable]
        bool ssl
        {
            get;
            set;
        }

        [Storable]
        string certificate
        {
            get;
            set;
        }

        //public override void ClientConnected(TClient Sender)
        //{
        //}
        /*
        public DStringDictionary Configurations
        {
            get { return config; }
        }
         */ 

        public enum ResponseCodes : int
        {
            HTTP_OK = 200,
            HTTP_NOTFOUND = 404,
            HTTP_SERVERERROR = 500,
            HTTP_MOVED = 301,
            HTTP_NOTMODIFIED = 304,
            HTTP_REDIRECT = 307
        }

   
       public HTTPSession CreateSession(string id, int timeout)
       {
            var s = new HTTPSession();

            s.Set(id, timeout);


            sessions.Add(id, s);

            return s;
       }
 

        /*
        protected override void SessionModified(NetworkSession session, string key, object oldValue, object newValue)
        {
            foreach (var instance in Instance.Children)
            {
                var f = (HTTPFilter)instance;
                f.SessionModified(session as HTTPSession, key, oldValue, newValue);
            }
        }
        */

        //public override object InitializeLifetimeService()
        //{
        //  return null;
        //}





        public static string MakeCookie(string Item, string Value, DateTime Expires, string Domain, string Path, bool HttpOnly)
        {

            //Set-Cookie: ckGeneric=CookieBody; expires=Sun, 30-Dec-2001 21:00:00 GMT; domain=.com.au; path=/
            //Set-Cookie: SessionID=another; expires=Fri, 29 Jun 2006 20:47:11 UTC; path=/
            string Cookie = Item + "=" + Value;

            if (Expires.Ticks != 0)
            {
                Cookie += "; expires=" + Expires.ToUniversalTime().ToString("ddd, dd MMM yyyy HH:mm:ss") + " GMT";
            }
            if (Domain != null)
            {
                Cookie += "; domain=" + Domain;
            }
            if (Path != null)
            {
                Cookie += "; path=" + Path;
            }
            if (HttpOnly)
            {
                Cookie += "; HttpOnly";
            }
            return Cookie;
        }

        protected override void ClientDisconnected(HTTPConnection sender)
        {
            //Console.WriteLine("OUT: " + this.Connections.Count);

            foreach (IResource resource in Instance.Children)
            {
                if (resource is HTTPFilter)
                {
                   (resource as HTTPFilter).ClientDisconnected(sender);
                }
            }
        }



        protected override void DataReceived(HTTPConnection sender, NetworkBuffer data)
        {
            //Console.WriteLine(Data);
            // Initialize a new session
            //HTTPConnection HTTP = (HTTPConnection)sender.ExtraObject;

            //string Data = System.Text.Encoding.Default.GetString(ReceivedData);

            byte[] msg = data.Read();




            var BL = sender.Parse(msg);

            if (BL == 0)
            {
                if (sender.Request.Method == HTTPRequestPacket.HTTPMethod.UNKNOWN)
                {
                    sender.Close();
                    return;
                }
                if (sender.Request.URL == "")
                {
                    sender.Close();
                    return;
                }
            }
            else if (BL == -1)
            {
                data.HoldForNextWrite(msg);
                return;
            }
            else if (BL < 0)
            {
                data.HoldFor(msg, (uint) (msg.Length - BL));
                return;
            }
            else if (BL > 0)
            {
                if (BL > maxPost)
                {
                    sender.Send(
                        "<html><body>POST method content is larger than "
                        + maxPost
                        + " bytes.</body></html>");

                    sender.Close();
                }
                else
                {
                    data.HoldFor(msg, (uint)(msg.Length + BL));
                }
                return;
            }
            else if (BL < 0) // for security
            {
                sender.Close();
                return;
            }



            if (sender.IsWebsocketRequest() & !sender.WSMode)
            {
                sender.Upgrade();
                //return;
            }


            //return;

            try
            {
                foreach (IResource resource in Instance.Children)
                {
                    if (resource is HTTPFilter)
                    {
                        if ((resource as HTTPFilter).Execute(sender))
                            return;
                    }
                }

               sender.Send("Bad Request");
                sender.Close();
            }
            catch (Exception ex)
            {
                if (ex.Message != "Thread was being aborted.")
                {

                    Global.Log("HTTPServer", LogType.Error, ex.ToString());

                    //Console.WriteLine(ex.ToString());
                    //EventLog.WriteEntry("HttpServer", ex.ToString(), EventLogEntryType.Error);
                    sender.Send(Return500(ex.Message));
                }

            }
        }

        private string Return500(string sMessage)
        {
            string sTMP = null;
            sTMP = "<HTML><HEAD><TITLE>500 Internal Server Error</TITLE></HEAD><br>\r\n";
            sTMP = sTMP + "<BODY BGCOLOR=" + (char)(34) + "#FFFFFF" + (char)(34) + " Text=" + (char)(34) + "#000000" + (char)(34) + " LINK=" + (char)(34) + "#0000FF" + (char)(34) + " VLINK=" + (char)(34) + "#000080" + (char)(34) + " ALINK=" + (char)(34) + "#008000" + (char)(34) + "><br>\r\n";
            sTMP = sTMP + "<b>500</b> Sorry - Internal Server Error<br>" + sMessage + "\r\n";
            sTMP = sTMP + "</BODY><br>\r\n";
            sTMP = sTMP + "</HTML><br>\r\n";
            return sTMP;
        }


        /*
        protected override void SessionEnded(NetworkSession session)
        {
            // verify wether there are no active connections related to the session

            foreach (HTTPConnection c in Connections)//.Values)
            {
                if (c.Session == session)
                {
                    session.Refresh();
                    return;
                }
            }

            foreach (Instance instance in Instance.Children)
            {
                var f = (HTTPFilter)instance.Resource;
                f.SessionExpired((HTTPSession)session);
            }

            base.SessionEnded((HTTPSession)session);
            //Sessions.Remove(Session.ID);
            //Session.Dispose();
        }
        */

        /*
        public int TTL
        {
            get
            {
                return Timeout;// mTimeout;
            }
        }
         */ 


        public AsyncReply<bool> Trigger(ResourceTrigger trigger)
        {

            if (trigger == ResourceTrigger.Initialize)
            {
                //var ip = (IPAddress)Instance.Attributes["ip"];
                //var port = (int)Instance.Attributes["port"];
                //var ssl = (bool)Instance.Attributes["ssl"];
                //var cert = (string)Instance.Attributes["certificate"];

                //if (ip == null) ip = IPAddress.Any;

                ISocket listener;
                IPAddress ipAdd;

                if (ip == null)
                    ipAdd = IPAddress.Any;
                else
                    ipAdd = IPAddress.Parse(ip);

               // if (ssl)
                //    listener = new SSLSocket(new IPEndPoint(ipAdd, port), new X509Certificate2(certificate));
               // else
                    listener = new TCPSocket(new IPEndPoint(ipAdd, port));

                Start(listener,
                            timeout,
                            clock);
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
        
        protected override void ClientConnected(HTTPConnection sender)
        {
            //sender.SessionModified += SessionModified;
            //sender.SessionEnded += SessionExpired;
            sender.SetParent(this);

            //Console.WriteLine("IN: " + this.Connections.Count);

            foreach (var resource in Instance.Children)
            {
                if (resource is HTTPFilter)
                {
                    (resource as HTTPFilter).ClientConnected(sender);
                }
            }
        }

        public void Destroy()
        {

        }

        /*
		public int LocalPort
		{
			get 
			{
				return cServer.LocalPort;
			}
		}
         */

        /* 
        public HTTPServer(int Port)
        {
            cServer = new TServer();
            cServer.LocalPort = Port;
            cServer.StartServer();
            cServer.ClientConnected += new TServer.eClientConnected(ClientConnected);
            cServer.ClientDisConnected += new TServer.eClientDisConnected(ClientDisConnected);
            cServer.ClientIsSwitching += new TServer.eClientIsSwitching(ClientIsSwitching);
            cServer.DataReceived += new TServer.eDataReceived(DataReceived);

        }*/

        //~HTTPServer()
        //{
        //    cServer.StopServer();
        //}
    }
}