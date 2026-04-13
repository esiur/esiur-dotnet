using Esiur.Core;
using Esiur.Data;
using Esiur.Protocol;
using Esiur.Resource;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Tests.Queueing.Server
{
    [Resource]
    public partial class QueueingService : EntryPoint
    {
        static Random rand = new Random(322221);
        public List<TestObject> TestObjects = new List<TestObject>();

        [Export]
        public object testProperty;

        [Export]
        public async AsyncReply<TestObject> StartUpdatesLocal(int interval, int count, double localProbability)
        {

            var dis = GenerateRandomBoolSequence(count, localProbability, new Random(2222));

            for (var i = 0; i < count; i++)
            {
                if (dis[i])
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
            for (var i = 0; i < count; i++)
            {
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


        protected override bool Create()
        {
            return true;
        }


    }
}
