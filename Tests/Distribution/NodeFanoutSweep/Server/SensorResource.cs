using Esiur.Resource;

/// <summary>
/// Observable sensor resource. Setting <c>value</c> raises a property-change notification that the
/// Esiur runtime propagates to every attached subscriber — the fan-out path under measurement.
/// (The generated property is named <c>Value</c>; subscribers filter on that name.)
/// </summary>
[Resource]
public partial class SensorResource : Resource
{
    public int SensorId { get; set; }

    [Export] public double value;
}
