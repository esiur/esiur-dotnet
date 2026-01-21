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

    public Map<string, string> Annotations { get; set; }

    public static (uint, ArgumentTemplate) Parse(byte[] data, uint offset, int index)
    {
        var optional = (data[offset] & 0x1) == 0x1;
        var hasAnnotations = (data[offset++] & 0x2) == 0x2;

        var cs = (uint)data[offset++];
        var name = data.GetString(offset, cs);
        offset += cs;
        var (size, type) = TRU.Parse(data, offset);

        offset += size;

        Map<string, string> annotations = null;

        if (hasAnnotations)
        {
            //var acs = data.GetUInt32(offset, Endian.Little);
            //offset += 2;
            var (l, a) = Codec.ParseSync(data, offset, null);
            // for saftey, Map<string, string> might change in the future
            if (a is Map<string, string> ann)
                annotations = ann;

            cs += l;
        }

        return (cs + 2 + size, new ArgumentTemplate()
        {
            Name = name,
            Index = index,
            Type = type,
            Optional = optional,
            Annotations = annotations
        });
    }

    public ArgumentTemplate()
    {

    }

  
    public override string ToString()
    {
        if (Optional)
            return $"[{Name}: {Type}]";
        else
            return $"{Name}: {Type} ";
    }

    public byte[] Compose()
    {
        var name = DC.ToBytes(Name);

        if (Annotations == null)
        {
            return new BinaryList()
                    .AddUInt8(Optional ? (byte)1 : (byte)0)
                    .AddUInt8((byte)name.Length)
                    .AddUInt8Array(name)
                    .AddUInt8Array(Type.Compose())
                    .ToArray();
        }
        else
        {
            var exp = Codec.Compose(Annotations, null, null);

            return new BinaryList()
                    .AddUInt8((byte)(0x2 | (Optional ? 1 : 0)))
                    .AddUInt8((byte)name.Length)
                    .AddUInt8Array(name)
                    .AddUInt8Array(Type.Compose())
                    .AddUInt8Array(exp)
                    .ToArray();
        }
    }
}
