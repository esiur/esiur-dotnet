using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Data
{
    public enum TDUIdentifier
    {
        Null = 0x0,
        False = 0x1,
        True = 0x2,
        NotModified = 0x3,
        UInt8 = 0x8,
        Int8 = 0x9,
        Char8 = 0xA,
        LocalResource8 = 0xB,
        RemoteResource8 = 0xC,
        LocalProcedure8 = 0xD,
        RemoteProcedure8 = 0xE,
        UInt16 = 0x10,
        Int16 = 0x11,
        Char16 = 0x12,
        LocalResource16 = 0x13,
        RemoteResource16 = 0x14,
        LocalProcedure16 = 0x15,
        RemoteProcedure16 = 0x16,
        UInt32 = 0x18,
        Int32 = 0x19,
        Float32 = 0x1A,
        LocalResource32 = 0x1B,
        RemoteResource32 = 0x1C,
        LocalProcedure32 = 0x1D,
        RemoteProcedure32 = 0x1E,
        UInt64 = 0x20,
        Int64 = 0x21,
        Float64 = 0x22,
        DateTime = 0x23,
        UInt128 = 0x28,
        Int128 = 0x29,
        Decimal128 = 0x2A,
        UUID = 0x2B,

        RawData = 0x40,
        String = 0x41,
        List = 0x42,
        ResourceList = 0x43,
        RecordList = 0x44,
        Map = 0x45,
        MapList = 0x46,
        ResourceLink = 0x47,

        Record = 0x80,
        TypedList = 0x81,
        TypedMap = 0x82,
        TypedTuple = 0x83,
        TypedEnum = 0x84,
        TypedConstant = 0x85,

        TypeContinuation = 0xC0,


    }
}
