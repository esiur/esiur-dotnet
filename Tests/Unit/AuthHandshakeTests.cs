using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Esiur.Security.Authority;
using Esiur.Security.Authority.Providers;

namespace Esiur.Tests.Unit;

/// <summary>
/// Drives a pair of <see cref="PasswordAuthenticationHandler"/> instances (initiator and
/// responder) through the SHA3 challenge/response handshake and asserts both the happy
/// path (matching session keys) and the security hardening (constant-time challenge
/// checks, nonce validation, fail-closed behaviour on malformed peer input).
/// </summary>
public class AuthHandshakeTests
{
    // ---- test credential store -------------------------------------------------------

    class TestAccount
    {
        public string Identity;
        public byte[] RawPassword;
        public byte[] Salt;
        public byte[] Hash; // SHA3-256(RawPassword || Salt), exactly what the verifier stores
    }

    static readonly byte[] FixedSalt = { 9, 8, 7, 6, 5, 4, 3, 2, 1, 0, 1, 2, 3, 4, 5, 6 };

    static TestAccount MakeAccount(string identity, string password)
    {
        var raw = Encoding.UTF8.GetBytes(password);
        var hash = PasswordAuthenticationHandler.ComputeSha3(raw.Concat(FixedSalt).ToArray());
        return new TestAccount { Identity = identity, RawPassword = raw, Salt = FixedSalt, Hash = hash };
    }

    class StubProvider : PasswordAuthenticationProvider
    {
        readonly Dictionary<string, TestAccount> _accounts;
        readonly string _self;

        public StubProvider(string self, params TestAccount[] accounts)
        {
            _self = self;
            _accounts = accounts.ToDictionary(a => a.Identity, a => a);
        }

        public override IdentityPassword GetSelfIdentityAndCredential(string domain, string hostname)
            => new IdentityPassword(_self, _accounts[_self].RawPassword);

        public override byte[] GetSelfCredential(string identity, string domain, string hostname)
            => _accounts.TryGetValue(identity, out var a) ? a.RawPassword : null;

        public override PasswordHash GetHostedAccountCredential(string identity, string domain)
            => _accounts.TryGetValue(identity, out var a)
                ? new PasswordHash(a.Hash, a.Salt)
                : new PasswordHash(null, null);
    }

    // ---- helpers ---------------------------------------------------------------------

    static object[] DataOf(AuthenticationResult result)
        => ((List<object>)result.AuthenticationData)?.ToArray();

    static byte[] PrivateNonce(PasswordAuthenticationHandler handler)
        => (byte[])typeof(PasswordAuthenticationHandler)
            .GetField("_localNonce", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(handler);

    static (PasswordAuthenticationHandler init, PasswordAuthenticationHandler resp) NewPair(string identity = "alice")
    {
        var account = MakeAccount(identity, "correct horse battery staple");
        var initiator = new PasswordAuthenticationHandler(
            AuthenticationMode.InitializerIdentity, AuthenticationDirection.Initiator,
            identity, null, "host", "domain", new StubProvider(identity, account));
        var responder = new PasswordAuthenticationHandler(
            AuthenticationMode.InitializerIdentity, AuthenticationDirection.Responder,
            null, null, "host", "domain", new StubProvider(identity, account));
        return (initiator, responder);
    }

    // ---- happy path ------------------------------------------------------------------

    [Fact]
    public void ProviderAndHandler_UseVersionedPasswordProtocolName()
    {
        var provider = new PasswordAuthenticationProvider();
        var handler = provider.CreateAuthenticationHandler(new AuthenticationContext
        {
            Direction = AuthenticationDirection.Initiator,
            Mode = AuthenticationMode.InitializerIdentity,
            Domain = "domain",
            HostName = "host",
        });

        Assert.Equal("password-sha3-v1", provider.DefaultName);
        Assert.Equal(provider.DefaultName, handler.Protocol);
    }

    [Fact]
    public void InitializerIdentity_Handshake_Derives_Matching_SessionKeys()
    {
        var (init, resp) = NewPair();

        var r1 = init.Process(null);                 // -> [initNonce, initIdentity]
        Assert.Equal(AuthenticationRuling.InProgress, r1.Ruling);

        var r2 = resp.Process(DataOf(r1));           // -> [respNonce, respSalt, respChallenge]
        Assert.Equal(AuthenticationRuling.InProgress, r2.Ruling);

        var r3 = init.Process(DataOf(r2));           // -> [initChallenge], Succeeded
        Assert.Equal(AuthenticationRuling.Succeeded, r3.Ruling);

        var r4 = resp.Process(DataOf(r3));           // Succeeded
        Assert.Equal(AuthenticationRuling.Succeeded, r4.Ruling);

        Assert.NotNull(r3.SessionKey);
        Assert.Equal(64, r3.SessionKey.Length);      // 512-bit derived key
        Assert.Equal(r3.SessionKey, r4.SessionKey);  // both ends agree
    }

    [Fact]
    public void Wrong_Password_Fails()
    {
        var account = MakeAccount("alice", "the real password");
        var init = new PasswordAuthenticationHandler(
            AuthenticationMode.InitializerIdentity, AuthenticationDirection.Initiator,
            "alice", null, "host", "domain",
            new StubProvider("alice", MakeAccount("alice", "a different password")));
        var resp = new PasswordAuthenticationHandler(
            AuthenticationMode.InitializerIdentity, AuthenticationDirection.Responder,
            null, null, "host", "domain", new StubProvider("alice", account));

        var r1 = init.Process(null);
        var r2 = resp.Process(DataOf(r1));
        // Initiator validates the responder's challenge against its (wrong) password and bails.
        var r3 = init.Process(DataOf(r2));
        Assert.Equal(AuthenticationRuling.Failed, r3.Ruling);
    }

    // ---- security properties ---------------------------------------------------------

    [Fact]
    public void Tampered_Challenge_Fails()
    {
        var (init, resp) = NewPair();

        var r1 = init.Process(null);
        var r2 = resp.Process(DataOf(r1));
        var r3 = init.Process(DataOf(r2));           // [initChallenge]

        var tampered = DataOf(r3);
        ((byte[])tampered[0])[0] ^= 0xFF;            // flip a bit in the challenge

        var r4 = resp.Process(tampered);
        Assert.Equal(AuthenticationRuling.Failed, r4.Ruling);
    }

    [Fact]
    public void Reflected_Nonce_Is_Rejected()
    {
        // Replay defence: feeding the responder its own nonce must be rejected.
        var (init, resp) = NewPair();
        var respNonce = PrivateNonce(resp);

        var forged = new object[] { respNonce, "alice" };
        var result = resp.Process(forged);
        Assert.Equal(AuthenticationRuling.Failed, result.Ruling);
    }

    [Fact]
    public void Short_Nonce_Is_Rejected()
    {
        var (_, resp) = NewPair();
        var result = resp.Process(new object[] { new byte[5], "alice" });
        Assert.Equal(AuthenticationRuling.Failed, result.Ruling);
    }

    [Theory]
    [InlineData(0)]   // empty
    [InlineData(1)]   // too few elements
    public void Truncated_Input_Fails_Without_Throwing(int count)
    {
        var (_, resp) = NewPair();
        var result = resp.Process(Enumerable.Range(0, count).Select(_ => (object)new byte[20]).ToArray());
        Assert.Equal(AuthenticationRuling.Failed, result.Ruling);
    }

    [Fact]
    public void Null_Input_Fails_Without_Throwing()
    {
        var (_, resp) = NewPair();
        var result = resp.Process(null);
        Assert.Equal(AuthenticationRuling.Failed, result.Ruling);
    }

    [Fact]
    public void WrongType_Material_Fails_Closed()
    {
        // A peer sending a string where a nonce (byte[]) is expected must fail, not throw.
        var (_, resp) = NewPair();
        var result = resp.Process(new object[] { "not-a-nonce", "alice" });
        Assert.Equal(AuthenticationRuling.Failed, result.Ruling);
    }
}
