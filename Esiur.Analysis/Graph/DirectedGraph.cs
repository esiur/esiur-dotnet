using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Analysis.Graph
{
    public class DirectedGraph <T>: IGraph
    {
        public Node<T>[] Nodes { get; set; }
        public Edge<T>[] Edges { get; set; }



    }
}
