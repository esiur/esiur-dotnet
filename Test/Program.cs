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
using Esiur.Core;
using Esiur.Net.HTTP;
using Esiur.Net.IIP;
using Esiur.Net.Sockets;
using Esiur.Resource;
using Esiur.Security.Permissions;
using Esiur.Stores;
using Esiur.Stores.MongoDB;
using System;
using System.Threading;
using System.Threading.Tasks;

using Esiur.Security.Integrity;
using System.Linq;

namespace Test
{
    class Program
    {
        static MyObject localObject;
        static IResource remoteObject;



        static async Task Main(string[] args)
        {


            Warehouse.Protocols.Add("iip", () => new DistributedConnection());

            // Create stores to keep objects.
            var system = Warehouse.New<MemoryStore>("system");
            var remote = Warehouse.New<MemoryStore>("remote");
            var mongo = Warehouse.New<MongoDBStore>("db");


            // Create new distributed server object
            var iip = Warehouse.New<DistributedServer>("iip", system);
            // Set membership which handles authentication.
            iip.Membership = Warehouse.New<MyMembership>("ms", system);
            // Start the server on port 5000.
            iip.Start(new TCPSocket(new System.Net.IPEndPoint(System.Net.IPAddress.Any, 5000)), 600000, 60000);


            // Create http server to handle IIP over Websockets
            var http = Warehouse.New<HTTPServer>("http", system);
            http.Start(new TCPSocket(new System.Net.IPEndPoint(System.Net.IPAddress.Any, 5001)), 600000, 60000);

            // Create IIP over Websocket HTTP module and give it to HTTP server.
            var wsOverHttp = Warehouse.New<IIPoWS>("IIPoWS", system, http);

            wsOverHttp.DistributedServer = iip;


            /*
            var system = await Warehouse.Get("mem://system").Task;
            var remote = await Warehouse.Get("mem://remote").Task;
            var mongo = await Warehouse.Get("mongo://db").Task;
            var iip = await Warehouse.Get("iip://:5000").Task;
            var iws = await Warehouse.Get("iipows://:5001", new Structure() { ["iip"] = iip }).Task;
            */

            var ok = await Warehouse.Open();


            // Open the warehouse


            // Create new object if the store is empty
            if (mongo.Count == 0)
                localObject = Warehouse.New<MyObject>("my", mongo, null,
                    new UserPermissionsManager(new Structure()
                    {
                        ["demo@localhost"] = new Structure()
                        {
                            ["Subtract"] = new Structure { ["Execute"] = "yes" },
                            ["Stream"] = new Structure { ["Execute"] = "yes" },
                            ["_attach"] = "yes",
                            ["_get_attributes"] = "yes",
                            ["_set_attributes"] = "yes",
                        }
                    }));
            else
                localObject = (MyObject)(await Warehouse.Get("db/my"));//.Then((o) => { myObject = (MyObject)o; });


            //var obj = ProxyObject.<MyObject>();
            //Warehouse.Put(obj, "dd", system);
            //obj.Level2= 33;


            Warehouse.StoreConnected += (store, name) =>
            {
                if (store.Instance.Parents.Contains(iip))
                {
                    store.Get("local/js").Then((r) =>
                    {
                        if (r != null)
                        {
                            dynamic d = r;
                            d.send("Welcome");
                        }
                    });
                }
            };

            // Start testing
            TestClient();

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
                {
                    localObject.Level = 88;
                    Console.WriteLine(localObject.Name + " " + localObject.Level);
                }
            }


        }

        private static async void TestClient()
        {

            remoteObject = await Warehouse.Get("iip://localhost:5000/db/my", new Structure() { ["username"] = "demo", ["password"] = "1234" });

            dynamic x = remoteObject;

            
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

    
            (x.Stream(10) as AsyncReply<object>).Then(r =>
            {
                Console.WriteLine("Stream ended: " + r);
            }).Chunk(r =>
            {
                Console.WriteLine("Chunk..." + r);
            }).Progress((t, v, m) => Console.WriteLine("Processing {0}/{1}", v, m));

            var rt = await x.Subtract(10);


            Console.WriteLine(rt);
            // Getting object record
            (remoteObject.Instance.Store as DistributedConnection).GetRecord(remoteObject, DateTime.Now - TimeSpan.FromDays(1), DateTime.Now).Then(record =>
            {
                Console.WriteLine("Records received: " + record.Count);
            });

            //var timer = new Timer(T_Elapsed, null, 5000, 5000);
            

        }

        private static void T_Elapsed(object state)
        {
            localObject.Level++;
            dynamic o = remoteObject;
            Console.WriteLine(localObject.Level + " " + o.Level + o.Me.Me.Level);
        }
    }
}

