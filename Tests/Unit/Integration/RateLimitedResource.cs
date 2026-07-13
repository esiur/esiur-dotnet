using Esiur.Resource;
using System.Threading;

namespace Esiur.Tests.Unit.Integration;

[Resource]
public partial class RateLimitedResource
{
    int _callCount;
    int _value;

    [Export]
    [RateControl("standard-call")]
    public int Call() => Interlocked.Increment(ref _callCount);

    [Export]
    [RateControl("standard-set")]
    public int Value
    {
        get => _value;
        set => _value = value;
    }
}
