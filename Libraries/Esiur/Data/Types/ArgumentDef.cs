using Esiur.Core;
using Esiur.Data;
using Esiur.Protocol;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Text;

namespace Esiur.Data.Types;

public class ArgumentDef
{
    public string Name { get; set; }

    public bool Optional { get; set; }

    public Tru Type { get; set; }

    public ParameterInfo ParameterInfo { get; set; }

    public int Index { get; set; }

    public Map<string, string> Annotations { get; set; }

    public static async AsyncReply<ParseResult<ArgumentDef>> ParseAsync(byte[] data, uint offset, int index, EpConnection connection, ulong[] requestSequence)
    {
        var optional = (data[offset] & 0x1) == 0x1;
        var hasAnnotations = (data[offset++] & 0x2) == 0x2;

        var cs = (uint)data[offset++];
        var name = data.GetString(offset, cs);
        offset += cs;
        var type = await Tru.ParseAsync(data, offset, connection, requestSequence);

        offset += type.Size;

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

        return new ParseResult<ArgumentDef>(new ArgumentDef()
        {
            Name = name,
            Index = index,
            Type = type.Value,
            Optional = optional,
            Annotations = annotations
        }, cs + 2 + type.Size);
    }

    public ArgumentDef()
    {

    }

  
    public override string ToString()
    {
        if (Optional)
            return $"[{Name}: {Type}]";
        else
            return $"{Name}: {Type} ";
    }

    public byte[] Compose(EpConnection connection)
    {
        var name = DC.ToBytes(Name);

        if (Annotations == null)
        {
            return new BinaryList()
                    .AddUInt8(Optional ? (byte)1 : (byte)0)
                    .AddUInt8((byte)name.Length)
                    .AddUInt8Array(name)
                    .AddUInt8Array(Type.Compose(connection))
                    .ToArray();
        }
        else
        {
            var exp = Codec.Compose(Annotations, connection.Instance.Warehouse, connection);

            return new BinaryList()
                    .AddUInt8((byte)(0x2 | (Optional ? 1 : 0)))
                    .AddUInt8((byte)name.Length)
                    .AddUInt8Array(name)
                    .AddUInt8Array(Type.Compose(connection))
                    .AddUInt8Array(exp)
                    .ToArray();
        }
    }
}
