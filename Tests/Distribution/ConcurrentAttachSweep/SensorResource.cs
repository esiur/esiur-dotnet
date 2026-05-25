using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Tests.ConcurrentAttachSweep;

using Esiur.Resource;

[Resource]
public partial class SensorResource : Resource
{
    public int SensorId { get; set; }

    [Export]
    public double value;
}