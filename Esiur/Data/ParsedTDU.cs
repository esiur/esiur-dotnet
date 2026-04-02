using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Data
{
    public struct ParsedTdu
    {
        public TduIdentifier Identifier;
        public int Index;
        public TduClass Class;
        public uint Offset;
        public ulong ContentLength;
        public byte[] Data;
        public byte Exponent;
        public ulong TotalLength;
        public byte[] Metadata;
        public uint Ends;

        public static ParsedTdu Parse(byte[] data, uint offset, uint ends)
        {

            var h = data[offset++];

            var cls = (TduClass)(h >> 6);

            if (cls == TduClass.Fixed)
            {
                var exp = (h & 0x38) >> 3;

                if (exp == 0)
                    return new ParsedTdu()
                    {
                        Identifier = (TduIdentifier)h,
                        Data = data,
                        Offset = offset,
                        Class = cls,
                        Exponent = (byte)exp,
                        Index = (byte)h & 0x7,
                        ContentLength = 0,
                        TotalLength = 1,
                        Ends = ends
                    };

                ulong cl = (ulong)(1 << (exp - 1));

                if (ends - offset < cl)
                    return new ParsedTdu()
                    {
                        Class = TduClass.Invalid,
                        TotalLength = (cl - (ends - offset))
                    };

                //offset += (uint)cl;

                return new ParsedTdu()
                {
                    Identifier = (TduIdentifier)h,
                    Data = data,
                    Offset = offset,
                    Class = cls,
                    ContentLength = cl,
                    TotalLength = 1 + cl,
                    Exponent = (byte)exp,
                    Index = (byte)h & 0x7,
                    Ends = ends
                };
            }
            else if (cls == TduClass.Typed)
            {
                ulong cll = (ulong)(h >> 3) & 0x7;

                if (ends - offset < cll)
                    return new ParsedTdu()
                    {
                        Class = TduClass.Invalid,
                        TotalLength = (cll - (ends - offset))
                    };

                ulong cl = 0;

                for (uint i = 0; i < cll; i++)
                    cl = cl << 8 | data[offset++];

                if (ends - offset < cl)
                    return new ParsedTdu()
                    {
                        TotalLength = (cl - (ends - offset)),
                        Class = TduClass.Invalid,
                    };

                var metaData = DC.Clip(data, offset + 1, data[offset]);
                offset += data[offset] + (uint)1;


                return new ParsedTdu()
                {
                    Identifier = (TduIdentifier)(h & 0xC7),
                    Data = data,
                    Offset = offset,
                    Class = cls,
                    ContentLength = cl - 1 - (uint)metaData.Length,
                    TotalLength = 1 + cl + cll,
                    Index = (byte)h & 0x7,
                    Metadata = metaData,
                    Ends = ends
                };
            }
            else
            {
                ulong cll = (ulong)(h >> 3) & 0x7;

                if (ends - offset < cll)
                    return new ParsedTdu()
                    {
                        Class = TduClass.Invalid,
                        TotalLength = (cll - (ends - offset))
                    };

                ulong cl = 0;

                for (uint i = 0; i < cll; i++)
                    cl = cl << 8 | data[offset++];

                if (ends - offset < cl)
                    return new ParsedTdu()
                    {
                        Class = TduClass.Invalid,
                        TotalLength = (cl - (ends - offset))
                    };


                return
                    new ParsedTdu()
                    {
                        Identifier = (TduIdentifier)(h & 0xC7),
                        Data = data,
                        Offset = offset,
                        Class = cls,
                        ContentLength = cl,
                        TotalLength = 1 + cl + cll,
                        Index = (byte)h & 0x7,
                        Ends = ends
                    };
            }
        }

    }
}
