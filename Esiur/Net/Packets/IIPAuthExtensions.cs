using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Net.Packets
{
    public static class IIPAuthExtensions
    {
        public static IIPAuthPacketIAuthFormat GetIAuthFormat(this object value)
        {
            if (value is string)
                return IIPAuthPacketIAuthFormat.Text;
            else if (value is int || value is uint
                || value is byte || value is sbyte
                || value is short || value is ushort
                || value is long || value is ulong)
                return IIPAuthPacketIAuthFormat.Number;
            else if (value.GetType().IsArray)
                return IIPAuthPacketIAuthFormat.Choice;

            throw new Exception("Unknown IAuth format");
        }
    }
}
