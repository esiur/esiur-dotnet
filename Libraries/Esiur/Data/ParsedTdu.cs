using Esiur.Core;
using Esiur.Protocol;
using Esiur.Resource;
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
        public uint PayloadOffset;
        public ulong PayloadLength;
        public byte[] Data;
        public byte Exponent;
        public ulong TotalLength;
        public Tru Metadata;
        public uint Ends;

        public static AsyncReply<ParsedTdu> ParseAsync(byte[] data, EpConnection connection)
        {
            return ParseAsync(data, (uint)0, (uint)data.Length, connection);
        }

        public static async AsyncReply<ParsedTdu> ParseAsync(byte[] data, uint offset, uint ends, EpConnection connection)
        {
            // @TODO: add protection against memory allocation attacks by checking the length of the data before parsing it.

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
                    PayloadOffset = offset,
                    Class = cls,
                    PayloadLength = cl,
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

                //var metaData = DC.Clip(data, offset + 1, data[offset]);
                //offset += data[offset] + (uint)1;

                var metaDataTru = await Tru.ParseAsync(data, offset, connection, null);
                offset += metaDataTru.Size;

                return new ParsedTdu()
                {
                    Identifier = (TduIdentifier)(h & 0xC7),
                    Data = data,
                    PayloadOffset = offset,
                    Class = cls,
                    PayloadLength = cl - metaDataTru.Size,
                    TotalLength = 1 + cl + cll,
                    Index = (byte)h & 0x7,
                    Metadata = metaDataTru.Value,
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
                        PayloadOffset = offset,
                        Class = cls,
                        PayloadLength = cl,
                        TotalLength = 1 + cl + cll,
                        Index = (byte)h & 0x7,
                        Ends = ends
                    };
            }
        }

        public static object Parse(byte[] data, uint offset, uint ends, EpConnection connection)
        {
            // @TODO: add protection against memory allocation attacks by checking the length of the data before parsing it.

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
                    PayloadOffset = offset,
                    Class = cls,
                    PayloadLength = cl,
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

                //var metaData = DC.Clip(data, offset + 1, data[offset]);
                //offset += data[offset] + (uint)1;
                var rt = new AsyncReply<ParsedTdu>();

                Tru.ParseAsync(data, offset, connection, null).Then(metaDataTru =>
                {
                    offset += metaDataTru.Size;

                    rt.Trigger(new ParsedTdu()
                    {
                        Identifier = (TduIdentifier)(h & 0xC7),
                        Data = data,
                        PayloadOffset = offset,
                        Class = cls,
                        PayloadLength = cl - metaDataTru.Size,
                        TotalLength = 1 + cl + cll,
                        Index = (byte)h & 0x7,
                        Metadata = metaDataTru.Value,
                        Ends = ends
                    });
                });

                return rt;
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
                        PayloadOffset = offset,
                        Class = cls,
                        PayloadLength = cl,
                        TotalLength = 1 + cl + cll,
                        Index = (byte)h & 0x7,
                        Ends = ends
                    };
            }
        }

        public static byte[] ClipTduData(byte[] data, uint offset, uint ends)
        {
            var oOffset = (int)offset;

            var h = data[offset++];

            var cls = (TduClass)(h >> 6);

            if (cls == TduClass.Fixed)
            {
                var exp = (h & 0x38) >> 3;

                if (exp == 0)
                {
                    return new byte[] { h };
                }

                ulong cl = (ulong)(1 << (exp - 1));

                if (ends - offset < cl)
                {
                    return null; // failded
                }

                var rt = new byte[1 + cl];
                Buffer.BlockCopy(data, oOffset, rt, 0, (int)cl);
                return rt;

            }
            else
            {
                ulong cll = (ulong)(h >> 3) & 0x7;

                if (ends - offset < cll)
                    return null;

                ulong cl = 0;

                for (uint i = 0; i < cll; i++)
                    cl = cl << 8 | data[offset++];

                if (ends - offset < cl)
                    return null;

                var rt = new byte[1 + cll + cl];

                Buffer.BlockCopy(data, oOffset, rt, 0, rt.Length);

                return rt;
            }

        }

        public static ParsedTdu ParseSync(byte[] data, uint offset, uint ends, Warehouse warehouse)
        {
            // @TODO: add protection against memory allocation attacks by checking the length of the data before parsing it.

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
                    PayloadOffset = offset,
                    Class = cls,
                    PayloadLength = cl,
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

                //var metaData = DC.Clip(data, offset + 1, data[offset]);
                //offset += data[offset] + (uint)1;

                var metaDataTru = Tru.Parse(data, offset, warehouse);
                offset += metaDataTru.Size;

                return new ParsedTdu()
                {
                    Identifier = (TduIdentifier)(h & 0xC7),
                    Data = data,
                    PayloadOffset = offset,
                    Class = cls,
                    PayloadLength = cl - metaDataTru.Size,
                    TotalLength = 1 + cl + cll,
                    Index = (byte)h & 0x7,
                    Metadata = metaDataTru.Value,
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
