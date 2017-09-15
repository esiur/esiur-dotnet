/*
 
Copyright(c) Ahmed Kh. Zamil

All rights reserved.

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

using Esiur.Engine;
using Esiur.Net.HTTP;
using Esiur.Net.IIP;
using Esiur.Net.Sockets;
using Esiur.Resource;
using Esiur.Stores;
using Esiur.Stores.MongoDB;
using System;
using System.Threading;

namespace Test
{
    class Program
    {
        static MyObject myObject;
        static DistributedResource remoteObject;

        static void Main(string[] args)
        {

            var system = Warehouse.New<MemoryStore>("system");
            var remote = Warehouse.New<MemoryStore>("remote");
            var mongo = Warehouse.New<MongoDBStore>("db");

            Warehouse.Open().Then((ok)=> {
                if (mongo.Count == 0)
                    myObject = Warehouse.New<MyObject>("my", mongo);
                else
                    Warehouse.Get("db/my").Then((o) => { myObject = (MyObject)o; });

                var iip = Warehouse.New<DistributedServer>("iip", system);
                iip.Membership = new MyMembership();
                iip.Start(new TCPSocket(new System.Net.IPEndPoint(System.Net.IPAddress.Any, 5000)), 600000, 60000);



                var http = Warehouse.New<HTTPServer>("http", system);
                http.Start(new TCPSocket(new System.Net.IPEndPoint(System.Net.IPAddress.Any, 5001)), 600000, 60000);

                var wsOverHttp = Warehouse.New<IIPoWS>("IIPoWS", system, http);


                TestClient();
            });




            var running = true;

            while (running)
            {
                var cmd = Console.ReadLine();
                if (cmd.ToLower() == "exit")
                    Warehouse.Close().Then((x) =>
                    {
                        if (!x)
                            Console.WriteLine("Failed to close the warehouse.");
                        else
                            Console.WriteLine("Successfully closed the warehouse.");

                        running = false;
                    });
                else
                    Console.WriteLine(myObject.Name + " " + myObject.Level);
            }
        }

        private static void TestClient()
        {
            var client = new DistributedConnection(new TCPSocket("localhost", 5000), "any", "ahmed", "password");

            var remote = Warehouse.GetStore("remote");

           
            Warehouse.Put(client, client.RemoteEndPoint.ToString(), remote);
            

            client.OnReady += (c) =>
            {
                client.Get("db/my").Then((dynamic x) =>
                {
                    remoteObject = x;

                    Console.WriteLine("My Name is: " + x.Name);
                    x.Name = "Hamoo";
                    x.LevelUp += new DistributedResourceEvent((sender, parameters) =>
                    {
                        Console.WriteLine("LevelUp " + parameters[0] + " " + parameters[1]);
                    });

                    x.LevelDown += new DistributedResourceEvent((sender, parameters) =>
                    {
                        Console.WriteLine("LevelUp " + parameters[0] + " " + parameters[1]);
                    });

                    (x.Add(10) as AsyncReply).Then((r) =>
                    {
                        Console.WriteLine("RT: " + r + " " + x.Level);
                    });

                    (x.Subtract(10) as AsyncReply).Then((r) =>
                    {
                        Console.WriteLine("RT: " + r + " " + x.Level);
                    });


                    var t = new Timer(T_Elapsed, null, 5000, 5000);

                });
            };
        }

        private static void T_Elapsed(object state)
        {
            myObject.Level++;
            dynamic o = remoteObject;


            Console.WriteLine(myObject.Level + " " + o.Level + o.Me.Me.Level);

            Console.WriteLine(o.Info.ToString());
        }
    }
}

