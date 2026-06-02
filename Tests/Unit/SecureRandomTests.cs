using System;
using System.Linq;
using Esiur.Misc;

namespace Esiur.Tests.Unit;

/// <summary>
/// Guards the fix that moved Global.GenerateBytes / GenerateCode off System.Random onto a
/// cryptographic RNG. These are sanity/entropy checks, not statistical proofs: their job is
/// to fail loudly if someone reintroduces a predictable or constant generator.
/// </summary>
public class SecureRandomTests
{
    [Fact]
    public void GenerateBytes_Returns_Requested_Length()
    {
        Assert.Equal(20, Global.GenerateBytes(20).Length);
        Assert.Equal(0, Global.GenerateBytes(0).Length);
    }

    [Fact]
    public void GenerateBytes_Are_Not_Repeated()
    {
        var a = Global.GenerateBytes(32);
        var b = Global.GenerateBytes(32);
        Assert.False(a.SequenceEqual(b), "Two nonces must not be identical.");
    }

    [Fact]
    public void GenerateBytes_Are_Not_Constant()
    {
        var bytes = Global.GenerateBytes(64);
        Assert.True(bytes.Distinct().Count() > 1, "Output must not be a single repeated byte.");
    }

    [Fact]
    public void GenerateBytes_Have_Broad_Distribution()
    {
        // Across 4 KiB of output almost every byte value should appear at least once.
        var bytes = Global.GenerateBytes(4096);
        Assert.True(bytes.Distinct().Count() > 200,
            "A cryptographic RNG should produce a wide spread of byte values.");
    }

    [Fact]
    public void GenerateCode_Honours_Length_And_Alphabet()
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var code = Global.GenerateCode(24);
        Assert.Equal(24, code.Length);
        Assert.All(code, c => Assert.Contains(c, alphabet));
    }

    [Fact]
    public void GenerateCode_Is_Not_Repeated()
    {
        Assert.NotEqual(Global.GenerateCode(24), Global.GenerateCode(24));
    }
}
