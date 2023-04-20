/*
 
Copyright (c) 2023 Ahmed Kh. Zamil

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/

using Esiur.Analysis.Graph;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Esiur.Analysis.Test
{
    public partial class FGraph : Form
    {
        DirectedGraph<decimal> graph;
        int step = 0;

        public FGraph()
        {
            graph = new DirectedGraph<decimal>();

            //var n1 = graph.AddNode(1, "1", 100, 300);
            //var n2 = graph.AddNode(2, "2", 500, 300);
            //graph.Link(n1, n2, (decimal)0.5, "1->2");
            //graph.Link(n1, n1, (decimal)0.5, "1->1");

            //graph.Link(n2, n1, (decimal)0.2, "2->1");
            //graph.Link(n2, n2, (decimal)0.8, "2->2");

            var n0 = graph.AddNode(1, "0", 100, 300);
            var n1 = graph.AddNode(1, "1", 300, 300);
            var n2 = graph.AddNode(2, "2", 500, 300);
            var n3 = graph.AddNode(3, "3", 700, 300);
            var n4 = graph.AddNode(4, "4", 900, 300);
            var n5 = graph.AddNode(0, "5", 1100, 300);

            graph.Link(n0, n0, (decimal)0.2, "00");
            graph.Link(n0, n2, (decimal)0.2, "02");
            graph.Link(n0, n3, (decimal)0.2, "03");
            graph.Link(n0, n4, (decimal)0.2, "04");
            graph.Link(n0, n5, (decimal)0.2, "05");

            graph.Link(n1, n0, (decimal)0.1, "10");
            graph.Link(n1, n1, (decimal)0.1, "11");
            graph.Link(n1, n2, (decimal)0.2, "12");
            graph.Link(n1, n3, (decimal)0.2, "13");
            graph.Link(n1, n4, (decimal)0.2, "14");
            graph.Link(n1, n5, (decimal)0.2, "15");


            graph.Link(n2, n1, (decimal)0.2, "21");
            graph.Link(n2, n2, (decimal)0.2, "22");
            graph.Link(n2, n3, (decimal)0.2, "23");
            graph.Link(n2, n4, (decimal)0.2, "24");
            graph.Link(n2, n5, (decimal)0.2, "25");

            graph.Link(n3, n2, (decimal)0.2, "32");
            graph.Link(n3, n3, (decimal)0.2, "33");
            graph.Link(n3, n4, (decimal)0.3, "34");
            graph.Link(n3, n5, (decimal)0.3, "35");


            graph.Link(n4, n3, (decimal)0.4, "43");
            graph.Link(n4, n4, (decimal)0.3, "44");
            graph.Link(n4, n5, (decimal)0.3, "45");

            graph.Link(n5, n4, (decimal)0.5, "54");
            graph.Link(n5, n5, (decimal)0.5, "55");


            graph.Build();

            for(var i = 0; i < graph.Nodes.Count; i++)
            {
                decimal sum = 0;
                for(var j = 0; j < graph.Nodes.Count; j++)
                {
                    sum += graph.TransitionMatrix[i, j];
                }

                if (sum != 1)
                    throw new Exception("Sum must be 1");
            }

            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            graph.Step();
            step++;
            pbDraw.Refresh();

         }

        private void pbDraw_Paint(object sender, PaintEventArgs e)
        {
            
            var pen = new Pen(Brushes.Red, 4);
            var g = e.Graphics;

            g.FillRectangle(Brushes.White, 0, 0, pbDraw.Width, pbDraw.Height);

            // update label
            foreach (var edge in graph.Edges)
            {
                DrawArcBetweenTwoPoints(g, pen, new PointF(edge.SourceNode.X, edge.SourceNode.Y),
                    new PointF(edge.DestinationNode.X, edge.DestinationNode.Y), edge.Label + " " + Math.Round( edge.Weight, 4));
            }

            foreach (var node in graph.Nodes)
            {
                g.FillEllipse(Brushes.LightGreen, node.X - 30, node.Y - 30, 60, 60);
                g.DrawString(node.Label, new Font("Arial", 26), Brushes.Blue, node.X - 20, node.Y - 20);
            }


            g.DrawString("Step " + step, new Font("Arial", 26), Brushes.Orange, new PointF(20, pbDraw.Height - 50));

            g.Flush();
        }


        public void DrawArcBetweenTwoPoints(Graphics g, Pen pen, PointF a, PointF b, string label)
        {


            if (a.X == b.X && a.Y == b.Y)
            {
                var c = new PointF(a.X, a.Y - 60);

                // draw 
                g.DrawString(label, new Font("Arial", 12), Brushes.Black, new PointF(c.X, c.Y - 25));

                g.DrawCurve(pen, new PointF[] { a, new PointF(a.X - 30, a.Y - 30), c, new PointF(a.X + 30, a.Y - 30), a });
            }
            else  
            {
                float dis = (float)Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

                if (b.X > a.X)
                {
                    var c = new PointF(a.X + ((b.X - a.X) / 2), a.Y - 0.25f * dis);
                    g.DrawCurve(pen, new PointF[] { a, c, b });
                    g.DrawString(label, new Font("Arial", 12), Brushes.Black, new PointF( c.X - 30, c.Y - 25));
                    g.DrawLines(pen, new PointF[] { new PointF(c.X - 6, c.Y - 6), c, new PointF(c.X - 6, c.Y + 6) });
                }
                else
                {
                    var c = new PointF(b.X + ((a.X - b.X) / 2), b.Y + 0.25f * dis);
                    g.DrawCurve(pen, new PointF[] { b, c, a });
                    g.DrawString(label, new Font("Arial", 12), Brushes.Black, new PointF(c.X - 30, c.Y + 5));

                    g.DrawLines(pen, new PointF[] { new PointF(c.X + 6, c.Y + 6), c, new PointF(c.X + 6, c.Y - 6) });

                }


            }
        }

    }
}
