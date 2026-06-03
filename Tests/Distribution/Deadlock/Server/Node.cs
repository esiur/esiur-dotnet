using Esiur.Resource;
using Esiur.Tests.Deadlock.Server;

/// <summary>
/// Resource used to build reference topologies (cycles, cross-references) for the distributed
/// deadlock test. <see cref="Links"/> holds references to other nodes; fetching a node transitively
/// fetches its links, which is what exercises EpConnection.FetchResource cycle handling.
/// Property indices are stable: Id = 0, Links = 1.
/// </summary>
[Resource]
public partial class Node
{
    [Export] public int Id { get; set; }

    [Export] public Node[]? Links { get; set; }

    [Export] public Resource1[] Resources1 { get; set; }
    [Export] public Resource2[] Resources2 { get; set; }
}
