using Esiur.Resource;

namespace Esiur.AspNetCore.Example;

[Resource]
public partial class MyResource
{
    private int count;

    [Export]
    public string Hello(string name) => $"Hello, {name}!";

    [Export]
    public int Increment() => Interlocked.Increment(ref count);

    [Export]
    public int CurrentCount() => Volatile.Read(ref count);
}
