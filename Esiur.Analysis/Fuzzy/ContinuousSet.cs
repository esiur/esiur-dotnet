using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Esiur.Analysis.Fuzzy
{
    public class ContinuousSet : INumericalSet<double>
    {
        public MembershipFunction Function { get; set; }

        public double AlphaCut { get; set; } = double.MinValue;



        public INumericalSet<double> Intersection(INumericalSet<double> with)
        {
            return new OperationSet(Operation.Intersection, this, with);
        }

        public INumericalSet<double> Union(INumericalSet<double> with)
        {
            return new OperationSet(Operation.Union, this, with);
        }

        public ContinuousSet(MembershipFunction function)
        {
            this.Function = function;
        }

        public double this[double input]
        {
            get
            {

                var results = Function(input);

                return results < AlphaCut ? 0 : results;
            }
        }

    
    }
}
