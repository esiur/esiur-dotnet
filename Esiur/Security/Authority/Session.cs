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
using Esiur.Data;
using Esiur.Engine;
using Esiur.Net;
using Esiur.Resource;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Security.Authority
{
    public class Session
    {
        public Authentication LocalAuthentication => localAuth;
        public Authentication RemoteAuthentication => remoteAuth;

       // public Source Source { get; }
        public byte[] Id { get; set; }
        public DateTime Creation { get; }
        public DateTime Modification { get; }
        public KeyList<string, object> Variables {get;} = new KeyList<string, object>();

         //KeyList<string, object> Variables { get; }
        //IStore Store { get; }

        //string id;
        Authentication localAuth, remoteAuth;
        //string domain;

        public Session(Authentication localAuthentication, Authentication remoteAuthentication)
        {
             
            this.localAuth = localAuthentication;
            this.remoteAuth = remoteAuthentication;
        }
    }
}
