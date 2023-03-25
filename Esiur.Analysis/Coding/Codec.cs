using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Analysis.Coding
{
    public interface ICodec
    {

        public  byte[] Encode(byte[] source, uint offset, uint length);

        public byte[] Decode(byte[] source, uint offset, uint length);

    }
}
