using Esiur.Data;
using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;

namespace Esiur.Resource.Template
{
    public class ArgumentTemplate
    {
        public string Name { get; set; }

        public TemplateDataType Type { get; set; }

        public ParameterInfo ParameterInfo { get; set; }

        public static (uint, ArgumentTemplate) Parse(byte[] data, uint offset)
        {
            var cs = (uint)data[offset++];
            var name = DC.GetString(data, offset, cs);
            offset += cs;
            var (size, type) = TemplateDataType.Parse(data, offset);

            return (cs + 1 + size, new ArgumentTemplate(name, type));
        }

        public ArgumentTemplate()
        {

        }

        public ArgumentTemplate(string name, TemplateDataType type)
        {
            Name = name;
            Type = type;
        }

        public byte[] Compose()
        {
            var name = DC.ToBytes(Name);

            return new BinaryList()
                    .AddUInt8((byte)name.Length)
                    .AddUInt8Array(name)
                    .AddUInt8Array(Type.Compose())
                    .ToArray();
        }
    }
}
