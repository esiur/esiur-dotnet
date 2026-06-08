using Esiur.Net.Sockets;
using Esiur.Resource;
using RPC.EsiurTest;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace RPC.Client.Tests.Events
{
    public class EsiurEventsTest
    {

        public enum TestType
        {
            Chunk,
            Event,
            Property
        }  

        public static async Task DoTest(TestType type, string address, int count, int size, int delay)
        {
            var rt = new TestResults();
            //using var mon = new PerProcessNetMonitor(Process.GetCurrentProcess().Id);
            //mon.Start();


            var service = await Warehouse.Default.Get<Service>(address);
            var sock = service.ResourceConnection.Socket as TcpSocket;

            
            //await Task.Delay(3000);

            //var (tx, rx, ctx, crx) = mon.GetDiff(0, 0);

            if (type == TestType.Event) 
                 service.EventTest(count, size, delay);
            else if (type == TestType.Property)
                 service.PropertyChangeTest(count, size, delay);
            else if (type == TestType.Chunk)
                 service.ChunkTest(count, size, delay);

            var crx = sock.BytesReceived;
            var ctx = sock.BytesSent;

            Console.WriteLine($"Handshake {ctx}/{crx}");


            await Task.Delay(7000 + (count * size));

             crx = sock.BytesReceived - crx;
             ctx = sock.BytesSent - ctx;

            //(tx, rx, ctx, crx) = mon.GetDiff(tx, rx);
            //Console.WriteLine($"Results {ctx}/{crx} Total: {tx}/{rx}");
            Console.WriteLine($"Results {ctx}/{crx}");


        }


        public static async Task DoEventTest(string address, int count, int size, int delay)
        {
            var rt = new TestResults();
            using var mon = new PerProcessNetMonitor(Process.GetCurrentProcess().Id);
            mon.Start();


            var service = await Warehouse.Default.Get<Service>(address);

            await service.EventTest(count, size, delay);

            await Task.Delay(3000);

            var (tx, rx, ctx, crx) = mon.GetDiff(0, 0);

            Console.WriteLine($"Handshake {ctx}/{crx}");



            await Task.Delay(7000 + (count * delay));

            (tx, rx, ctx, crx) = mon.GetDiff(tx, rx);
            Console.WriteLine($"Results {ctx}/{crx} Total: {tx}/{rx}");

        }

        public static async Task DoPropertyTest(string address, int count, int size, int delay)
        {
            var rt = new TestResults();
            using var mon = new PerProcessNetMonitor(Process.GetCurrentProcess().Id);
            mon.Start();


            var service = await Warehouse.Default.Get<Service>(address);

            await Task.Delay(3000);

            var (tx, rx, ctx, crx) = mon.GetDiff(0, 0);

            Console.WriteLine($"Handshake {ctx}/{crx}");

            await service.PropertyChangeTest(count, size, delay);


            await Task.Delay(3000 + (size * delay));

            (tx, rx, ctx, crx) = mon.GetDiff(tx, rx);
            Console.WriteLine($"Results {ctx}/{crx} Total: {tx}/{rx}");

        }

    }
}
