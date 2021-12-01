
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
using Esiur.Misc;
using Esiur.Data;
using System.Net;
using System.Text.Json.Serialization;

namespace Esiur.Net.Packets;
public class HTTPRequestPacket : Packet
{

    public enum HTTPMethod : byte
    {
        GET,
        POST,
        HEAD,
        PUT,
        DELETE,
        OPTIONS,
        TRACE,
        CONNECT,
        UNKNOWN
    }

    public StringKeyList Query;
    public HTTPMethod Method;
    public StringKeyList Headers;

    public bool WSMode;

    public string Version;
    public StringKeyList Cookies; // String
    public string URL; /// With query
    public string Filename; /// Without query
    //public byte[] PostContents;
    public KeyList<string, object> PostForms;
    public byte[] Message;


    private HTTPMethod getMethod(string method)
    {
        switch (method.ToLower())
        {
            case "get":
                return HTTPMethod.GET;
            case "post":
                return HTTPMethod.POST;
            case "head":
                return HTTPMethod.HEAD;
            case "put":
                return HTTPMethod.PUT;
            case "delete":
                return HTTPMethod.DELETE;
            case "options":
                return HTTPMethod.OPTIONS;
            case "trace":
                return HTTPMethod.TRACE;
            case "connect":
                return HTTPMethod.CONNECT;
            default:
                return HTTPMethod.UNKNOWN;
        }
    }

    public override string ToString()
    {
        return "HTTPRequestPacket"
            + "\n\tVersion: " + Version
            + "\n\tMethod: " + Method
            + "\n\tURL: " + URL
            + "\n\tMessage: " + (Message != null ? Message.Length.ToString() : "NULL");
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

        Cookies = new StringKeyList();
        PostForms = new KeyList<string, object>();
        Query = new StringKeyList();
        Headers = new StringKeyList();

        sMethod = sLines[0].Split(' ');
        Method = getMethod(sMethod[0].Trim());

        if (sMethod.Length == 3)
        {
            sMethod[1] = WebUtility.UrlDecode(sMethod[1]);
            if (sMethod[1].Length >= 7)
            {
                if (sMethod[1].StartsWith("http://"))
                {
                    sMethod[1] = sMethod[1].Substring(sMethod[1].IndexOf("/", 7));
                }
            }

            URL = sMethod[1].Trim();

            if (URL.IndexOf("?", 0) != -1)
            {
                Filename = URL.Split(new char[] { '?' }, 2)[0];
            }
            else
            {
                Filename = URL;
            }

            if (Filename.IndexOf("%", 0) != -1)
            {
                Filename = WebUtility.UrlDecode(Filename);
            }

            Version = sMethod[2].Trim();
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

            if (header[0] == "cookie")
            {
                string[] cookies = header[1].Split(';');

                foreach (string cookie in cookies)
                {
                    if (cookie.IndexOf('=') != -1)
                    {
                        string[] splitCookie = cookie.Split('=');
                        splitCookie[0] = splitCookie[0].Trim();
                        splitCookie[1] = splitCookie[1].Trim();
                        if (!(Cookies.ContainsKey(splitCookie[0].Trim())))
                            Cookies.Add(splitCookie[0], splitCookie[1]);
                    }
                    else
                    {
                        if (!(Cookies.ContainsKey(cookie.Trim())))
                        {
                            Cookies.Add(cookie.Trim(), String.Empty);
                        }
                    }
                }
            }
        }

        // Query String
        if (URL.IndexOf("?", 0) != -1)
        {
            string[] SQ = URL.Split(new char[] { '?' }, 2)[1].Split('&');
            foreach (string S in SQ)
            {
                if (S.IndexOf("=", 0) != -1)
                {
                    string[] qp = S.Split(new char[] { '=' }, 2);

                    if (!Query.ContainsKey(WebUtility.UrlDecode(qp[0])))
                    {
                        Query.Add(WebUtility.UrlDecode(qp[0]), WebUtility.UrlDecode(qp[1]));
                    }
                }
                else
                {
                    if (!(Query.ContainsKey(WebUtility.UrlDecode(S))))
                    {
                        Query.Add(WebUtility.UrlDecode(S), null);
                    }
                }
            }
        }

        // Post Content-Length
        if (Method == HTTPMethod.POST)
        {
            try
            {

                uint postSize = uint.Parse((string)Headers["content-length"]);

                // check limit
                if (postSize > data.Length - headerSize)
                    return -(postSize - (data.Length - headerSize));


                if (
                    Headers["content-type"] == null
                    || Headers["content-type"] == ""
                    || Headers["content-type"].StartsWith("application/x-www-form-urlencoded"))
                {
                    string[] PostVars = null;
                    PostVars = Encoding.UTF8.GetString(data, (int)headerSize, (int)postSize).Split('&');
                    for (int J = 0; J < PostVars.Length; J++)
                    {
                        if (PostVars[J].IndexOf("=") != -1)
                        {
                            string key = WebUtility.HtmlDecode(
                                WebUtility.UrlDecode(PostVars[J].Split(new char[] { '=' }, 2)[0]));
                            if (PostForms.Contains(key))
                                PostForms[key] = WebUtility.HtmlDecode(
                                    WebUtility.UrlDecode(PostVars[J].Split(new char[] { '=' }, 2)[1]));
                            else
                                PostForms.Add(key, WebUtility.HtmlDecode(
                                    WebUtility.UrlDecode(PostVars[J].Split(new char[] { '=' }, 2)[1])));
                        }
                        else
                            if (PostForms.Contains("unknown"))
                            PostForms["unknown"] = PostForms["unknown"]
                                + "&" + WebUtility.HtmlDecode(WebUtility.UrlDecode(PostVars[J]));
                        else
                            PostForms.Add("unknown", WebUtility.HtmlDecode(WebUtility.UrlDecode(PostVars[J])));
                    }
                }
                else if (Headers["content-type"].StartsWith("multipart/form-data"))
                {
                    int st = 1;
                    int ed = 0;
                    string strBoundry = "--" + Headers["content-type"].Substring(
                        Headers["content-type"].IndexOf("boundary=", 0) + 9);

                    string[] sc = Encoding.UTF8.GetString(data, (int)headerSize, (int)postSize).Split(
                                                new string[] { strBoundry }, StringSplitOptions.None);


                    for (int j = 1; j < sc.Length - 1; j++)
                    {
                        string[] ps = sc[j].Split(new string[] { "\r\n\r\n" }, 2, StringSplitOptions.None);
                        ps[1] = ps[1].Substring(0, ps[1].Length - 2); // remove the empty line
                        st = ps[0].IndexOf("name=", 0) + 6;
                        ed = ps[0].IndexOf("\"", st);
                        PostForms.Add(ps[0].Substring(st, ed - st), ps[1]);
                    }
                }
                //else if (Headers["content-type"] == "application/json")
                //{
                //    var json = DC.Clip(data, headerSize, postSize);
                //}
                else
                {
                    //PostForms.Add(Headers["content-type"], Encoding.Default.GetString( ));
                    Message = DC.Clip(data, headerSize, postSize);
                }

                return headerSize + postSize;

            }
            catch
            {
                return 0;
            }
        }

        return headerSize;
    }
}
