using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Analysis.Graph
{
    public class Edge<T>
    {
        public Node<T> SourceNode { get; set; }
        public Node<T> DestinationNode { get; set; }

        public T Weight { get; set; }

        public string Label { get; set; }
    }
}
