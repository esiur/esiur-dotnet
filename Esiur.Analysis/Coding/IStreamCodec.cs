using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Analysis.Coding
{
    public interface IStreamCodec<T>
    {

        //public  byte[] Encode(byte[] source, uint offset, uint length);

        //public byte[] Decode(byte[] source, uint offset, uint length);

        public T[] Encode(CodeWord<T>[] source, uint offset, uint length);
        public CodeWord<T>[] Decode(T[] source, uint offset, uint length);

    }
}
