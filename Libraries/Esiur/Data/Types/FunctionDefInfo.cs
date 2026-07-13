#nullable enable
using System.Collections.Generic;

namespace Esiur.Data.Types;

public class FunctionDefInfo : MemberDefInfo
{
    [Index((int)FunctionDefField.Arguments)]
    public List<ArgumentDefInfo>? Arguments { get; set; }

    [Index((int)FunctionDefField.ReturnType)]
    public Tru ReturnType { get; set; } = null!;

    [Index((int)FunctionDefField.StreamMode)]
    public StreamMode StreamMode { get; set; }
}
