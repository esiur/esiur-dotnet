using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Data
{
    public interface IParseResult<out T>
    {
        public T Value { get; }
        public uint Size { get; }
    }
}
