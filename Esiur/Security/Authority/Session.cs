﻿/*
 
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
using Esiur.Core;
using Esiur.Net;
using Esiur.Resource;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Esiur.Security.Cryptography;
using static System.Collections.Specialized.BitVector32;
using Esiur.Net.Packets;

namespace Esiur.Security.Authority;
public class Session
{
    public byte[] Id { get; set; }
    public DateTime Creation { get; }
    public DateTime Modification { get; }
    public KeyList<string, object> Variables { get; } = new KeyList<string, object>();



    public IKeyExchanger KeyExchanger { get; set; } = null;
    public ISymetricCipher SymetricCipher { get; set; } = null;


    public Map<IIPAuthPacketHeader, object> LocalHeaders { get; set; } = new Map<IIPAuthPacketHeader, object>();
    public Map<IIPAuthPacketHeader, object> RemoteHeaders { get; set; } = new Map<IIPAuthPacketHeader, object>();

    public AuthenticationMethod LocalMethod { get; set; }
    public AuthenticationMethod RemoteMethod { get; set; }

    public AuthenticationType AuthenticationType { get; set; }


    public string AuthorizedAccount { get; set; }

 

}
