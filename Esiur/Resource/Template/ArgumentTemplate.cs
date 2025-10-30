using Esiur.Data;
using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;

namespace Esiur.Resource.Template;
public class ArgumentTemplate
{
    public string Name { get; set; }

    public bool Optional { get; set; }

    public TRU Type { get; set; }

    public ParameterInfo ParameterInfo { get; set; }

    public int Index { get; set; }

    public static (uint, ArgumentTemplate) Parse(byte[] data, uint offset, int index)
    {
        var optional = (data[offset++] & 0x1) == 0x1;

        var cs = (uint)data[offset++];
        var name = data.GetString(offset, cs);
        offset += cs;
        var (size, type) = TRU.Parse(data, offset);

        return (cs + 2 + size, new ArgumentTemplate(name, index, type, optional));
    }

    public ArgumentTemplate()
    {

    }

    public ArgumentTemplate(string name, int index, TRU type, bool optional)
    {
        Name = name;
        Index = index;
        Type = type;
        Optional = optional;
    }

    public byte[] Compose()
    {
        var name = DC.ToBytes(Name);

        return new BinaryList()
                .AddUInt8(Optional ? (byte)1 : (byte)0)
                .AddUInt8((byte)name.Length)
                .AddUInt8Array(name)
                .AddUInt8Array(Type.Compose())
                .ToArray();
    }
}
