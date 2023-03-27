using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Esiur.Analysis.Coding
{

    public struct CodeWord<T>
    {
        public T[] Word;
        int hashCode;

        public override bool Equals(object obj)
        {
            if (obj is CodeWord<T>)
                return Word.SequenceEqual(((CodeWord<T>)obj).Word);
            return false;
        }

        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }

  

        public static CodeWord<Base2> FromByte(byte b)
        {
            var word = new Base2[8];
            for(var i = 0; i < 8; i++)
            {
                word[i] = (b & (0x1 << i)) > 0 ? Base2.One : Base2.Zero; 
            }

            return new CodeWord<Base2>() { Word = word };
        }

        public override string ToString()
        {
            return String.Join(" ", Word);
        }
    }

    public class Symbol<T>
    {
        public double Probability { get; set; }
        public CodeWord<T> Word { get; set; }
    }
}
