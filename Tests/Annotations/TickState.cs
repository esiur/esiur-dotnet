using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Tests.Annotations
{
    public sealed class TickState
    {
        public int Load { get; set; }
        public int ErrorCount { get; set; }
        public bool Enabled { get; set; }
    }

}
