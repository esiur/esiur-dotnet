using System;
using Esiur.Resource;
using Esiur.Core;
using Esiur.Data;
using Esiur.Protocol;
#nullable enable
namespace RPC.EsiurTest
{
    [TypeId("d90d3558e2b18d9a8f45707372ddf2c3")]
    public class TestObject : EpResource
    {
        public TestObject(EpConnection connection, uint instanceId, ulong age, string link) : base(connection, instanceId, age, link) { }
        public TestObject() { }
        [Annotation("String")]
        [Export]
        public string Name
        {
            get => (string)properties[0];
            set => SetResourceProperty(0, value);
        }
        [Annotation("Int32")]
        [Export]
        public int Size
        {
            get => (int)properties[1];
            set => SetResourceProperty(1, value);
        }
        [Annotation("Object")]
        [Export]
        public object Value
        {
            get => (object)properties[2];
            set => SetResourceProperty(2, value);
        }

    }
}
