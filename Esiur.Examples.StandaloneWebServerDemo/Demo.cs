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
        [Export] int age { get; set; }
        [Export] string name { get; set;}
        [Export] 
    }
}
