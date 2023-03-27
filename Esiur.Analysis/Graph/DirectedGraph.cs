using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Esiur.Analysis.Graph
{
    public class DirectedGraph <T>: IGraph where T:struct
    {
        public List<Node<T>> Nodes { get;} = new List<Node<T>>();

        public List<Edge<T>> Edges { get; }= new List<Edge<T>>();

        public void Link(Node<T> source, Node<T> destination , T weight, string label)
        {
            var edge = new Edge<T>() { SourceNode = source, DestinationNode = destination, Weight = weight, Label = label };
            Edges.Add(edge);
            source.Destinations.Add(edge);
            destination.Sources.Add(edge);
            
        }

        public Node<T> AddNode(T value, string label, int x, int y)
        {
            var n = new Node<T>() { Value = value, Label = label, X = x, Y = y };
            Nodes.Add(n);
            return n;
        }

        public Matrix<T> TransitionMatrix { get; private set; }

        public Matrix<T> CurrentStep { get; private set; }

        public Edge<T>[,] EdgesMatrix { get; private set; }

        public void Build()
        {
            // create matrix
            var m = new T[Nodes.Count, Nodes.Count];
            var e = new Edge<T>[Nodes.Count, Nodes.Count];

            for(var i = 0; i < Nodes.Count; i++)
            {
                for(var j = 0; j < Nodes.Count; j++)
                {
                    var link = Edges.FirstOrDefault(x => x.SourceNode == Nodes[i] && x.DestinationNode == Nodes[j]);
                    if (link == null)
                        m[i, j] = default(T);
                    else
                    {
                        m[i, j] = link.Weight;
                        e[i, j] = link;
                    }
                }
            }

            TransitionMatrix = new Matrix<T>(m);
            CurrentStep = TransitionMatrix;
            EdgesMatrix = e;
        }

        public void Step()
        {
            CurrentStep *= CurrentStep;
            // update weights
            for(var i = 0; i < CurrentStep.Rows; i++)
                for(var j = 0; j < CurrentStep.Columns; j++)
                {
                    if (EdgesMatrix[i, j] != null)
                        EdgesMatrix[i, j].Weight = CurrentStep[i, j];
                }

        }
    }
}
