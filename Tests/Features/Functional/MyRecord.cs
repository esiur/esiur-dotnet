using Esiur.Data;
using Esiur.Resource;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Tests.Functional;

    [Export]
    public class MyRecord:IRecord
    {
        public string Name { get; set; }
        public int Id { get; set; }

        public double Score { get; set; }

    }
