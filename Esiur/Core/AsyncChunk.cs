using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Core
{
    // This interface is used to provide return type for templates and support chunk callbacks using IAsyncEnumerable feature of C# 8
    public interface IAsyncChunk<T> : IAsyncEnumerable<object>
    {

    }
}
