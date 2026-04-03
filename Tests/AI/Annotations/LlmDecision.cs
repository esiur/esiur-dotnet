using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Tests.Annotations
{
    public sealed class LlmDecision
    {
        public string? Function { get; set; }
        public string? Reason { get; set; }
    }
}
