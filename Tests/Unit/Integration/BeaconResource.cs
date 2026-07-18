using Esiur.Resource;

namespace Esiur.Tests.Unit.Integration;

[Resource]
public partial class BeaconResource
{
    [Export] int pings;

    // No [AutoDelivery]: subscribable by default — requires an explicit
    // Subscribe request before occurrences flow to a given connection.
    [Export] public event ResourceEventHandler<string>? Ping;

    // Opts out via [AutoDelivery]: flows to every attached connection
    // unconditionally.
    [Export]
    [AutoDelivery]
    public event ResourceEventHandler<string>? Tick;

    public void Fire(string name, string value)
    {
        // Write through the generated public property (Pings), not the
        // private backing field — only the generated setter emits the
        // PropertyModified notification.
        Pings = pings + 1;
        if (name == "ping") Ping?.Invoke(value);
        else if (name == "tick") Tick?.Invoke(value);
    }
}
