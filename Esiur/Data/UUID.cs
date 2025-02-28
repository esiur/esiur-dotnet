using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Esiur.Data
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct UUID
    {
        //4e7db2d8-a785-1b99-1854-4b4018bc5677
        byte a1;
        byte a2;
        byte a3;
        byte a4;
        byte b1;
        byte b2;
        byte c1;
        byte c2;
        byte d1;
        byte d2;
        byte e1;
        byte e2;
        byte e3;
        byte e4;
        byte e5;
        byte e6;


        public UUID(byte[] data)
        {
            if (data.Length < 16)
                throw new Exception("UUID data size must be at least 16 bytes");

            for(var i = 0; i < 16; i++)
                Data[i] = data[i];
        }

        public override string ToString()
        {
            return $"{a1.ToString("x2")}{a2.ToString("x2")}{a3.ToString("x2")}{a4.ToString("x2")}-{b1.ToString("x2")}{b2.ToString("x2")}-{c1.ToString("x2")}{c2.ToString("x2")}-{d1.ToString("x2")}{d2.ToString("x2")}-{e1.ToString("x2")}{e2.ToString("x2")}{e3.ToString("x2")}{e4.ToString("x2")}{e5.ToString("x2")}{e6.ToString("x2")}";
        }

        public static bool operator == (UUID a, UUID b)
        {
            return a.a1 == b.a1
                    && a.a2 == b.a2
                    && a.a3 == b.a3
                    && a.a4 == b.a4
                    && a.b1 == b.b1
                    && a.b2 == b.b2
                    && a.c1 == b.c1
                    && a.c2 == b.c2
                    && a.d1 == b.d1
                    && a.d2 == b.d2
                    && a.e1 == b.e1
                    && a.e2 == b.e2
                    && a.e3 == b.e3
                    && a.e4 == b.e4
                    && a.e5 == b.e5
                    && a.e6 == b.e6;
        }

        public static bool operator !=(UUID a, UUID b)
        {
            return !(a == b);
        }

    }
}
