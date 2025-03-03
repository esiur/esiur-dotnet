using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Esiur.Data
{
    //[StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct UUID
    {
        //4e7db2d8-a785-1b99-1854-4b4018bc5677
        //byte a1;
        //byte a2;
        //byte a3;
        //byte a4;
        //byte b1;
        //byte b2;
        //byte c1;
        //byte c2;
        //byte d1;
        //byte d2;
        //byte e1;
        //byte e2;
        //byte e3;
        //byte e4;
        //byte e5;
        //byte e6;

        public byte[] Data { get; private set; }


        public UUID(byte[] data, uint offset)
        {
            if (offset + 16 < data.Length)
                throw new Exception("UUID data size must be at least 16 bytes");

            Data = DC.Clip(data, offset, 16);

            //a1 = data[offset++];
            //a2 = data[offset++];
            //a3 = data[offset++];
            //a4 = data[offset++];
            //b1 = data[offset++];
            //b2 = data[offset++];
            //c1 = data[offset++];
            //c2 = data[offset++];
            //d1 = data[offset++];
            //d2 = data[offset++];
            //e1 = data[offset++];
            //e2 = data[offset++];
            //e3 = data[offset++];
            //e4 = data[offset++];
            //e5 = data[offset++];
            //e6 = data[offset++];
        }

        public UUID(byte[] data) {

            if (data.Length != 16)
                throw new Exception("UUID data size must be 16 bytes");

            Data = data;
            //a1 = data[0];
            //a2 = data[1];
            //a3 = data[2];
            //a4 = data[3];
            //b1 = data[4];
            //b2 = data[5];
            //c1 = data[6];
            //c2 = data[7];
            //d1 = data[8];
            //d2 = data[9];
            //e1 = data[10];
            //e2 = data[11];
            //e3 = data[12];
            //e4 = data[13];
            //e5 = data[14];
            //e6 = data[15];
        }
        public override string ToString()
        {

            return $"{DC.ToHex(Data, 0, 4, null)}-{DC.ToHex(Data, 4, 2, null)}-{DC.ToHex(Data, 6, 2, null)}-{DC.ToHex(Data, 8, 2, null)}-{DC.ToHex(Data, 10, 6, null)}";

            //return $"{a1.ToString("x2")}{a2.ToString("x2")}{a3.ToString("x2")}{a4.ToString("x2")}-{b1.ToString("x2")}{b2.ToString("x2")}-{c1.ToString("x2")}{c2.ToString("x2")}-{d1.ToString("x2")}{d2.ToString("x2")}-{e1.ToString("x2")}{e2.ToString("x2")}{e3.ToString("x2")}{e4.ToString("x2")}{e5.ToString("x2")}{e6.ToString("x2")}";
        }

        public static bool operator == (UUID a, UUID b)
        {
            return a.Data.SequenceEqual(b.Data);

            //return a.a1 == b.a1
            //        && a.a2 == b.a2
            //        && a.a3 == b.a3
            //        && a.a4 == b.a4
            //        && a.b1 == b.b1
            //        && a.b2 == b.b2
            //        && a.c1 == b.c1
            //        && a.c2 == b.c2
            //        && a.d1 == b.d1
            //        && a.d2 == b.d2
            //        && a.e1 == b.e1
            //        && a.e2 == b.e2
            //        && a.e3 == b.e3
            //        && a.e4 == b.e4
            //        && a.e5 == b.e5
            //        && a.e6 == b.e6;
        }

        public static bool operator !=(UUID a, UUID b)
        {
            return !(a == b);
        }

    }
}
