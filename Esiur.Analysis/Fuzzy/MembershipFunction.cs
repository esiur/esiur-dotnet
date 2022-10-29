using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Analysis.Fuzzy
{
    public delegate double MembershipFunction(double value);

    public static class MembershipFunctions
    {
        public static MembershipFunction Triangular(double start, double peak, double end)
        {
            return new MembershipFunction(x =>
            {
                if (x < start || x > end) return 0;
                if (x < peak) return (x - start) / (peak - start);
                if (x > peak) return (end - x) / (end - peak);
                return 1; // x = peak
            });
        }

        public static MembershipFunction Descending(double peak, double end)
        {
            return new MembershipFunction(x =>
            {
                if (x <= peak) return 1;
                if (x > peak && x < end) return (end - x) / (end - peak);
                return 0;
            });
        }

        public static MembershipFunction Ascending(double start, double peak)
        {
            return new MembershipFunction(x =>
            {
                if (x >= peak) return 1;
                if (x < peak && x > start) return (x - start) / (peak - start);
                return 0;
            });
        }
    }

}
