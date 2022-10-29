using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Analysis.Fuzzy
{
    internal interface IVectorSet: IEnumerable<double>
    {
        public double this[double index]
        {
            get;
        }
    }
}
