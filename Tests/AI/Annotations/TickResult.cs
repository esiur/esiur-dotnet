using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Tests.Annotations
{
    public sealed class TickResult
    {
        public string Model { get; set; } = "";
        public int Tick { get; set; }

        public int LoadBefore { get; set; }
        public int ErrorCountBefore { get; set; }
        public bool EnabledBefore { get; set; }

        public string RawResponse { get; set; } = "";
        public string? PredictedFunction { get; set; }
        public string? Reason { get; set; }

        public bool Parsed { get; set; }
        public bool Allowed { get; set; }
        public bool Invoked { get; set; }
        public bool Correct { get; set; }

        public double LatencyMs { get; set; }

        public int LoadAfter { get; set; }
        public int ErrorCountAfter { get; set; }
        public bool EnabledAfter { get; set; }

        public string ExpectedText { get; set; } = "";


        public bool Repaired { get; set; }
        public int JsonObjectCount { get; set; }
        public string? FirstFunction { get; set; }
        public string? FinalFunction { get; set; }

         public string? FirstPredictedFunction { get; set; }
    }

}
