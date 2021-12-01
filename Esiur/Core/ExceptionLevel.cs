using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Core;

public enum ExceptionLevel
{
    Code = 0x1,
    Message = 0x2,
    Source = 0x4,
    Trace = 0x8
}
