using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Analysis.Graph
{
    public class Edge<T>
    {
        public T SourceNode { get; set; }
        public T DestinationNode { get; set; }
    }
}
