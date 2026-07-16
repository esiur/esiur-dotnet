using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Esiur.Net.Packets.Http
{
    public enum HttpCookieSameSite
    {
        Unspecified,
        Lax,
        Strict,
        None,
    }

    public struct HttpCookie
    {
        public string Name;
        public string Value;
        public DateTime Expires;
        public string Path;
        public bool HttpOnly;
        public bool Secure;
        public HttpCookieSameSite SameSite;
        public string Domain;

        public HttpCookie(string name, string value)
        {
            Name = name;
            Value = value;
            Path = null;
            Expires = DateTime.MinValue;
            HttpOnly = false;
            Secure = false;
            SameSite = HttpCookieSameSite.Unspecified;
            Domain = null;
        }

        public HttpCookie(string name, string value, DateTime expires)
        {
            Name = name;
            Value = value;
            Expires = expires;
            HttpOnly = false;
            Secure = false;
            SameSite = HttpCookieSameSite.Unspecified;
            Domain = null;
            Path = null;
        }

        public override string ToString()
        {
            //Set-Cookie: ckGeneric=CookieBody; expires=Sun, 30-Dec-2001 21:00:00 GMT; domain=.com.au; path=/
            //Set-Cookie: SessionID=another; expires=Fri, 29 Jun 2006 20:47:11 UTC; path=/
            var cookie = Name + "=" + Value;

            if (Expires.Ticks != 0)
                cookie += "; expires=" + Expires.ToUniversalTime().ToString("r", CultureInfo.InvariantCulture);

            if (Domain != null)
                cookie += "; domain=" + Domain;

            if (Path != null)
                cookie += "; path=" + Path;

            if (HttpOnly)
                cookie += "; HttpOnly";

            if (Secure)
                cookie += "; Secure";

            if (SameSite != HttpCookieSameSite.Unspecified)
                cookie += "; SameSite=" + SameSite;

            return cookie;
        }
    }

}
