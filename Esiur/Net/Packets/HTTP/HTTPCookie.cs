using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Net.Packets.HTTP
{
    public struct HTTPCookie
    {
        public string Name;
        public string Value;
        public DateTime Expires;
        public string Path;
        public bool HttpOnly;
        public string Domain;

        public HTTPCookie(string name, string value)
        {
            Name = name;
            Value = value;
            Path = null;
            Expires = DateTime.MinValue;
            HttpOnly = false;
            Domain = null;
        }

        public HTTPCookie(string name, string value, DateTime expires)
        {
            Name = name;
            Value = value;
            Expires = expires;
            HttpOnly = false;
            Domain = null;
            Path = null;
        }

        public override string ToString()
        {
            //Set-Cookie: ckGeneric=CookieBody; expires=Sun, 30-Dec-2001 21:00:00 GMT; domain=.com.au; path=/
            //Set-Cookie: SessionID=another; expires=Fri, 29 Jun 2006 20:47:11 UTC; path=/
            var cookie = Name + "=" + Value;

            if (Expires.Ticks != 0)
                cookie += "; expires=" + Expires.ToUniversalTime().ToString("ddd, dd MMM yyyy HH:mm:ss") + " GMT";

            if (Domain != null)
                cookie += "; domain=" + Domain;

            if (Path != null)
                cookie += "; path=" + Path;

            if (HttpOnly)
                cookie += "; HttpOnly";

            return cookie;
        }
    }

}
