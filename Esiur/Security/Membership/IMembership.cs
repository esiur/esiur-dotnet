﻿/*
 
Copyright (c) 2017-2024 Ahmed Kh. Zamil

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
using Esiur.Data;
using Esiur.Net.IIP;
using Esiur.Core;
using Esiur.Security.Authority;
using Esiur.Resource;
using Esiur.Net.Packets;

namespace Esiur.Security.Membership;

public interface IMembership
{
    public event ResourceEventHandler<AuthorizationIndication> Authorization;

    AsyncReply<string> UserExists(string username, string domain);
    AsyncReply<string> TokenExists(ulong tokenIndex, string domain);

    AsyncReply<byte[]> GetPassword(string username, string domain);
    AsyncReply<byte[]> GetToken(ulong tokenIndex, string domain);
    AsyncReply<AuthorizationResults> Authorize(Session session);
    AsyncReply<AuthorizationResults> AuthorizePlain(Session session, uint reference, object value);
    AsyncReply<AuthorizationResults> AuthorizeHashed(Session session, uint reference, IIPAuthPacketHashAlgorithm algorithm, byte[] value);
    AsyncReply<AuthorizationResults> AuthorizeEncrypted(Session session, uint reference, IIPAuthPacketPublicKeyAlgorithm algorithm, byte[] value);

    AsyncReply<bool> Login(Session session);
    AsyncReply<bool> Logout(Session session);
    bool GuestsAllowed { get; }

}



