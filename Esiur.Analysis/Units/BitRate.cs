using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Esiur.Analysis.Units
{
    public enum BitRateKind
    {
        Bits,
        Octets,
        Bytes
    }

    public struct BitRate : IComparable
    {
        public double Value;


        public BitRate(double value, BitRateKind kind = BitRateKind.Bits)
        {
            if (kind == BitRateKind.Bytes)
                Value = value * 8;
            else if (kind == BitRateKind.Octets)
                Value = value * 7;
            else
                Value = value;
        }

        public static implicit operator BitRate(double d) => new BitRate(d);
        public static implicit operator double(BitRate d) => d.Value;

        //public static explicit operator PowerUnit(double d) => new PowerUnit(d, PowerUnitKind.Watt);
        //public static explicit operator double(PowerUnit d) => d.Value;

        //public static PowerUnit operator +(PowerUnit a) => a;
        //public static PowerUnit operator -(PowerUnit a) => new PowerUnit(-a.num, a.den);

        public static BitRate operator +(BitRate a, BitRate b)
            => new BitRate(a.Value + b.Value);

        public static BitRate operator -(BitRate a, BitRate b)
            => new BitRate(a.Value - b.Value);

        public static BitRate operator *(BitRate a, BitRate b)
            => new BitRate(a.Value * b.Value);

        public static BitRate operator /(BitRate a, BitRate b)
        {
            if (b.Value == 0)
            {
                throw new DivideByZeroException();
            }
            return new BitRate(a.Value / b.Value);
        }


        public override string ToString()
        {
            if (Value >= 1e12)
                return (Value / 1e12).ToString("F") + " tbps";
            else if (Value >= 1e9)
                return (Value / 1e9).ToString("F") + " gbps";
            else if (Value >= 1e6)
                return (Value / 1e6).ToString("F") + " mbps";
            else if (Value >= 1e3)
                return (Value * 1e3).ToString("F") + " kbps";
            else
                return Value.ToString("F") + " bps";
        }


        public int CompareTo(object obj)
        {
            if (obj is BitRate p)
                return Value.CompareTo(p.Value);
            else
                return Value.CompareTo(obj);
        }
    }
}
