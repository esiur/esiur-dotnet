using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Data;

public enum RecordComparisonResult : byte
{
    Null,
    Record,
    RecordSameType,
    Same
}
