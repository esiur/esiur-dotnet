using System;
using System.Collections.Generic;
using System.Text;

namespace Esyur.Security.Integrity
{

    public class NMEA0183
    {
        public static byte Compute(string data)
        {
            return Compute(data, 0, (uint)data.Length);
        }

        public static byte Compute(string data, uint offset, uint length)
        {
            byte rt = 0;
            var ends = offset + length;
            for (int i = (int)offset; i < ends; i++)
                rt ^= (byte)data[i];

            return rt;
        }
    }

}
