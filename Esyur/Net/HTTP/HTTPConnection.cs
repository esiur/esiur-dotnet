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
using Esyur.Net.Packets;
using Esyur.Misc;
using System.Security.Cryptography;

namespace Esyur.Net.HTTP
{
    public class HTTPConnection : NetworkConnection
    {

        public void SetParent(HTTPServer Parent)
        {
            Server = Parent;
        }

        public bool WSMode { get; internal set; }
        private HTTPServer Server;
        public WebsocketPacket WSRequest { get; set; }
        public HTTPRequestPacket Request { get; set; }
        public HTTPResponsePacket Response { get; } = new HTTPResponsePacket();

        HTTPSession session;

        public KeyList<string, object> Variables { get; } = new KeyList<string, object>();



        internal long Parse(byte[] data)
        {
            if (WSMode)
            {
                // now parse WS protocol
                WebsocketPacket ws = new WebsocketPacket();

                var pSize = ws.Parse(data, 0, (uint)data.Length);


                if (pSize > 0)
                {
                    WSRequest = ws;
                    return 0;
                }
                else
                {
                    return pSize;
                }
            }
            else
            {
                var rp = new HTTPRequestPacket();
                var pSize = rp.Parse(data, 0, (uint)data.Length);
                if (pSize > 0)
                {
                    Request = rp;
                    return 0;
                }
                else
                {
                    return pSize;
                }
            }
        }


        public void Flush()
        {
            // close the connection
            if (Request.Headers["connection"].ToLower() != "keep-alive" & Connected)
                Close();
        }

        public bool Upgrade()
        {
            if (IsWebsocketRequest())
            {
                string magicString = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
                string ret = Request.Headers["Sec-WebSocket-Key"] + magicString;
                // Compute the SHA1 hash
                SHA1 sha = SHA1.Create();
                byte[] sha1Hash = sha.ComputeHash(Encoding.UTF8.GetBytes(ret));
                Response.Headers["Upgrade"] = Request.Headers["Upgrade"];
                Response.Headers["Connection"] = Request.Headers["Connection"];// "Upgrade";
                Response.Headers["Sec-WebSocket-Accept"] = Convert.ToBase64String(sha1Hash);

                if (Request.Headers.ContainsKey("Sec-WebSocket-Protocol"))
                    Response.Headers["Sec-WebSocket-Protocol"] = Request.Headers["Sec-WebSocket-Protocol"];


                Response.Number = HTTPResponsePacket.ResponseCode.HTTP_SWITCHING;
                Response.Text = "Switching Protocols";
                WSMode = true;

                Send();

                return true;
            }

            return false;
        }

        public HTTPServer Parent
        {
            get
            {
                return Server;
            }
        }

        public override void Send(string data)
        {
            Response.Message = Encoding.UTF8.GetBytes(data);
            Send();
        }

        public override void Send(byte[] msg, int offset, int length)
        {
            Response.Message = DC.Clip(msg, (uint)offset, (uint)length);
            Send();
        }

        public override void Send(byte[] message)
        {
            Response.Message = message;
            Send();
        }

        public void Send(HTTPResponsePacket.ComposeOptions Options = HTTPResponsePacket.ComposeOptions.AllCalculateLength)
        {
            if (Response.Handled)
                return;


            try
            {
                Response.Compose(Options);
                base.Send(Response.Data);

                // Refresh the current session
                if (session != null)
                    session.Refresh();

            }
            catch
            {
                try
                {
                    Close();
                }
                finally { }
            }
            finally
            {

            }
        }


        public void CreateNewSession()
        {
            if (session == null)
            {
                // Create a new one
                session = Server.CreateSession(Global.GenerateCode(12), 60 * 20);

                HTTPResponsePacket.HTTPCookie cookie = new HTTPResponsePacket.HTTPCookie("SID", session.Id);
                cookie.Expires = DateTime.MaxValue;
                cookie.Path = "/";
                cookie.HttpOnly = true;

                Response.Cookies.Add(cookie);
            }
        }


        public bool IsWebsocketRequest()
        {
            if (Request.Headers.ContainsKey("connection")
                && Request.Headers["connection"].ToLower().Contains("upgrade")
                && Request.Headers.ContainsKey("upgrade")
                && Request.Headers["upgrade"].ToLower() == "websocket"
                && Request.Headers.ContainsKey("Sec-WebSocket-Version")
                && Request.Headers["Sec-WebSocket-Version"] == "13"
                && Request.Headers.ContainsKey("Sec-WebSocket-Key"))
            //&& Request.Headers.ContainsKey("Sec-WebSocket-Protocol"))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        bool bb;

        public void SendFile(string filename)
        {
            if (Response.Handled == true)
                return;


            try
            {

                //HTTP/1.1 200 OK
                //Server: Microsoft-IIS/5.0
                //Content-Location: http://127.0.0.1/index.html
                //Date: Wed, 10 Dec 2003 19:10:25 GMT
                //Content-Type: text/html
                //Accept-Ranges: bytes
                //Last-Modified: Mon, 22 Sep 2003 22:36:56 GMT
                //Content-Length: 1957

                if (!File.Exists(filename))
                {
                    Response.Number = HTTPResponsePacket.ResponseCode.HTTP_NOTFOUND;
                    Send("File Not Found");
                    return;
                }


                var fileEditTime = File.GetLastWriteTime(filename).ToUniversalTime();
                if (Request.Headers.ContainsKey("if-modified-since"))
                {
                    try
                    {
                        var ims = DateTime.Parse(Request.Headers["if-modified-since"]);
                        if (Math.Abs((fileEditTime - ims).TotalSeconds) < 0)
                        {
                            Response.Number = HTTPResponsePacket.ResponseCode.HTTP_NOTMODIFIED;
                            Response.Text = "Not Modified";
                            Send((byte[])null);
                        }
                    }
                    catch
                    {

                    }
                }



                Response.Number = HTTPResponsePacket.ResponseCode.HTTP_OK;
                // Fri, 30 Oct 2007 14:19:41 GMT
                Response.Headers["Last-Modified"] = fileEditTime.ToString("ddd, dd MMM yyyy HH:mm:ss");
                FileInfo fi = new FileInfo(filename);
                Response.Headers["Content-Length"] = fi.Length.ToString();
                Send(HTTPResponsePacket.ComposeOptions.SpecifiedHeadersOnly);

                //var fd = File.ReadAllBytes(filename);

                //base.Send(fd);

                
                using (var fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
                {

                    var buffer = new byte[5000];


                    while (true)
                    {
                        var n = fs.Read(buffer, 0, 5000);

                        if (n <= 0)
                            break;


                        base.Send(buffer, 0, n);

                    }
                }
                

            }
            catch
            {
                try
                {
                    Close();
                }
                finally { }
            }
        }
    }
}