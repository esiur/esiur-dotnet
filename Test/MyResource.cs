using Esiur.Core;
using Esiur.Resource;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Test
{
    [Resource]
    [Annotation("A", "B", "C", "D")]
    public partial class MyResource
    {
        [Export][Annotation("Comment")] string description;
        [Export] int categoryId;

        [Export] public string Hello() => "Hi";

        [Export] public string HelloParent() => "Hi from Parent";

        [Export]
        [Annotation("This function computes the standard deviation of a list")]
        public double StDev(double[] values)
        {
            double avg = values.Average();
            return Math.Sqrt(values.Average(v => Math.Pow(v - avg, 2)));
        }

    }
}
