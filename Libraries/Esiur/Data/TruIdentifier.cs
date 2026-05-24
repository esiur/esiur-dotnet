using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Data
{
    public enum TruIdentifier
    {
        Void = 0x0,
        Dynamic = 0x1,
        Bool = 0x2,
        UInt8,
        Int8,
        Char,
        UInt16,
        Int16,
        UInt32,
        Int32,
        Float32,
        UInt64,
        Int64,
        Float64,
        DateTime,
        UInt128,
        Int128,
        Decimal,
        String,
        RawData,
        Resource,
        Record,
        List,
        Map,
        //Enum = 0x44,
        //TypedResource = 0x45, // Followed by UUID
        //TypedRecord = 0x46, // Followed by UUID
        LocalType8 = 0x40,
        RemoteType8 = 0x41,
        LocalType16 = 0x42,
        RemoteType16 = 0x43,
        LocalType32 = 0x44,
        RemoteType32 = 0x45,
        LocalType64 = 0x46,
        RemoteType64 = 0x47,

        TypedList = 0x48, // Followed by element type

        Tuple2 = 0x50, // Followed by element type
        TypedMap = 0x51, // Followed by key type and value type
        Tuple3 = 0x58,
        Tuple4 = 0x60,
        Tuple5 = 0x68,
        Tuple6 = 0x70,
        Tuple7 = 0x78
    }

}
