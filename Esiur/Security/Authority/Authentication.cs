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
using System.Threading.Tasks;
using static Esiur.Net.Packets.IIPAuthPacket;

namespace Esiur.Security.Authority
{
    public class Authentication
    {
        AuthenticationType type;

        public AuthenticationMethod Method { get; set; }

        public ulong TokenIndex { get; set; }

        public string Username { get; set; }
        public Certificate Certificate { get; set; }
        public string Domain { get; set; }

        public string FullName => Username + "@" + Domain;

        public Source Source { get; } = new Source();

        public AuthenticationState State
        {
            get;
            set;
        }

        public AuthenticationType Type
        {
            get => type;
        }

        public Authentication(AuthenticationType type)
        {
            this.type = type;
        }
    }
}
