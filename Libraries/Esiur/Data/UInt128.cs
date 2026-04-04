using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Data
{
    public struct UInt128
    {
        public UInt128(ulong lsb, ulong msb)
        {
            this.MSB = msb;
            this.LSB = lsb;
        }

        public ulong MSB { get;set; }
        public ulong LSB { get;set; }
    }
}
