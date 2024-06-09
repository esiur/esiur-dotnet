using Esiur.Data;
using Esiur.Resource;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Examples.StandaloneWebServerDemo
{
    [Export]
    [Resource]
    public partial class Demo
    {
        [Export] int color { get; set; }
        [Export] string label { get; set;}
        [Export] public ResourceEventHandler<int> Cleared;
        [Export] public ResourceEventHandler<Point> Drawn;

        [Export] List<List<int>> points;

        [Export] public void Draw(int x, int y, int color)
        {

            Drawn?.Invoke(new Point() { X = x, Y = y, Color = color });
        }

        public Demo()
        {
            points = new List<List<int>>();
            for (var x = 0; x < 400; x++)
            {
                var p = new List<int>();
                points.Add(p);
                for (var y = 0; y < 300; y++)
                    p.Add(0);
            }
        }
    }

    [Export]
    public class Point : IRecord
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Color { get; set; }
    }
}
