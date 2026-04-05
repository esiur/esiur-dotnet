using Esiur.Resource;

/// <summary>
/// Shared observable sensor resource used across all scalability tests.
/// Property changes via Value setter are automatically propagated
/// to all attached remote peers by the Esiur runtime.
/// </summary>
[Resource]
public partial class SensorResource : Resource
{
    public int SensorId { get; set; }

    [Export]
    public double value;
}
