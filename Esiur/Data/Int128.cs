using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Data
{
    public struct Int128
    {
        public Int128( ulong lsb, ulong msb)
        {
            this.MSB = msb;
            this.LSB = lsb;
        }

        public ulong MSB { get; set; }
        public ulong LSB { get; set; }
    }
}
