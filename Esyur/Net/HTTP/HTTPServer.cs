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
using Esyur.Net.Sockets;
using Esyur.Data;
using Esyur.Misc;
using Esyur.Core;
using Esyur.Net.Packets;
using System.Security.Cryptography.X509Certificates;
using Esyur.Resource;

namespace Esyur.Net.HTTP
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

        [Attribute]
        public virtual uint Timeout
        {
            get;
            set;
        }

        [Attribute]
        public virtual uint Clock
        {
            get;
            set;
        }

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
            
            foreach (var filter in filters)
                filter.ClientDisconnected(sender);
        }



        protected override void DataReceived(HTTPConnection sender, NetworkBuffer data)
        {
 
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
                if (BL > MaxPost)
                {
                    sender.Send(
                        "<html><body>POST method content is larger than "
                        + MaxPost
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
                foreach (var resource in filters)
                    if (resource.Execute(sender))
                        return;

                sender.Response.Number = HTTPResponsePacket.ResponseCode.HTTP_SERVERERROR;
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
                    sender.Send(Error500(ex.Message));
                }

            }
        }

        private string Error500(string msg)
        {
            return "<html><head><title>500 Internal Server Error</title></head><br>\r\n"
                     + "<body><br>\r\n"
                     + "<b>500</b> Internal Server Error<br>" + msg + "\r\n"
                     + "</body><br>\r\n"
                     + "</html><br>\r\n";
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

                ISocket listener;
                IPAddress ipAdd;

                if (IP == null)
                    ipAdd = IPAddress.Any;
                else
                    ipAdd = IPAddress.Parse(IP);

               // if (ssl)
                //    listener = new SSLSocket(new IPEndPoint(ipAdd, port), new X509Certificate2(certificate));
               // else
                    listener = new TCPSocket(new IPEndPoint(ipAdd, Port));

                Start(listener,
                            Timeout,
                            Clock);
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
        
        protected override void ClientConnected(HTTPConnection sender)
        {
            //sender.SessionModified += SessionModified;
            //sender.SessionEnded += SessionExpired;
            sender.SetParent(this);

            //Console.WriteLine("IN: " + this.Connections.Count);
            if (filters.Length == 0)
            {
                sender.Close();
                return;
            }

            foreach (var resource in filters)
            {
                resource.ClientConnected(sender);
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