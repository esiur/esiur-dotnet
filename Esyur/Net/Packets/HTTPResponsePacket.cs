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
using System.Linq;
using System.Text;
using Esyur.Misc;
using Esyur.Data;
 
namespace Esyur.Net.Packets
{
    public class HTTPResponsePacket : Packet
    {

        public enum ComposeOptions : int
        {
            AllCalculateLength,
            AllDontCalculateLength,
            SpecifiedHeadersOnly,
            DataOnly
        }

        public enum ResponseCode : int
        {
            HTTP_SWITCHING = 101,
            HTTP_OK = 200,
            HTTP_NOTFOUND = 404,
            HTTP_SERVERERROR = 500,
            HTTP_MOVED = 301,
            HTTP_NOTMODIFIED = 304,
            HTTP_REDIRECT = 307
        }

        public class HTTPCookie
        {
            public string Name;
            public string Value;
            public DateTime Expires;
            public string Path;
            public bool HttpOnly;
            public string Domain;
            
            public HTTPCookie(string Name, string Value)
            {
                this.Name = Name;
                this.Value = Value;
            }

            public HTTPCookie(string Name, string Value, DateTime Expires)
            {
                this.Name = Name;
                this.Value = Value;
                this.Expires = Expires;
            }

            public override string ToString()
            {
                //Set-Cookie: ckGeneric=CookieBody; expires=Sun, 30-Dec-2001 21:00:00 GMT; domain=.com.au; path=/
                //Set-Cookie: SessionID=another; expires=Fri, 29 Jun 2006 20:47:11 UTC; path=/
                string Cookie = Name + "=" + Value;

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
        }

        public StringKeyList Headers = new StringKeyList(true);
        public string Version = "HTTP/1.1";

        public byte[] Message;
        public ResponseCode Number;
        public string Text;
        //public DStringDictionary Cookies;
        public List<HTTPCookie> Cookies = new List<HTTPCookie>();
        public bool Handled;
        //public bool ResponseHandled; //flag this as true if you are handling it yourself

        //private bool createSession;

        //private HTTPServer Server;
        //public HTTPSession Session;

        public override string ToString()
        {
            return "HTTPResponsePacket"
                + "\n\tVersion: " + Version
                //+ "\n\tMethod: " + Method
                //+ "\n\tURL: " + URL
                + "\n\tMessage: " + (Message != null ? Message.Length.ToString() : "NULL");
        }

        private string MakeHeader(ComposeOptions Options)
        {
            string header = Version + " " + (int)Number + " " + Text + "\r\n"
                              + "Server: Delta Web Server\r\n"
                //Fri, 30 Oct 2007 14:19:41 GMT"
                              + "Date: " + DateTime.Now.ToUniversalTime().ToString("r") + "\r\n";


            if (Options == ComposeOptions.AllCalculateLength && Message != null) 
            {
                Headers["Content-Length"] = Message.Length.ToString();
            }

            foreach (var kv in Headers)
            {
                header += kv.Key + ": " + kv.Value + "\r\n";
            }

            // Set-Cookie: ckGeneric=CookieBody; expires=Sun, 30-Dec-2007 21:00:00 GMT; path=/
            // Set-Cookie: ASPSESSIONIDQABBDSQA=IPDPMMMALDGFLMICEJIOCIPM; path=/
            
            foreach (var Cookie in Cookies)
            {
                header += "Set-Cookie: " + Cookie.ToString() + "\r\n";
            }


            header += "\r\n";

            return header;
        }


        public bool Compose(ComposeOptions options)
        {
            List<byte> msg = new List<byte>();

            if (options != ComposeOptions.DataOnly)
            {
                msg.AddRange(Encoding.UTF8.GetBytes(MakeHeader(options)));
            }

            if (options != ComposeOptions.SpecifiedHeadersOnly)
            {
                if (Message != null)
                   msg.AddRange(Message);
            }
            
            Data = msg.ToArray();

            return true;
        }

        public override bool Compose()
        {
            return Compose(ComposeOptions.AllDontCalculateLength);
        }

        public override long Parse(byte[] data, uint offset, uint ends)
        {
            string[] sMethod = null;
            string[] sLines = null;

            uint headerSize = 0;

            for (uint i = offset; i < ends - 3; i++)
            {
                if (data[i] == '\r' && data[i + 1] == '\n'
                    && data[i + 2] == '\r' && data[i + 3] == '\n')
                {
                    sLines = Encoding.ASCII.GetString(data, (int)offset, (int)(i - offset)).Split(new string[] { "\r\n" },
                        StringSplitOptions.None);

                    headerSize = i + 4;
                    break;
                }
            }

            if (headerSize == 0)
                return -1;

            //Cookies = new DStringDictionary();
            //Headers = new DStringDictionary(true);

            sMethod = sLines[0].Split(' ');

            if (sMethod.Length == 3)
            {
                Version = sMethod[0].Trim();
                Number = (ResponseCode)(Convert.ToInt32(sMethod[1].Trim()));
                Text = sMethod[2];
            }

            // Read all headers

            for (int i = 1; i < sLines.Length; i++)
            {
                if (sLines[i] == String.Empty)
                {
                    // Invalid header
                    return 0;
                }

                if (sLines[i].IndexOf(':') == -1)
                {
                    // Invalid header
                    return 0;
                }

                string[] header = sLines[i].Split(new char[] { ':' }, 2);

                header[0] = header[0].ToLower();
                Headers[header[0]] = header[1].Trim();

                //Set-Cookie: NAME=VALUE; expires=DATE;

                if (header[0] == "set-cookie")
                {
                    string[] cookie = header[1].Split(';');

                    if (cookie.Length >= 1)
                    {
                        string[] splitCookie = cookie[0].Split('=');
                        HTTPCookie c = new HTTPCookie(splitCookie[0], splitCookie[1]);

                        for (int j = 1; j < cookie.Length; j++)
                        {
                            splitCookie = cookie[j].Split('=');
                            switch (splitCookie[0].ToLower())
                            {
                                case "domain":
                                    c.Domain = splitCookie[1];
                                    break;
                                case "path":
                                    c.Path = splitCookie[1];
                                    break;
                                case "httponly":
                                    c.HttpOnly = true;
                                    break;
                                case "expires":
                                    // Wed, 13-Jan-2021 22:23:01 GMT
                                    c.Expires = DateTime.Parse(splitCookie[1]);
                                    break;
                            }
                        }

                    }

                }
            }

            // Content-Length

            try
            {

                uint contentLength = uint.Parse((string)Headers["content-length"]);

                // check limit
                if (contentLength > data.Length - headerSize)
                {
                    return contentLength - (data.Length - headerSize);
                }

                Message = DC.Clip(data, offset, contentLength);

                return headerSize + contentLength;

            }
            catch
            {
                return 0;
            }
        }
    }
}
