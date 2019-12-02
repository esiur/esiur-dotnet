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

namespace Esyur.Security.Authority
{
    public class ClientAuthentication : Authentication
    {
        /*
        string username;
        byte[] password;
        string domain;
        byte[] token;
        UserCertificate certificate;

        public string Username => username;
        public byte[] Password => password;
        //public string Domain => domain;
        public byte[] Token => token;

        public byte[] Nounce { get; set; }
        */

        public ClientAuthentication()
            :base(AuthenticationType.Client)
        {

        }


        /*
        public ClientAuthentication(byte[] token)
            : base(AuthenticationType.Client)
        {
            this.token = token;
        }

        public ClientAuthentication(string username, byte[] password) 
            : base(AuthenticationType.Client)
        {
            this.username = username;
            this.password = password;
            //this.domain = domain;
        }
        */
    }
}
