using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Data
{
    public readonly struct ParseResult<T>: IParseResult<T>
    {
        public T Value { get; }
        public uint Size { get; }

        public ParseResult(T value, uint size)
        {
            Value = value;
            Size = size;
        }
    }
}