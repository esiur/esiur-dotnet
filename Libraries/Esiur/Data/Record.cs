using Esiur.Data.Types;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Data;

public class Record : KeyList<string, object>, IRecord
{
    public TypeDef TypeDef { get; private set; }

    public Record(TypeDef typeDef)
    {
        TypeDef = typeDef;
    }

    public override string ToString()
    {
        return $"Record<{TypeDef.Name}> {{{string.Join(", ", this.Select(x=>x.Key + ": " + x.Value))}}}";
    }
}
