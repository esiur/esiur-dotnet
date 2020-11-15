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
using Esiur.Core;
using Esiur.Net.Packets;
using System.Security.Cryptography.X509Certificates;
using Esiur.Resource;

namespace Esiur.Net.HTTP
{
    public class HTTPServer : NetworkServer<HTTPConnection>, IResource
    {
        Dictionary<string, HTTPSession> sessions= new Dictionary<string, HTTPSession>();
        HTTPFilter[] filters = new HTTPFilter[0];

        public Instance Instance
        {
            get;
            set;
        }

        [Attribute]
        public virtual string IP
        {
            get;
            set;
        }

        [Attribute]
        public virtual ushort Port
        {
            get;
            set;
        }

        //[Attribute]
        //public virtual uint Timeout
        //{
        //    get;
        //    set;
        //}

        //[Attribute]
        //public virtual uint Clock
        //{
        //    get;
        //    set;
        //}

        [Attribute]
        public virtual uint MaxPost
        {
            get;
            set;
        }

        [Attribute]
        public virtual bool SSL
        {
            get;
            set;
        }

        [Attribute]
        public virtual string Certificate
        {
            get;
            set;
        }
         
   
       public HTTPSession CreateSession(string id, int timeout)
       {
            var s = new HTTPSession();

            s.Set(id, timeout);


            sessions.Add(id, s);

            return s;
       }

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

        protected override void ClientDisconnected(HTTPConnection connection)
        {
            foreach (var filter in filters)
                filter.ClientDisconnected(connection);
        }

        

        internal bool Execute(HTTPConnection sender)
        {
            foreach (var resource in filters)
                if (resource.Execute(sender).Wait(30000))
                    return true;
            return false;
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


        public async AsyncReply<bool> Trigger(ResourceTrigger trigger)
        {

            if (trigger == ResourceTrigger.Initialize)
            {
                //var ip = (IPAddress)Instance.Attributes["ip"];
                //var port = (int)Instance.Attributes["port"];
                //var ssl = (bool)Instance.Attributes["ssl"];
                //var cert = (string)Instance.Attributes["certificate"];

                //if (ip == null) ip = IPAddress.Any;

                Sockets.ISocket listener;
                IPAddress ipAdd;

                if (IP == null)
                    ipAdd = IPAddress.Any;
                else
                    ipAdd = IPAddress.Parse(IP);

                if (SSL)
                   listener = new SSLSocket(new IPEndPoint(ipAdd, Port), new X509Certificate2(Certificate));
                else
                    listener = new TCPSocket(new IPEndPoint(ipAdd, Port));

                Start(listener);
            }
            else if (trigger == ResourceTrigger.Terminate)
            {
                Stop();
            }
            else if (trigger == ResourceTrigger.SystemReload)
            {
                await Trigger(ResourceTrigger.Terminate);
                await Trigger(ResourceTrigger.Initialize);
            }
            else if (trigger == ResourceTrigger.SystemInitialized)
            {
                filters = await Instance.Children<HTTPFilter>();
            }

            return true;

        }


        public override void Add(HTTPConnection connection)
        {
            connection.Server = this;
            base.Add(connection);
        }

        public override void Remove(HTTPConnection connection)
        {
            connection.Server = null;
            base.Remove(connection);
        }

        protected override void ClientConnected(HTTPConnection connection)
        {
            if (filters.Length == 0)
            {
                connection.Close();
                return;
            }

            foreach (var resource in filters)
            {
                resource.ClientConnected(connection);
            }
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