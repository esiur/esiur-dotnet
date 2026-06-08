using System;
using Esiur.Resource;
using Esiur.Core;
using Esiur.Data;
using Esiur.Protocol;
#nullable enable
namespace Esiur.Tests.RPC.EsiurServer
{
    [Remote("Esiur.Tests.RPC.EsiurServer.TestObject", "")]
    public class TestObject : EpResource
    {
        public TestObject(EpConnection connection, uint instanceId, ulong age, string link) : base(connection, instanceId, age, link) { }
        public TestObject() { }
        [Annotation("", "String")]
        [Export]
        public string Name
        {
            get => (string)_properties[0];
            set => SetResourceProperty(0, value);
        }
        [Annotation("", "Int32")]
        [Export]
        public int Size
        {
            get => (int)_properties[1];
            set => SetResourceProperty(1, value);
        }
        [Annotation("", "Object")]
        [Export]
        public object Value
        {
            get => (object)_properties[2];
            set => SetResourceProperty(2, value);
        }

    }
}
