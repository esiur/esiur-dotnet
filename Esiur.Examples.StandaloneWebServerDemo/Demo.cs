using Esiur.Data;
using Esiur.Resource;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Examples.StandaloneWebServerDemo
{
    [Resource]
    public partial class Demo
    {
        [Export] int color;
        [Export] string label = "Hello World";
        [Export] public event ResourceEventHandler<int>? Cleared;
        [Export] public event ResourceEventHandler<Point>? Drawn;

        [Export] List<List<byte>> points;

        [Export] public void Draw(int x, int y, byte color)
        {
            try
            {
                points[x][y] = color;
                Drawn?.Invoke(new Point() { X = x, Y = y, Color = color });
            }
            catch  
            {

            }
        }
        
        [Export] public void Clear()
        {
            foreach (var pa in points)
                for (var i = 0; i < pa.Count; i++)
                    pa[i] = 0;
                    
            Cleared?.Invoke(0);
        }

        public Demo()
        {
            points = new List<List<byte>>();
            for (var x = 0; x < 100; x++)
            {
                var p = new List<byte>();
                points.Add(p);
                for (var y = 0; y < 80; y++)
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
