using Esiur.Resource;

/// <summary>
/// Server-side telemetry resource the sweep orchestrator attaches to (sys/control). The server
/// updates these once per second; the updates propagate to the orchestrator so it can attribute
/// fan-out saturation to the server (CPU across all cores, can exceed 100%) and confirm the
/// connected-subscriber count. Exported as fields so the runtime generates change-notifying
/// properties (CpuPercent, ConnectedClients).
/// </summary>
[Resource]
public partial class ControlResource : Resource
{
    [Export] public double cpuPercent;
    [Export] public int connectedClients;
}
