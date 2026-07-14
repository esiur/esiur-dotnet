using Esiur.Resource;

namespace Esiur.Tests.Unit.Integration;

[Resource]
public partial class EncryptedEchoResource
{
    [Export]
    public int Echo(int value) => value;
}
