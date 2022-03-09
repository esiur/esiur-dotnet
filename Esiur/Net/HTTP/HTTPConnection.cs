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
using Esiur.Net.Packets;
using Esiur.Misc;
using System.Security.Cryptography;
using Esiur.Core;

namespace Esiur.Net.HTTP;
public class HTTPConnection : NetworkConnection
{



    public bool WSMode { get; internal set; }
    public HTTPServer Server { get; internal set; }

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
        if (Request.Headers["connection"].ToLower() != "keep-alive" & IsConnected)
            Close();
    }

    public bool Upgrade()
    {
        var ok = Upgrade(Request, Response);

        if (ok)
        {
            WSMode = true;
            Send();
        }

        return ok;
    }

    public static bool Upgrade(HTTPRequestPacket request, HTTPResponsePacket response)
    {
        if (IsWebsocketRequest(request))
        {
            string magicString = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
            string ret = request.Headers["Sec-WebSocket-Key"] + magicString;
            // Compute the SHA1 hash
            SHA1 sha = SHA1.Create();
            byte[] sha1Hash = sha.ComputeHash(Encoding.UTF8.GetBytes(ret));
            response.Headers["Upgrade"] = request.Headers["Upgrade"];
            response.Headers["Connection"] = request.Headers["Connection"];// "Upgrade";
            response.Headers["Sec-WebSocket-Accept"] = Convert.ToBase64String(sha1Hash);

            if (request.Headers.ContainsKey("Sec-WebSocket-Protocol"))
                response.Headers["Sec-WebSocket-Protocol"] = request.Headers["Sec-WebSocket-Protocol"];


            response.Number = HTTPResponsePacket.ResponseCode.Switching;
            response.Text = "Switching Protocols";

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

    public void Send(WebsocketPacket packet)
    {
        if (packet.Data != null)
            base.Send(packet.Data);
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
        return IsWebsocketRequest(this.Request);
    }

    public static bool IsWebsocketRequest(HTTPRequestPacket request)
    {
        if (request.Headers.ContainsKey("connection")
            && request.Headers["connection"].ToLower().Contains("upgrade")
            && request.Headers.ContainsKey("upgrade")
            && request.Headers["upgrade"].ToLower() == "websocket"
            && request.Headers.ContainsKey("Sec-WebSocket-Version")
            && request.Headers["Sec-WebSocket-Version"] == "13"
            && request.Headers.ContainsKey("Sec-WebSocket-Key"))
        //&& Request.Headers.ContainsKey("Sec-WebSocket-Protocol"))
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    protected override void DataReceived(NetworkBuffer data)
    {

        byte[] msg = data.Read();

        var BL = Parse(msg);

        if (BL == 0)
        {
            if (Request.Method == HTTPRequestPacket.HTTPMethod.UNKNOWN)
            {
                Close();
                return;
            }
            if (Request.URL == "")
            {
                Close();
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
            data.HoldFor(msg, (uint)(msg.Length - BL));
            return;
        }
        else if (BL > 0)
        {
            if (BL > Server.MaxPost)
            {
                Send(
                    "<html><body>POST method content is larger than "
                    + Server.MaxPost
                    + " bytes.</body></html>");

                Close();
            }
            else
            {
                data.HoldFor(msg, (uint)(msg.Length + BL));
            }
            return;
        }
        else if (BL < 0) // for security
        {
            Close();
            return;
        }



        if (IsWebsocketRequest(Request) & !WSMode)
        {
            Upgrade();
            //return;
        }


        //return;

        try
        {
            if (!Server.Execute(this))
            {
                Response.Number = HTTPResponsePacket.ResponseCode.InternalServerError;
                Send("Bad Request");
                Close();
            }
        }
        catch (Exception ex)
        {
            if (ex.Message != "Thread was being aborted.")
            {

                Global.Log("HTTPServer", LogType.Error, ex.ToString());

                //Console.WriteLine(ex.ToString());
                //EventLog.WriteEntry("HttpServer", ex.ToString(), EventLogEntryType.Error);
                Send(Error500(ex.Message));
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

    public async AsyncReply<bool> SendFile(string filename)
    {
        if (Response.Handled == true)
            return false;


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
                Response.Number = HTTPResponsePacket.ResponseCode.NotFound;
                Send("File Not Found");
                return true;
            }


            var fileEditTime = File.GetLastWriteTime(filename).ToUniversalTime();
            if (Request.Headers.ContainsKey("if-modified-since"))
            {
                try
                {
                    var ims = DateTime.Parse(Request.Headers["if-modified-since"]);
                    if ((fileEditTime - ims).TotalSeconds < 2)
                    {
                        Response.Number = HTTPResponsePacket.ResponseCode.NotModified;
                        Response.Headers.Clear();
                        //Response.Text = "Not Modified";
                        Send(HTTPResponsePacket.ComposeOptions.SpecifiedHeadersOnly);
                        return true;
                    }
                }
                catch
                {
                    return false;
                }
            }



            Response.Number = HTTPResponsePacket.ResponseCode.OK;
            // Fri, 30 Oct 2007 14:19:41 GMT
            Response.Headers["Last-Modified"] = fileEditTime.ToString("ddd, dd MMM yyyy HH:mm:ss");
            FileInfo fi = new FileInfo(filename);
            Response.Headers["Content-Length"] = fi.Length.ToString();
            Send(HTTPResponsePacket.ComposeOptions.SpecifiedHeadersOnly);

            //var fd = File.ReadAllBytes(filename);

            //base.Send(fd);


            using (var fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
            {

                var buffer = new byte[60000];


                while (true)
                {
                    var n = fs.Read(buffer, 0, 60000);

                    if (n <= 0)
                        break;

                    //Thread.Sleep(50);
                    await base.SendAsync(buffer, 0, n);

                }
            }

            return true;

        }
        catch
        {
            try
            {
                Close();
            }
            finally
            {

            }

            return false;
        }
    }

    protected override void Connected()
    {
        // do nothing
    }

    protected override void Disconencted()
    {
        // do nothing
    }
}
