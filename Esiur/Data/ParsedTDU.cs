using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Data
{
    public struct ParsedTDU
    {
        public TDUIdentifier Identifier;
        public int Index;
        public TDUClass Class;
        public uint Offset;
        public ulong ContentLength;
        public byte[] Data;
        public byte Exponent;
        public ulong TotalLength;
        public byte[] Metadata;

        public static (ulong, ParsedTDU?) Parse(byte[] data, uint offset, uint ends)
        {
            var h = data[offset++];

            var cls = (TDUClass)(h >> 6);

            if (cls == TDUClass.Fixed)
            {
                var exp = (h & 0x38) >> 3;

                if (exp == 0)
                    return (1, new ParsedTDU()
                    {
                        Identifier = (TDUIdentifier)h,
                        Data = data,
                        Offset = offset,
                        Class = cls,
                        Exponent = (byte)exp,
                        Index = (byte)h & 0x7,
                        ContentLength = 0,
                        TotalLength = 1,
                    });

                ulong cl = (ulong)(1 << (exp - 1));

                if (ends - offset < cl)
                    return (cl - (ends - offset), null);

                //offset += (uint)cl;

                return (1 + cl, new ParsedTDU()
                {
                    Identifier = (TDUIdentifier)h,
                    Data = data,
                    Offset = offset,
                    Class = cls,
                    ContentLength = cl,
                    TotalLength = 1 + cl,
                    Exponent = (byte)exp,
                    Index = (byte)h & 0x7,
                });
            }
            else if (cls == TDUClass.Typed)
            {
                ulong cll = (ulong)(h >> 3) & 0x7;

                if (ends - offset < cll)
                    return (cll - (ends - offset), null);

                ulong cl = 0;

                for (uint i = 0; i < cll; i++)
                    cl = cl << 8 | data[offset++];

                if (ends - offset < cl)
                    return (cl - (ends - offset), null);

                var metaData = DC.Clip(data, offset + 1, data[offset]);
                offset += data[offset] + (uint)1;


                return (1 + cl + cll, new ParsedTDU()
                {
                    Identifier = (TDUIdentifier)(h & 0xC7),
                    Data = data,
                    Offset = offset,
                    Class = cls,
                    ContentLength = cl,
                    TotalLength = 1 + cl + cll,
                    Index = (byte)h & 0x7,
                    Metadata = metaData,
                });
            }
            else
            {
                ulong cll = (ulong)(h >> 3) & 0x7;

                if (ends - offset < cll)
                    return (cll - (ends - offset), null);

                ulong cl = 0;

                for (uint i = 0; i < cll; i++)
                    cl = cl << 8 | data[offset++];

                if (ends - offset < cl)
                    return (cl - (ends - offset), null);


                return (1 + cl + cll,
                    new ParsedTDU()
                    {
                        Identifier = (TDUIdentifier)(h & 0xC7),
                        Data = data,
                        Offset = offset,
                        Class = cls,
                        ContentLength = cl,
                        TotalLength = 1 + cl + cll,
                        Index = (byte)h & 0x7
                    });
            }
        }

    }
}
