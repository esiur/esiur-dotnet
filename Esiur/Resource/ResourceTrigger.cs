using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Resource
{
    public enum ResourceTrigger : int
    {
        Loaded = 0,
        Initialize,
        Terminate,
        Configure,
        SystemInitialized,
        SystemTerminated,
        SystemReload,
    }
}
