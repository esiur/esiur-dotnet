using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Esiur.Analysis.Fuzzy
{
    public enum Operation
    {
        Intersection,
        Union
    }

    public class OperationSet : INumericalSet<double>
    {

        public Operation Operation { get; set; }

        public INumericalSet<double>[] Sets { get; internal set; }

        public double AlphaCut { get; set; }

        public double this[double index]
        {
            get
            {
                double x = 0;
                if (Operation == Operation.Union)
                    x = Sets.Max(x => x[index]);
                else if (Operation == Operation.Intersection)
                    x = Sets.Min(x => x[index]);

                // Alpha might be changed for this instance
                return x < AlphaCut ? 0 : x;
            }
        }

        public OperationSet(Operation operation, params INumericalSet<double>[] sets)
        {
            Sets = sets;

            if (operation == Operation.Intersection)
                AlphaCut = sets.Min(x => x.AlphaCut);
            else if (Operation == Operation.Union)
                AlphaCut = sets.Max(x => x.AlphaCut);

            Operation = operation;
        }

        public INumericalSet<double> Intersection(INumericalSet<double> with)
        {
            return new OperationSet(Operation.Intersection, new INumericalSet<double>[] { this, with });
        }

        public INumericalSet<double> Union(INumericalSet<double> with)
        {
            return new OperationSet(Operation.Union, new INumericalSet<double>[] { this, with });
        }
    }
}
