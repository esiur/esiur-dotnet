using Esiur.Core;
using Esiur.Protocol;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Data
{
    public struct PlainTdu
    {
        public TduIdentifier Identifier;
        public int Index;
        public TduClass Class;
        public uint TduOffset;
        public uint PayloadOffset;
        public ulong PayloadLength;
        public byte[] Data;
        public byte Exponent;
        public ulong TotalLength;
        public uint Ends;



        public static PlainTdu Parse(
            byte[] data,
            uint offset,
            uint ends,
            ulong maximumPayloadLength = ulong.MaxValue)
        {
            var oOffset = offset;

            // @TODO: add protection against memory allocation attacks by checking the length of the data before parsing it.

            var h = data[offset++];

            var cls = (TduClass)(h >> 6);

            if (cls == TduClass.Fixed)
            {
                var exp = (h & 0x38) >> 3;

                if (exp == 0)
                    return new PlainTdu()
                    {
                        Identifier = (TduIdentifier)h,
                        Data = data,
                        TduOffset = oOffset,
                        PayloadOffset = offset,
                        Class = cls,
                        Exponent = (byte)exp,
                        Index = (byte)h & 0x7,
                        PayloadLength = 0,
                        TotalLength = 1,
                        Ends = ends
                    };

                ulong cl = (ulong)(1 << (exp - 1));

                if (ends - offset < cl)
                    return new PlainTdu()
                    {
                        Class = TduClass.Invalid,
                        TotalLength = (cl - (ends - offset))
                    };

                //offset += (uint)cl;

                return new PlainTdu()
                {
                    Identifier = (TduIdentifier)h,
                    Data = data,
                    TduOffset= oOffset,
                    PayloadOffset = offset,
                    Class = cls,
                    PayloadLength = cl,
                    TotalLength = 1 + cl,
                    Exponent = (byte)exp,
                    Index = (byte)h & 0x7,
                    Ends = ends
                };
            }
            else if ((h & 0xC7) == 0x80) // else if (cls == TduClass.Typed)
            {
                ulong cll = (ulong)(h >> 3) & 0x7;

                if (ends - offset < cll)
                    return new PlainTdu()
                    {
                        Class = TduClass.Invalid,
                        TotalLength = (cll - (ends - offset))
                    };

                ulong cl = 0;

                for (uint i = 0; i < cll; i++)
                    cl = cl << 8 | data[offset++];

                if (cl > maximumPayloadLength)
                    throw new ParserLimitException(
                        $"Declared packet payload of {cl} bytes exceeds the {maximumPayloadLength}-byte limit.");

                if (ends - offset < cl)
                    return new PlainTdu()
                    {
                        TotalLength = (cl - (ends - offset)),
                        Class = TduClass.Invalid,
                    };


                return new PlainTdu()
                {
                    Identifier = (TduIdentifier)(h & 0xC7),
                    Data = data,
                    TduOffset = oOffset,
                    PayloadOffset = offset,
                    Class = cls,
                    PayloadLength = cl,
                    TotalLength = 1 + cl + cll,
                    Index = (byte)h & 0x7,
                    Ends = ends
                };
            }
            else
            {
                ulong cll = (ulong)(h >> 3) & 0x7;

                if (ends - offset < cll)
                    return new PlainTdu()
                    {
                        Class = TduClass.Invalid,
                        TotalLength = (cll - (ends - offset))
                    };

                ulong cl = 0;

                for (uint i = 0; i < cll; i++)
                    cl = cl << 8 | data[offset++];

                if (cl > maximumPayloadLength)
                    throw new ParserLimitException(
                        $"Declared packet payload of {cl} bytes exceeds the {maximumPayloadLength}-byte limit.");

                if (ends - offset < cl)
                    return new PlainTdu()
                    {
                        Class = TduClass.Invalid,
                        TotalLength = (cl - (ends - offset))
                    };


                return
                    new PlainTdu()
                    {
                        Identifier = (TduIdentifier)(h & 0xC7),
                        Data = data,
                        TduOffset = oOffset,
                        PayloadOffset = offset,
                        Class = cls,
                        PayloadLength = cl,
                        TotalLength = 1 + cl + cll,
                        Index = (byte)h & 0x7,
                        Ends = ends
                    };
            }
        }
    }
}
