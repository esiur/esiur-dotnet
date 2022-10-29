using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Esiur.Analysis.Units
{
    public enum FrequencyKind
    {
        Hertz,
        Kilohertz,
        Megahertz,
        Gigahertz,
        Terahertz
    }

    public struct Frequency : IComparable
    {
        public double Value;


        public Frequency(double value, FrequencyKind kind = FrequencyKind.Hertz)
        {
            if (kind == FrequencyKind.Kilohertz)
                Value = value * 1000;
            else if (kind == FrequencyKind.Megahertz)
                Value = value * 1000000;
            else if (kind == FrequencyKind.Gigahertz)
                Value = value * 1000000000;
            else if (kind == FrequencyKind.Terahertz)
                Value = value * 1000000000000;
            else
                Value = value;
        }

        public static implicit operator Frequency(double d) => new Frequency(d);
        public static implicit operator double(Frequency d) => d.Value;

        //public static explicit operator PowerUnit(double d) => new PowerUnit(d, PowerUnitKind.Watt);
        //public static explicit operator double(PowerUnit d) => d.Value;

        //public static PowerUnit operator +(PowerUnit a) => a;
        //public static PowerUnit operator -(PowerUnit a) => new PowerUnit(-a.num, a.den);

        public static Frequency operator +(Frequency a, Frequency b)
            => new Frequency(a.Value + b.Value);

        public static Frequency operator -(Frequency a, Frequency b)
            => new Frequency(a.Value - b.Value);

        public static Frequency operator *(Frequency a, Frequency b)
            => new Frequency(a.Value * b.Value);

        public static Frequency operator /(Frequency a, Frequency b)
        {
            if (b.Value == 0)
            {
                throw new DivideByZeroException();
            }
            return new Frequency(a.Value / b.Value);
        }


        public override string ToString()
        {
            if (Value >= 1e12)
                return (Value / 1e12).ToString("F") + " Terahertz";
            else if (Value >= 1e9)
                return (Value / 1e9).ToString("F") + " Gigahertz";
            else if (Value >= 1e6)
                return (Value / 1e6).ToString("F") + " Megahertz";
            else if (Value >= 1e3)
                return (Value * 1e3).ToString("F") + " Kilohertz";
            else
                return Value.ToString("F") + " Hertz";
        }


        public int CompareTo(object obj)
        {
            if (obj is Frequency p)
                return Value.CompareTo(p.Value);
            else
                return Value.CompareTo(obj);
        }
    }
}
