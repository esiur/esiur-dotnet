using Esiur.Data;
using Esiur.Engine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Resource
{
    [AttributeUsage(AttributeTargets.All)]
    public class Storable : global::System.Attribute
    {
        public delegate object SerializerFunction(object value);
        public delegate object DeserializerFunction(object data);

        SerializerFunction serializer;
        DeserializerFunction deserializer;
        DataType type;

        public Storable()
        {
            type = DataType.Void;
        }

        public DeserializerFunction Deserializer
        {
            get { return deserializer; }
        }

        public SerializerFunction Serializer
        {
            get { return serializer; }
        }

        public Storable(DataType type)
        {
            this.type = type;
        }

        public Storable(DataType type, SerializerFunction serializer, DeserializerFunction deserializer)
        {
            this.type = type;
            this.serializer = serializer;
            this.deserializer = deserializer;
        }
    }
}
