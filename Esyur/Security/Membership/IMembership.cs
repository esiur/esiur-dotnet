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
using Esyur.Data;
using Esyur.Net.IIP;
using Esyur.Core;
using Esyur.Security.Authority;
using Esyur.Resource;

namespace Esyur.Security.Membership
{
    public interface IMembership : IResource
    {
        AsyncReply<bool> UserExists(string username, string domain);
        AsyncReply<byte[]> GetPassword(string username, string domain);
        AsyncReply<byte[]> GetToken(ulong TokenIndex, string domain);
        AsyncReply<bool> Login(Session session);
        AsyncReply<bool> Logout(Session session);

        AsyncReply<string> TokenExists(ulong tokenIndex, string domain);
    }
}
