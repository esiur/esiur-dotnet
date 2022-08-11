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
using Esiur.Net.HTTP;
using Esiur.Net.IIP;
using Esiur.Net.Sockets;
using Esiur.Resource;
using Esiur.Security.Permissions;
using Esiur.Stores;
using System;
using System.Threading;
using System.Threading.Tasks;

using Esiur.Security.Integrity;
using System.Linq;
using Esiur.Resource.Template;
using System.Collections;
using System.Runtime.CompilerServices;
using Esiur.Proxy;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace Test
{



    class Program
    {

        static async Task Main(string[] args)
        {

            // Create stores to keep objects.
            var system = await Warehouse.Put("sys", new MemoryStore());
            var server = await Warehouse.Put("sys/server", new DistributedServer());


            var web = await Warehouse.Put("sys/web", new HTTPServer() { Port = 8088 });

            var service = await Warehouse.Put("sys/service", new MyService());
            var res1 = await Warehouse.Put("sys/service/r1", new MyResource() { Description = "Testing 1", CategoryId = 10 });
            var res2 = await Warehouse.Put("sys/service/r2", new MyResource() { Description = "Testing 2", CategoryId = 11 });
            var res3 = await Warehouse.Put("sys/service/c1", new MyChildResource() { ChildName = "Child 1", Description = "Child Testing 3", CategoryId = 12 });
            var res4 = await Warehouse.Put("sys/service/c2", new MyChildResource() { ChildName = "Child 2 Destroy", Description = "Testing Destroy Handler", CategoryId = 12 });

            server.MapCall("Hello", (string msg, DateTime time, DistributedConnection sender) =>
            {
                Console.WriteLine(msg);
                return "Hi " + DateTime.UtcNow;
            }).MapCall("temp", () => res4);

            service.Resource = res1;
            service.ChildResource = res3;
            service.Resources = new MyResource[] { res1, res2, res1, res3 };

            //web.MapGet("/{action}/{age}", (int age, string action, HTTPConnection sender) =>
            //{
            //    Console.WriteLine($"AGE: {age} ACTION: {action}");

            //    sender.Response.Number = Esiur.Net.Packets.HTTPResponsePacket.ResponseCode.NotFound;
            //    sender.Send("Not found");
            //});

            web.MapGet("/", (HTTPConnection sender) =>
            {
                sender.Send("Hello");
            });

            var ok = await Warehouse.Open();

            // Start testing
            TestClient(service);
            // TestClient(service);
        }



        private static async void TestClient(IResource local)
        {
            var con = await Warehouse.Get<DistributedConnection>("iip://localhost", new { AutoReconnect = true });

            //dynamic remote = await Warehouse.Get<IResource>("iip://localhost/sys/service", new { AutoReconnect = true });

            //var con = remote.Connection as DistributedConnection;

            dynamic remote = await con.Get("sys/service");

            var pcall = await con.Call("Hello", "whats up ?", DateTime.UtcNow);

            var temp = await con.Call("temp");
            Console.WriteLine("Temp: " + temp.GetHashCode());

            var template = await con.GetTemplateByClassName("Test.MyResource");


            TestObjectProps(local, remote);

            var gr = await remote.GetGenericRecord();
            Console.WriteLine(gr);

            var opt = await remote.Optional(new { a1 = 22, a2 = 33, a4 = "What?" });
            Console.WriteLine(opt);

            var hello = await remote.AsyncHello();

            await remote.Void();
            await remote.Connection("ss", 33);
            await remote.ConnectionOptional("Test 2", 88);
            var rt = await remote.Optional("Optiona", 311);
            Console.WriteLine(rt);

            var t2 = await remote.GetTuple2(1, "A");
            Console.WriteLine(t2);
            var t3 = await remote.GetTuple3(1, "A", 1.3);
            Console.WriteLine(t3);
            var t4 = await remote.GetTuple4(1, "A", 1.3, true);
            Console.WriteLine(t4);

            remote.StringEvent += new DistributedResourceEvent((sender, args) =>
               Console.WriteLine($"StringEvent {args}")
            );

            remote.ArrayEvent += new DistributedResourceEvent((sender, args) =>
               Console.WriteLine($"ArrayEvent {args}")
            );

            await remote.InvokeEvents("Hello");



            //var path = TemplateGenerator.GetTemplate("iip://localhost/sys/service", "Generated");

            //Console.WriteLine(path);

            perodicTimer = new Timer(new TimerCallback(perodicTimerElapsed), remote, 0, 1000);
        }

        static async void perodicTimerElapsed(object state)
        {
            GC.Collect();
            try
            {
                dynamic remote = state;
                Console.WriteLine("Perodic : " + await remote.AsyncHello());
            }
            catch (Exception ex)
            {
                Console.WriteLine("Perodic : " + ex.ToString());
            }
        }

        static Timer perodicTimer;

        static void TestObjectProps(IResource local, DistributedResource remote)
        {

            foreach (var pt in local.Instance.Template.Properties)
            {

                var lv = pt.PropertyInfo.GetValue(local);
                object v;
                var rv = remote.TryGetPropertyValue(pt.Index, out v);
                if (!rv)
                    Console.WriteLine($" ** {pt.Name} Failed");
                else
                    Console.WriteLine($"{pt.Name} {GetString(lv)} == {GetString(v)}");
            }

        }

        static string GetString(object value)
        {
            if (value == null)
                return "NULL";

            var t = value.GetType();
            var nt = Nullable.GetUnderlyingType(t);
            if (nt != null)
                t = nt;
            if (t.IsArray)
            {
                var ar = (Array)value;
                if (ar.Length == 0)
                    return "[]";
                var rt = "[";
                for (var i = 0; i < ar.Length - 1; i++)
                    rt += GetString(ar.GetValue(i)) + ",";
                rt += GetString(ar.GetValue(ar.Length - 1)) + "]";

                return rt;
            }
            else if (value is IRecord)
            {
                return "{" + String.Join(", ", t.GetProperties().Select(x => x.Name + ": " + x.GetValue(value))) + "}";
            }

            else
                return value.ToString();
        }

    }




}

