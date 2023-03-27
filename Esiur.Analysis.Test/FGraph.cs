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

            var n1 = graph.AddNode(1, "1", 80, 120);
            var n2 = graph.AddNode(2, "2", 300, 120);

            graph.Link(n1, n2, (decimal)0.5, "1->2");
            graph.Link(n1, n1, (decimal)0.5, "1->1");

            graph.Link(n2, n1, (decimal)0.2, "2->1");
            graph.Link(n2, n2, (decimal)0.8, "2->2");

            graph.Build();

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
                g.DrawString(node.Label, new Font("Arial", 26), Brushes.Blue, node.X - 15, node.Y - 20);
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
                g.DrawString(label, new Font("Arial", 14), Brushes.Black, new PointF(c.X, c.Y - 25));

                g.DrawCurve(pen, new PointF[] { a, new PointF(a.X - 30, a.Y - 30), c, new PointF(a.X + 30, a.Y - 30), a });
            }
            else  
            {
                if (b.X > a.X)
                {
                    var c = new PointF(a.X + ((b.X - a.X) / 2), a.Y - 20);
                    g.DrawCurve(pen, new PointF[] { a, c, b });
                    g.DrawString(label, new Font("Arial", 14), Brushes.Black, new PointF( c.X - 30, c.Y - 25));

                }
                else
                {
                    var c = new PointF(b.X + ((a.X - b.X) / 2), b.Y + 20);
                    g.DrawCurve(pen, new PointF[] { b, c, a });
                    g.DrawString(label, new Font("Arial", 14), Brushes.Black, new PointF(c.X - 30, c.Y + 5));
                }

              
            }
        }

    }
}
