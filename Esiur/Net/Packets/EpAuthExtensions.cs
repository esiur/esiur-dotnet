using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Net.Packets
{
    public static class EpAuthExtensions
    {
        public static EpAuthPacketIAuthFormat GetIAuthFormat(this object value)
        {
            if (value is string)
                return EpAuthPacketIAuthFormat.Text;
            else if (value is int || value is uint
                || value is byte || value is sbyte
                || value is short || value is ushort
                || value is long || value is ulong)
                return EpAuthPacketIAuthFormat.Number;
            else if (value.GetType().IsArray)
                return EpAuthPacketIAuthFormat.Choice;

            throw new Exception("Unknown IAuth format");
        }
    }
}
