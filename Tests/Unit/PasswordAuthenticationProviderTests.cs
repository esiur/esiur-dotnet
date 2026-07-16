using Esiur.Security.Authority.Providers;

namespace Esiur.Tests.Unit;

public sealed class PasswordAuthenticationProviderTests
{
    [Fact]
    public void ProtocolName_HasNoLegacyPublicAlias()
    {
        var publicProtocolConstants = typeof(PasswordAuthenticationProvider)
            .GetFields(System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => Assert.IsType<string>(field.GetRawConstantValue()))
            .ToArray();

        Assert.Equal(new[] { PasswordAuthenticationProvider.ProtocolName },
            publicProtocolConstants);
    }

    [Fact]
    public void CreateCredential_GeneratesFreshSaltAndExpectedProtocolHash()
    {
        var password = new byte[] { 1, 2, 3, 4, 5 };

        var first = PasswordAuthenticationProvider.CreateCredential(password);
        var second = PasswordAuthenticationProvider.CreateCredential(password);

        Assert.Equal(32, first.Salt.Length);
        Assert.Equal(32, first.Hash.Length);
        Assert.Equal(
            PasswordAuthenticationHandler.ComputeSha3(
                password.Concat(first.Salt).ToArray()),
            first.Hash);
        Assert.False(first.Salt.SequenceEqual(second.Salt));
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, password);
    }

    [Fact]
    public void CreateCredential_RejectsMissingOrEmptyPassword()
    {
        Assert.Throws<ArgumentNullException>(() =>
            PasswordAuthenticationProvider.CreateCredential(null!));
        Assert.Throws<ArgumentException>(() =>
            PasswordAuthenticationProvider.CreateCredential(Array.Empty<byte>()));
    }
}
