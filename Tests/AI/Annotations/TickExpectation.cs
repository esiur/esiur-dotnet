using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Tests.Annotations
{
    public sealed class TickExpectation
    {
        public int Tick { get; set; }
        public HashSet<string?> AllowedFunctions { get; set; } = new();
        public string Note { get; set; } = "";
    }
}
