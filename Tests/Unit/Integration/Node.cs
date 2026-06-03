using Esiur.Resource;

namespace Esiur.Tests.Unit.Integration;

/// <summary>
/// A minimal distributed resource used to build arbitrary reference topologies (cycles,
/// cross-references, diamonds) for the deadlock integration tests. <see cref="Links"/> holds
/// references to other nodes; when a node is fetched the client transitively fetches its links,
/// which is what exercises EpConnection.FetchResource cycle handling.
/// </summary>
[Resource]
public partial class Node
{
    [Export] public int Id { get; set; }

    [Export] public Node[]? Links { get; set; }
}
