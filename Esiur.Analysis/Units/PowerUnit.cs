using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Esiur.Analysis.Units
{
    public enum PowerUnitKind
    {
        Watt,
        Decibel
    }

    public struct PowerUnit : IComparable
    {
        public double Value;


        public PowerUnit(double value, PowerUnitKind kind = PowerUnitKind.Watt)
        {
            if (kind == PowerUnitKind.Watt)
                Value = value;
            else if (kind == PowerUnitKind.Decibel)
                Value = FromDb(value);
            else
                Value = 0;
        }

        public static implicit operator PowerUnit(double d) => new PowerUnit(d, PowerUnitKind.Watt);
        public static implicit operator double(PowerUnit d) => d.Value;

        //public static explicit operator PowerUnit(double d) => new PowerUnit(d, PowerUnitKind.Watt);
        //public static explicit operator double(PowerUnit d) => d.Value;

        //public static PowerUnit operator +(PowerUnit a) => a;
        //public static PowerUnit operator -(PowerUnit a) => new PowerUnit(-a.num, a.den);

        public static PowerUnit operator +(PowerUnit a, PowerUnit b)
            => new PowerUnit(a.Value + b.Value);

        public static PowerUnit operator -(PowerUnit a, PowerUnit b)
            => new PowerUnit(a.Value - b.Value);

        public static PowerUnit operator *(PowerUnit a, PowerUnit b)
            => new PowerUnit(a.Value * b.Value);

        public static PowerUnit operator /(PowerUnit a, PowerUnit b)
        {
            if (b.Value == 0)
            {
                throw new DivideByZeroException();
            }
            return new PowerUnit(a.Value / b.Value);
        }

      
        public override string ToString()
        {
            if (Value < 1e-6)
                return (Value * 1e9).ToString("F") + "nW";
            else if (Value < 1e-3)
                return (Value * 1e6).ToString("F") + "µW";
            else if (Value < 1)
                return (Value * 1e3).ToString("F") + "mW";
            else
                return Value.ToString("F") + "W";
        }

        public double ToDb() => 10 * Math.Log(10, Value);
        public static double FromDb(double value) => Math.Pow(10, value / 10);

        public int CompareTo(object obj)
        {

            if (obj is PowerUnit p)
                return Value.CompareTo(p.Value);
            else
                return Value.CompareTo(obj);
        }
    }
}
