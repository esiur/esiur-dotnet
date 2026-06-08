using System;
using Esiur.Resource;
using Esiur.Core;
using Esiur.Data;
using Esiur.Protocol;
#nullable enable
namespace Esiur.Tests.RPC.EsiurServer
{
    [Remote("Esiur.Tests.RPC.EsiurServer.Service", "localhost")]
    public class Service : EpResource
    {
        public Service(EpConnection connection, uint instanceId, ulong age, string link) : base(connection, instanceId, age, link) { }
        public Service() { }
        [Annotation("", "([Int32] count,[Int32] size,[Int32] delay) -> AsyncReply`1")]
        [Export]
        public AsyncReply<byte[]> ChunkTest(int count, int size, int delay)
        {
            var args = new Map<byte, object>() { [0] = count, [1] = size, [2] = delay };
            var rt = new AsyncReply<byte[]>();
            _Invoke(0, args)
            .Then(x => rt.Trigger((byte[])x))
            .Error(x => rt.TriggerError(x))
            .Chunk(x => rt.TriggerChunk(x));
            return rt;
        }
        [Annotation("", "([Byte[]] payload) -> Byte[]")]
        [Export]
        public AsyncReply<byte[]> EchoBytes(byte[] payload)
        {
            var args = new Map<byte, object>() { [0] = payload };
            var rt = new AsyncReply<byte[]>();
            _Invoke(1, args)
            .Then(x => rt.Trigger((byte[])x))
            .Error(x => rt.TriggerError(x))
            .Chunk(x => rt.TriggerChunk(x));
            return rt;
        }

        [Annotation("", "([BusinessDocument[]] payload) -> BusinessDocument[]")]
        [Export]
        public AsyncReply<Esiur.Tests.RPC.EsiurServer.BusinessDocument[]> EchoDocuments(Esiur.Tests.RPC.EsiurServer.BusinessDocument[] payload)
        {
            var args = new Map<byte, object>() { [0] = payload };
            var rt = new AsyncReply<Esiur.Tests.RPC.EsiurServer.BusinessDocument[]>();
            _Invoke(2, args)
            .Then(x => rt.Trigger((Esiur.Tests.RPC.EsiurServer.BusinessDocument[])x))
            .Error(x => rt.TriggerError(x))
            .Chunk(x => rt.TriggerChunk(x));
            return rt;
        }

        [Annotation("", "([DocType[]] payload) -> DocType[]")]
        [Export]
        public AsyncReply<Esiur.Tests.RPC.EsiurServer.DocType[]> EchoEnumArray(Esiur.Tests.RPC.EsiurServer.DocType[] payload)
        {
            var args = new Map<byte, object>() { [0] = payload };
            var rt = new AsyncReply<Esiur.Tests.RPC.EsiurServer.DocType[]>();
            _Invoke(3, args)
            .Then(x => rt.Trigger((Esiur.Tests.RPC.EsiurServer.DocType[])x))
            .Error(x => rt.TriggerError(x))
            .Chunk(x => rt.TriggerChunk(x));
            return rt;
        }
        [Annotation("", "([Int32[]] payload) -> Int32[]")]
        [Export]
        public AsyncReply<int[]> EchoIntArray(int[] payload)
        {
            var args = new Map<byte, object>() { [0] = payload };
            var rt = new AsyncReply<int[]>();
            _Invoke(4, args)
            .Then(x => rt.Trigger((int[])x))
            .Error(x => rt.TriggerError(x))
            .Chunk(x => rt.TriggerChunk(x));
            return rt;
        }
        [Annotation("", "([Map`2] payload) -> Map`2")]
        [Export]
        public AsyncReply<Map<string, Esiur.Tests.RPC.EsiurServer.BusinessDocument>> EchoMap(Map<string, Esiur.Tests.RPC.EsiurServer.BusinessDocument> payload)
        {
            var args = new Map<byte, object>() { [0] = payload };
            var rt = new AsyncReply<Map<string, Esiur.Tests.RPC.EsiurServer.BusinessDocument>>();
            _Invoke(5, args)
            .Then(x => rt.Trigger((Map<string, Esiur.Tests.RPC.EsiurServer.BusinessDocument>)x))
            .Error(x => rt.TriggerError(x))
            .Chunk(x => rt.TriggerChunk(x));
            return rt;
        }
        [Annotation("", "([String[]] payload) -> String[]")]
        [Export]
        public AsyncReply<string[]> EchoStringArray(string[] payload)
        {
            var args = new Map<byte, object>() { [0] = payload };
            var rt = new AsyncReply<string[]>();
            _Invoke(6, args)
            .Then(x => rt.Trigger((string[])x))
            .Error(x => rt.TriggerError(x))
            .Chunk(x => rt.TriggerChunk(x));
            return rt;
        }
        [Annotation("", "([Int32] count,[Int32] size,[Int32] delay) -> Void")]
        [Export]
        public AsyncReply<object> EventTest(int count, int size, int delay)
        {
            var args = new Map<byte, object>() { [0] = count, [1] = size, [2] = delay };
            var rt = new AsyncReply<object>();
            _Invoke(7, args)
            .Then(x => rt.Trigger((object)x))
            .Error(x => rt.TriggerError(x))
            .Chunk(x => rt.TriggerChunk(x));
            return rt;
        }
        [Annotation("", "([Int32] count,[Int32] size,[Int32] delay) -> Void")]
        [Export]
        public AsyncReply<object> PropertyChangeTest(int count, int size, int delay)
        {
            var args = new Map<byte, object>() { [0] = count, [1] = size, [2] = delay };
            var rt = new AsyncReply<object>();
            _Invoke(8, args)
            .Then(x => rt.Trigger((object)x))
            .Error(x => rt.TriggerError(x))
            .Chunk(x => rt.TriggerChunk(x));
            return rt;
        }
        [Annotation("", "([Int32] interval,[Int32] count,[Double] localProbability,[Double] remoteProbability,[String] remoteHostLink) -> AsyncReply`1")]
        [Export]
        public AsyncReply<Esiur.Tests.RPC.EsiurServer.TestObject> StartUpdates(int interval, int count, double localProbability, double remoteProbability, string remoteHostLink)
        {
            var args = new Map<byte, object>() { [0] = interval, [1] = count, [2] = localProbability, [3] = remoteProbability, [4] = remoteHostLink };
            var rt = new AsyncReply<Esiur.Tests.RPC.EsiurServer.TestObject>();
            _Invoke(9, args)
            .Then(x => rt.Trigger((Esiur.Tests.RPC.EsiurServer.TestObject)x))
            .Error(x => rt.TriggerError(x))
            .Chunk(x => rt.TriggerChunk(x));
            return rt;
        }
        [Annotation("", "([Int32] interval,[Int32] count,[Double] localProbability) -> AsyncReply`1")]
        [Export]
        public AsyncReply<Esiur.Tests.RPC.EsiurServer.TestObject> StartUpdatesLocal(int interval, int count, double localProbability)
        {
            var args = new Map<byte, object>() { [0] = interval, [1] = count, [2] = localProbability };
            var rt = new AsyncReply<Esiur.Tests.RPC.EsiurServer.TestObject>();
            _Invoke(10, args)
            .Then(x => rt.Trigger((Esiur.Tests.RPC.EsiurServer.TestObject)x))
            .Error(x => rt.TriggerError(x))
            .Chunk(x => rt.TriggerChunk(x));
            return rt;
        }
        [Annotation("", "([Int32] interval,[Int32] count,[Double] remoteProbability,[String] remoteNode,[String] remoteLink) -> AsyncReply`1")]
        [Export]
        public AsyncReply<Esiur.Tests.RPC.EsiurServer.TestObject> StartUpdatesMirror(int interval, int count, double remoteProbability, string remoteNode, string remoteLink)
        {
            var args = new Map<byte, object>() { [0] = interval, [1] = count, [2] = remoteProbability, [3] = remoteNode, [4] = remoteLink };
            var rt = new AsyncReply<Esiur.Tests.RPC.EsiurServer.TestObject>();
            _Invoke(11, args)
            .Then(x => rt.Trigger((Esiur.Tests.RPC.EsiurServer.TestObject)x))
            .Error(x => rt.TriggerError(x))
            .Chunk(x => rt.TriggerChunk(x));
            return rt;
        }
        [Annotation("", "([Int32] interval,[Int32] count,[Double] remoteProbability,[String] remoteLink) -> AsyncReply`1")]
        [Export]
        public AsyncReply<Esiur.Tests.RPC.EsiurServer.TestObject> StartUpdatesRemote(int interval, int count, double remoteProbability, string remoteLink)
        {
            var args = new Map<byte, object>() { [0] = interval, [1] = count, [2] = remoteProbability, [3] = remoteLink };
            var rt = new AsyncReply<Esiur.Tests.RPC.EsiurServer.TestObject>();
            _Invoke(12, args)
            .Then(x => rt.Trigger((Esiur.Tests.RPC.EsiurServer.TestObject)x))
            .Error(x => rt.TriggerError(x))
            .Chunk(x => rt.TriggerChunk(x));
            return rt;
        }
        [Annotation("", "Byte[]")]
        [Export]
        public byte[] MessageToChange
        {
            get => (byte[])_properties[0];
            set => SetResourceProperty(0, value);
        }
        [Annotation("", "Object")]
        [Export]
        public object TestProperty
        {
            get => (object)_properties[1];
            set => SetResourceProperty(1, value);
        }
        protected override void _EmitEventByIndex(byte index, object args)
        {
            switch (index)
            {
                case 0: MessageUpdated?.Invoke((byte[])args); break;
            }
        }
        [Export] public event ResourceEventHandler<byte[]> MessageUpdated;


    }
}
