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
using Esiur.Net.IIP;
using Esiur.Security.Authority;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Resource;

//public delegate R DCovariant<out R>();

public delegate void ResourceEventHandler<in T>(T argument);//where T : class;
// public delegate void CustomUsersEventHanlder(string[] usernames, params object[] args);
//public delegate void CustomReceiversEventHanlder(DistributedConnection[] connections, params object[] args);
//public delegate void CustomInquirerEventHanlder(object inquirer, params object[] args);

public delegate void CustomResourceEventHandler<in T>(object issuer, Func<Session, bool> receivers, T argument);// object issuer, Session[] receivers, params object[] args);

// public delegate void CustomReceiversEventHanlder(string[] usernames, DistributedConnection[] connections, params object[] args);

