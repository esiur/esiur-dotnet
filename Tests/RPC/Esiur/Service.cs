using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Esiur.Core;
using Esiur.Data;
using Esiur.Protocol;
using Esiur.Resource;

namespace Esiur.Tests.RPC.EsiurServer
{
    [Resource]
    public partial class Service : EntryPoint
    {

        public static bool[] GenerateRandomBoolSequence(
      int length,
      double probabilityTrue,
      Random? rng = null)
        {
            if (length <= 0)
                throw new ArgumentOutOfRangeException(nameof(length));

            if (probabilityTrue < 0.0 || probabilityTrue > 1.0)
                throw new ArgumentOutOfRangeException(nameof(probabilityTrue));

            rng ??= Random.Shared;

            int trueTarget = (int)Math.Round(length * probabilityTrue);
            int remaining = length;
            int remainingTrue = trueTarget;

            var result = new bool[length];

            for (int i = 0; i < length; i++)
            {
                // Probability adjusted to guarantee exact total
                double p = (double)remainingTrue / remaining;
                bool value = rng.NextDouble() < p;

                result[i] = value;

                if (value)
                    remainingTrue--;

                remaining--;
            }

            return result;
        }
        public List<TestObject> TestObjects = new List<TestObject>();

        [Export]
        public event ResourceEventHandler<byte[]> MessageUpdated;

        [Export]

        public byte[] messageToChange;

        [Export]
        public object testProperty;

        // ---------- Unary: scalars & bytes ----------
        [Export]
        public byte[] EchoBytes(byte[] payload)
            => payload;
        [Export]

        public BusinessDocument[] EchoDocuments(BusinessDocument[] payload)
            => payload;
        [Export]

        public int[] EchoIntArray(int[] payload)
            => payload;
        [Export]

        public string[] EchoStringArray(string[] payload)
            => payload;
        [Export]

        public Map<string, BusinessDocument> EchoMap(Map<string, BusinessDocument> payload)
            => payload;
        [Export]

        public DocType[] EchoEnumArray(DocType[] payload)
            => payload;

        [Export]

        public AsyncReply<byte[]> ChunkTest(int count, int size, int delay)
        {
            var rt = new AsyncReply<byte[]>();


            Task.Run(async () =>
            {
                await Task.Delay(3000);

                for (int i = 0; i < count; i++)
                {
                    byte[] chunk = new byte[size];
                    new Random().NextBytes(chunk);
                    rt.TriggerChunk(chunk);
                    await Task.Delay(delay);
                }

                rt.Trigger(new byte[0]);
            });

            return rt;
        }


        [Export]
        public async void PropertyChangeTest(int count, int size, int delay)
        {
            await Task.Delay(3000);

            for (int i = 0; i < count; i++)
            {
                byte[] chunk = new byte[size];
                new Random().NextBytes(chunk);

                MessageToChange = chunk;
                await Task.Delay(delay);
            }
        }

        [Export]
        public async void EventTest(int count, int size, int delay)
        {
            await Task.Delay(3000);

            for (int i = 0; i < count; i++)
            {
                byte[] chunk = new byte[size];
                new Random().NextBytes(chunk);

                MessageUpdated?.Invoke(chunk);
                await Task.Delay(delay);
            }
        }


        static Random rand = new Random(322221);

        [Export]
        public async AsyncReply<TestObject> StartUpdatesLocal(int interval, int count, double localProbability)
        {

            var dis = GenerateRandomBoolSequence(count, localProbability, new Random(2222));

            for (var i = 0; i < count; i++)
            {
                //var probability = rand.NextDouble();

                if (dis[i])// probability <= localProbability)
                {
                    var o = await Warehouse.Default.New<TestObject>("sys/anything");

                    o.Value = i;
                    o.Name = "Update " + i;

                    TestObjects.Add(o);

                    TestProperty = o;
                }
                else
                {
                    TestProperty = i;
                }

                await Task.Delay(interval);

            }

            return null;
        }

        [Export]
        public async AsyncReply<ResourceLink<TestObject>> StartUpdatesRemote(int interval, int count, double remoteProbability, string remoteLink)
        {
            for (var i = 0; i < count; i++)
            {
                var probability = rand.NextDouble();

                if (probability <= remoteProbability)
                {
                    TestProperty = remoteLink;
                }
                else
                {
                    TestProperty = i;
                }

                await Task.Delay(interval);

            }

            return null;
        }


        [Export]
        public async AsyncReply<TestObject> StartUpdatesMirror(int interval, int count, double remoteProbability, string remoteNode, string remoteLink)
        {

            var remoteCon = await Warehouse.Default.Get<EpConnection>(remoteNode);

            for (var i = 0; i < count; i++)
            {
                var probability = rand.NextDouble();

                if (probability <= remoteProbability)
                {
                    var o = await remoteCon.Get(remoteLink);
                    TestObjects.Add(o as TestObject);

                    TestProperty = o;
                }
                else
                {
                    TestProperty = i;
                }

                await Task.Delay(interval);

            }

            return null;
        }



        [Export]
        public async AsyncReply<ResourceLink<TestObject>> StartUpdates(int interval, int count, double localProbability, double remoteProbability, string remoteHostLink)
        {

            //var created = new List<TestObject>();

            //for (var i = 0; i < count; i++)
            //{
            //    var o = await Warehouse.Default.New<TestObject>("sys/anything");
            //    o.Value = i;
            //    o.Name = "Update " + i;
            //    created.Add(o);
            //}

            for (var i = 0; i < count; i++)
            {

                //TestProperty = created[rand.Next(999)];
                //await Task.Delay(interval);

                // Console.WriteLine("Updating " + i);
                var probability = rand.NextDouble();

                if ((localProbability != 0 && probability <= localProbability) || localProbability == 1)
                {
                    var o = await Warehouse.Default.New<TestObject>("sys/anything");

                    o.Value = i;
                    o.Name = "Update " + i;

                    TestObjects.Add(o);

                    TestProperty = o;
                }
                else if (probability < localProbability + remoteProbability)
                {
                    TestProperty = new ResourceLink(remoteHostLink);
                }
                else
                {
                    TestProperty = i;
                }

                await Task.Delay(interval);
            }

            TestObjects.Clear();

            return null;
        }

        public override async AsyncReply<IResource> Query(string path, EpConnection sender)
        {
            if (path == "gen")
            {
                var o = await Warehouse.Default.New<TestObject>("sys/anything");
                o.Value = rand.Next();
                o.Name = "Update " + o.Value;
                TestObjects.Add(o);
                return o;
            }
            else
            {
                if (this.Instance != null)
                    return await this.Instance.Warehouse.Query(path);
                else
                    return null;
            }
        }

        protected override bool Create()
        {
            return true;
        }
    }
}
