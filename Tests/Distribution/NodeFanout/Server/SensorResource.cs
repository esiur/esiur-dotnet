using Esiur.Resource;

/// <summary>
/// A simple observable sensor resource.
/// Property changes are automatically propagated to all attached peers.
/// </summary>
[Resource]
public class SensorResource : Resource
{
    public int SensorId { get; set; }

    private double _value;

    [ResourceProperty]
    public double Value
    {
        get => _value;
        set
        {
            _value = value;
            PropertyModified("Value");  // notifies Esiur runtime to propagate
        }
    }
}
