using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Data;

public enum StructureComparisonResult : byte
{
    Null,
    Structure,
    StructureSameKeys,
    StructureSameTypes,
    Same
}
