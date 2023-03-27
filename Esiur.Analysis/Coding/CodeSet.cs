using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Analysis.Coding
{

    public class CodeSet<T> where T : System.Enum
    {

        public T[] Elements { get; private set; }

        public int ElementsCount { get; private set; }

        public CodeSet()
        {
            var values = System.Enum.GetValues(typeof(T));
            Elements = new T[values.Length];
            ElementsCount = values.Length;
            values.CopyTo(Elements, 0);
        }
    }

    //public interface IBaseValue<T>
    //{
    //    public T Value { get; set; }
    //    public T[] Allowed { get; set; }
    //}


    public enum Base2: byte
    {
        Zero,
        One
    }

    public enum Base3 : byte
    {
        Zero,
        One,
        Two
    }

    //public struct BinaryValue : IBaseValue<Base2>
    //{
    //    public Base2 Value { get; set; }
    //}

    //public struct TernaryValue : IBaseValue<Base3>
    //{
    //    public Base3 Value { get; set; }
    //}
}
