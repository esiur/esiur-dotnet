using Esiur.Data;
using Esiur.Engine;
using Esiur.Net;
using Esiur.Resource;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Security.Authority
{
    public class Session
    {
        Authentication Authentication { get; }
        Source Source { get; }
        string Id { get; }
        DateTime Creation { get; }
        DateTime Modification { get; }
        //KeyList<string, object> Variables { get; }
        //IStore Store { get; }
    }
}
