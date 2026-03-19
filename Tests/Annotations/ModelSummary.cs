using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Tests.Annotations
{
    public sealed class ModelSummary
    {
        public string Model { get; set; } = "";
        public int TotalTicks { get; set; }

        public double ParseRate { get; set; }
        public double AllowedRate { get; set; }
        public double CorrectRate { get; set; }

        public double MeanLatencyMs { get; set; }
        public double P95LatencyMs { get; set; }

        public double RepairRate { get; set; }
    }
}
