using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Analysis.Graph
{
    public class Node<T>
    {
        public T Value { get; set; }

        public int X { get; set; }
        public int Y { get; set; }
    }
}
