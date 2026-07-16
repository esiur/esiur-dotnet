using System;
using System.Collections.Generic;
using System.IO;

namespace Esiur.Security.Authority.Providers.Ppap;

/// <summary>
/// Per-connection PPAP ML-KEM state. A handler instance must not be reused.
/// </summary>
public sealed partial class PpapAuthenticationHandler : IAuthenticationHandler,
    IAuthenticationKeyRotationHandler, IDisposable
{
    enum HandshakeState
    {
        InitiatorStart,
        InitiatorAwaitServerHello,
        InitiatorAwaitResponderProof,
        ResponderAwaitClientHello,
        ResponderAwaitInitiatorProof,
        ResponderAwaitInitiatorFinished,
        Complete,
        Failed,
        Disposed,
    }

    readonly object _sync = new();
    readonly PpapAuthenticationProvider _provider;
    readonly PpapLocalIdentity _localIdentity;
    readonly AuthenticationDirection _direction;
    readonly AuthenticationMode _mode;
    readonly string _domain;
    readonly string _expectedInitiatorIdentity;
    readonly string _expectedResponderIdentity;
    readonly bool _authenticateInitiator;
    readonly bool _authenticateResponder;
    readonly List<byte[]> _transcript = new();

    HandshakeState _state;
    string _initiatorIdentity;
    string _responderIdentity;
    PpapRegistrationRecord _initiatorRegistration;
    PpapRegistrationRecord _responderRegistration;
    byte[] _initiatorMask;
    byte[] _responderMask;
    byte[] _ephemeralPrivateKey;
    byte[] _ephemeralSecret;
    byte[] _initiatorIdentitySecret;
    byte[] _responderIdentitySecret;
    byte[] _sessionKey;
    byte[] _initiatorFinishedKey;
    byte[] _responderFinishedKey;
    byte[] _transcriptHash;
    byte[] _authenticationContext;

    public IAuthenticationProvider Provider => _provider;
    public string Protocol => PpapProtocol.Name;

    internal PpapAuthenticationHandler(PpapAuthenticationProvider provider,
        AuthenticationContext context)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _localIdentity = provider.ResolveLocalIdentity(context);
        _direction = context.Direction;
        _mode = context.Mode;
        _domain = PpapCryptography.NormalizeDomain(context.Domain);
        _expectedInitiatorIdentity = NormalizeOptionalIdentity(context.InitiatorIdentity);
        _expectedResponderIdentity = NormalizeOptionalIdentity(context.ResponderIdentity);

        if (_direction != AuthenticationDirection.Initiator
            && _direction != AuthenticationDirection.Responder)
            throw new ArgumentOutOfRangeException(nameof(context.Direction));
        if (_mode != AuthenticationMode.InitializerIdentity
            && _mode != AuthenticationMode.ResponderIdentity
            && _mode != AuthenticationMode.DualIdentity)
            throw new ArgumentOutOfRangeException(nameof(context.Mode));

        _authenticateInitiator = _mode == AuthenticationMode.InitializerIdentity
            || _mode == AuthenticationMode.DualIdentity;
        _authenticateResponder = _mode == AuthenticationMode.ResponderIdentity
            || _mode == AuthenticationMode.DualIdentity;

        var localIsAuthenticated = _direction == AuthenticationDirection.Initiator
            ? _authenticateInitiator
            : _authenticateResponder;
        if (localIsAuthenticated && _localIdentity == null)
            throw new InvalidOperationException(
                "This PPAP mode requires a provider-configured local identity.");

        if (_localIdentity != null)
        {
            var expectedLocal = _direction == AuthenticationDirection.Initiator
                ? _expectedInitiatorIdentity
                : _expectedResponderIdentity;
            if (expectedLocal != null && !string.Equals(expectedLocal,
                _localIdentity.Identity, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "The connection identity does not match the PPAP provider identity.");
        }

        _state = _direction == AuthenticationDirection.Initiator
            ? HandshakeState.InitiatorStart
            : HandshakeState.ResponderAwaitClientHello;
    }

    public AuthenticationResult Process(object authData)
    {
        lock (_sync)
        {
            if (_state == HandshakeState.Disposed)
                return Failed();

            try
            {
                if (_direction == AuthenticationDirection.Initiator)
                    return ProcessInitiator(authData);
                return ProcessResponder(authData);
            }
            catch
            {
                _state = HandshakeState.Failed;
                ClearHandshakeSecrets(clearSessionKey: true);
                ClearRotationSecrets();
                return Failed();
            }
        }
    }

    AuthenticationResult ProcessInitiator(object authData)
    {
        if (_state == HandshakeState.InitiatorStart)
        {
            if (authData != null)
                throw new InvalidDataException("Unexpected initial PPAP data.");

            if (_authenticateInitiator)
                _initiatorIdentity = _localIdentity.Identity;

            PpapCryptography.GenerateKeyPair(out _ephemeralPrivateKey,
                out var ephemeralPublicKey);
            _initiatorMask = PpapCryptography.RandomBytes(PpapProtocol.IdentityMaskLength);
            byte[] message = null;
            try
            {
                message = PpapWire.EncodeClientHello(_mode, _domain,
                    ephemeralPublicKey, _initiatorMask);
                AddTranscript(message);
                _state = HandshakeState.InitiatorAwaitServerHello;
                return InProgress(message);
            }
            finally
            {
                PpapCryptography.Clear(ephemeralPublicKey);
            }
        }

        if (_state == HandshakeState.InitiatorAwaitServerHello)
        {
            var raw = RequireBytes(authData);
            var hello = PpapWire.DecodeServerHello(raw, _authenticateResponder);
            AddTranscript(raw);

            _ephemeralSecret = PpapCryptography.Decapsulate(
                _ephemeralPrivateKey, hello.EphemeralCiphertext);
            PpapCryptography.Clear(_ephemeralPrivateKey);
            _ephemeralPrivateKey = null;
            _responderMask = hello.ResponderMask;

            byte[] maskedInitiator = null;
            byte[] responderCiphertext = null;
            PpapRegistrationDescriptor responderDescriptor = null;

            if (_authenticateInitiator)
                maskedInitiator = PpapCryptography.MaskIdentity(_domain,
                    _initiatorIdentity, _responderMask, _ephemeralSecret);

            if (_authenticateResponder)
            {
                _responderRegistration = _provider.Registrations.ResolveMasked(
                    _domain, _initiatorMask, _ephemeralSecret,
                    hello.MaskedResponderIdentity);
                ValidateRemoteRegistration(_responderRegistration,
                    _expectedResponderIdentity);
                _responderIdentity = _responderRegistration.Identity;
                PpapCryptography.Encapsulate(
                    _responderRegistration.EncapsulationKeyBytes,
                    out responderCiphertext, out _responderIdentitySecret);
                responderDescriptor = PpapRegistrationDescriptor.FromRecord(
                    _responderRegistration);
            }

            try
            {
                var message = PpapWire.EncodeInitiatorProof(maskedInitiator,
                    responderCiphertext, responderDescriptor, _domain,
                    _ephemeralSecret);
                AddTranscript(message);
                _state = HandshakeState.InitiatorAwaitResponderProof;
                return InProgress(message);
            }
            finally
            {
                PpapCryptography.Clear(maskedInitiator);
                PpapCryptography.Clear(responderCiphertext);
            }
        }

        if (_state == HandshakeState.InitiatorAwaitResponderProof)
        {
            var proof = PpapWire.DecodeResponderProof(authData,
                _authenticateInitiator, _domain, _ephemeralSecret);
            AddTranscript(proof.TranscriptCore);

            if (_authenticateInitiator)
            {
                ValidateLocalDescriptor(proof.InitiatorRegistration,
                    _localIdentity);
                byte[] privateKey = null;
                byte[] publicKey = null;
                try
                {
                    privateKey = _provider.DerivePrivateKey(_localIdentity, _domain,
                        proof.InitiatorRegistration.Nonce,
                        proof.InitiatorRegistration.KdfProfile);
                    publicKey = PpapCryptography.GetPublicKey(privateKey);
                    _initiatorIdentitySecret = PpapCryptography.Decapsulate(
                        privateKey, proof.InitiatorCiphertext);
                    _initiatorRegistration = new PpapRegistrationRecord(
                        proof.InitiatorRegistration.Version, _initiatorIdentity,
                        proof.InitiatorRegistration.Kind,
                        proof.InitiatorRegistration.Nonce, publicKey,
                        proof.InitiatorRegistration.KdfProfile);
                }
                finally
                {
                    PpapCryptography.Clear(privateKey);
                    PpapCryptography.Clear(publicKey);
                }
            }

            DeriveHandshakeKeys();
            var expected = PpapCryptography.ComputeFinished(false,
                _responderFinishedKey, _transcriptHash);
            var verified = PpapCryptography.FixedTimeEquals(expected, proof.Finished);
            PpapCryptography.Clear(expected);
            PpapCryptography.Clear(proof.Finished);
            PpapCryptography.Clear(proof.TranscriptCore);
            if (!verified)
                throw new InvalidDataException("Responder Finished verification failed.");

            var initiatorFinished = PpapCryptography.ComputeFinished(true,
                _initiatorFinishedKey, _transcriptHash);
            try
            {
                var message = PpapWire.EncodeInitiatorFinished(initiatorFinished);
                _state = HandshakeState.Complete;
                ClearPostConfirmationSecrets();
                return Succeeded(message);
            }
            finally
            {
                PpapCryptography.Clear(initiatorFinished);
            }
        }

        throw new InvalidDataException("Invalid PPAP initiator state.");
    }

    AuthenticationResult ProcessResponder(object authData)
    {
        if (_state == HandshakeState.ResponderAwaitClientHello)
        {
            var raw = RequireBytes(authData);
            var hello = PpapWire.DecodeClientHello(raw);
            if (hello.Mode != _mode
                || !string.Equals(hello.Domain, _domain, StringComparison.Ordinal))
                throw new InvalidDataException("PPAP context mismatch.");
            AddTranscript(raw);
            _initiatorMask = hello.InitiatorMask;

            if (_authenticateResponder)
                _responderIdentity = _localIdentity.Identity;

            PpapCryptography.Encapsulate(hello.EphemeralKey,
                out var ephemeralCiphertext, out _ephemeralSecret);
            _responderMask = PpapCryptography.RandomBytes(
                PpapProtocol.IdentityMaskLength);
            byte[] maskedResponder = null;
            if (_authenticateResponder)
                maskedResponder = PpapCryptography.MaskIdentity(_domain,
                    _responderIdentity, _initiatorMask, _ephemeralSecret);

            try
            {
                var message = PpapWire.EncodeServerHello(ephemeralCiphertext,
                    _responderMask, maskedResponder);
                AddTranscript(message);
                _state = HandshakeState.ResponderAwaitInitiatorProof;
                return InProgress(message);
            }
            finally
            {
                PpapCryptography.Clear(ephemeralCiphertext);
                PpapCryptography.Clear(maskedResponder);
                PpapCryptography.Clear(hello.EphemeralKey);
            }
        }

        if (_state == HandshakeState.ResponderAwaitInitiatorProof)
        {
            var raw = RequireBytes(authData);
            var proof = PpapWire.DecodeInitiatorProof(raw,
                _authenticateInitiator, _authenticateResponder, _domain,
                _ephemeralSecret);
            AddTranscript(raw);

            if (_authenticateResponder)
            {
                ValidateLocalDescriptor(proof.ResponderRegistration,
                    _localIdentity);
                byte[] privateKey = null;
                byte[] publicKey = null;
                try
                {
                    privateKey = _provider.DerivePrivateKey(_localIdentity, _domain,
                        proof.ResponderRegistration.Nonce,
                        proof.ResponderRegistration.KdfProfile);
                    publicKey = PpapCryptography.GetPublicKey(privateKey);
                    _responderIdentitySecret = PpapCryptography.Decapsulate(
                        privateKey, proof.ResponderCiphertext);
                    _responderRegistration = new PpapRegistrationRecord(
                        proof.ResponderRegistration.Version, _responderIdentity,
                        proof.ResponderRegistration.Kind,
                        proof.ResponderRegistration.Nonce, publicKey,
                        proof.ResponderRegistration.KdfProfile);
                }
                finally
                {
                    PpapCryptography.Clear(privateKey);
                    PpapCryptography.Clear(publicKey);
                }
            }

            byte[] initiatorCiphertext = null;
            PpapRegistrationDescriptor initiatorDescriptor = null;
            if (_authenticateInitiator)
            {
                _initiatorRegistration = _provider.Registrations.ResolveMasked(
                    _domain, _responderMask, _ephemeralSecret,
                    proof.MaskedInitiatorIdentity);
                ValidateRemoteRegistration(_initiatorRegistration,
                    _expectedInitiatorIdentity);
                _initiatorIdentity = _initiatorRegistration.Identity;
                PpapCryptography.Encapsulate(
                    _initiatorRegistration.EncapsulationKeyBytes,
                    out initiatorCiphertext, out _initiatorIdentitySecret);
                initiatorDescriptor = PpapRegistrationDescriptor.FromRecord(
                    _initiatorRegistration);
            }

            byte[] core = null;
            byte[] protectedInitiatorDescriptor = null;
            byte[] responderFinished = null;
            try
            {
                core = PpapWire.EncodeResponderProofCore(initiatorCiphertext,
                    initiatorDescriptor, _domain, _ephemeralSecret,
                    out protectedInitiatorDescriptor);
                AddTranscript(core);
                DeriveHandshakeKeys();
                responderFinished = PpapCryptography.ComputeFinished(false,
                    _responderFinishedKey, _transcriptHash);
                var message = PpapWire.EncodeResponderProof(initiatorCiphertext,
                    protectedInitiatorDescriptor, responderFinished);
                _state = HandshakeState.ResponderAwaitInitiatorFinished;
                return InProgress(message);
            }
            finally
            {
                PpapCryptography.Clear(core);
                PpapCryptography.Clear(protectedInitiatorDescriptor);
                PpapCryptography.Clear(initiatorCiphertext);
                PpapCryptography.Clear(responderFinished);
            }
        }

        if (_state == HandshakeState.ResponderAwaitInitiatorFinished)
        {
            var remoteFinished = PpapWire.DecodeInitiatorFinished(authData);
            var expected = PpapCryptography.ComputeFinished(true,
                _initiatorFinishedKey, _transcriptHash);
            var verified = PpapCryptography.FixedTimeEquals(expected, remoteFinished);
            PpapCryptography.Clear(expected);
            PpapCryptography.Clear(remoteFinished);
            if (!verified)
                throw new InvalidDataException("Initiator Finished verification failed.");

            _state = HandshakeState.Complete;
            ClearPostConfirmationSecrets();
            return Succeeded(null);
        }

        throw new InvalidDataException("Invalid PPAP responder state.");
    }

    void DeriveHandshakeKeys()
    {
        _authenticationContext = PpapWire.EncodeAuthenticationContext(_mode,
            _domain, _initiatorIdentity,
            _authenticateInitiator ? (PpapIdentityKind?)GetInitiatorKind() : null,
            _responderIdentity,
            _authenticateResponder ? (PpapIdentityKind?)GetResponderKind() : null);
        _transcriptHash = PpapCryptography.ComputeTranscriptHash(
            _transcript, _authenticationContext);
        PpapCryptography.DeriveHandshakeKeys(_ephemeralSecret,
            _initiatorIdentitySecret, _responderIdentitySecret,
            _transcriptHash, _authenticationContext,
            out _sessionKey, out _initiatorFinishedKey,
            out _responderFinishedKey);

        PpapCryptography.Clear(_ephemeralSecret);
        PpapCryptography.Clear(_initiatorIdentitySecret);
        PpapCryptography.Clear(_responderIdentitySecret);
        _ephemeralSecret = null;
        _initiatorIdentitySecret = null;
        _responderIdentitySecret = null;
    }

    PpapIdentityKind GetInitiatorKind()
        => _initiatorRegistration?.Kind ?? _localIdentity.Kind;

    PpapIdentityKind GetResponderKind()
        => _responderRegistration?.Kind ?? _localIdentity.Kind;

    void ValidateRemoteRegistration(PpapRegistrationRecord record,
        string expectedIdentity)
    {
        if (record == null)
            throw new InvalidDataException("The masked identity could not be resolved.");
        if (expectedIdentity != null && !string.Equals(expectedIdentity,
            record.Identity, StringComparison.Ordinal))
            throw new InvalidDataException("The resolved identity was not expected.");
    }

    static void ValidateLocalDescriptor(PpapRegistrationDescriptor descriptor,
        PpapLocalIdentity identity)
    {
        if (descriptor == null || identity == null || descriptor.Kind != identity.Kind)
            throw new InvalidDataException("The local registration descriptor is invalid.");
        if (identity.Kind == PpapIdentityKind.PasswordDerived
            && !identity.KdfProfile.Equals(descriptor.KdfProfile))
            throw new InvalidDataException("The local registration KDF profile is not accepted.");
    }

    void AddTranscript(byte[] message)
    {
        if (message == null || message.Length == 0
            || message.Length > PpapProtocol.MaximumWireMessageBytes)
            throw new InvalidDataException("Invalid transcript message.");
        _transcript.Add((byte[])message.Clone());
    }

    static byte[] RequireBytes(object data)
    {
        if (!(data is byte[] value))
            throw new InvalidDataException("PPAP data must be a byte array.");
        return value;
    }

    AuthenticationResult InProgress(byte[] data)
        => new AuthenticationResult(AuthenticationRuling.InProgress, data);

    AuthenticationResult Succeeded(byte[] data)
    {
        var local = _direction == AuthenticationDirection.Initiator
            ? _initiatorIdentity : _responderIdentity;
        var remote = _direction == AuthenticationDirection.Initiator
            ? _responderIdentity : _initiatorIdentity;
        return new AuthenticationResult(AuthenticationRuling.Succeeded, data,
            local, remote, _sessionKey);
    }

    static AuthenticationResult Failed()
        => new AuthenticationResult(AuthenticationRuling.Failed, null);

    static string NormalizeOptionalIdentity(string identity)
        => identity == null ? null : PpapCryptography.NormalizeIdentity(identity);

    void ClearPostConfirmationSecrets()
    {
        PpapCryptography.Clear(_initiatorFinishedKey);
        PpapCryptography.Clear(_responderFinishedKey);
        PpapCryptography.Clear(_transcriptHash);
        PpapCryptography.Clear(_authenticationContext);
        PpapCryptography.Clear(_initiatorMask);
        PpapCryptography.Clear(_responderMask);
        _initiatorFinishedKey = null;
        _responderFinishedKey = null;
        _transcriptHash = null;
        _authenticationContext = null;
        _initiatorMask = null;
        _responderMask = null;
        foreach (var message in _transcript)
            PpapCryptography.Clear(message);
        _transcript.Clear();
    }

    void ClearHandshakeSecrets(bool clearSessionKey)
    {
        PpapCryptography.Clear(_ephemeralPrivateKey);
        PpapCryptography.Clear(_ephemeralSecret);
        PpapCryptography.Clear(_initiatorIdentitySecret);
        PpapCryptography.Clear(_responderIdentitySecret);
        PpapCryptography.Clear(_initiatorFinishedKey);
        PpapCryptography.Clear(_responderFinishedKey);
        PpapCryptography.Clear(_transcriptHash);
        PpapCryptography.Clear(_authenticationContext);
        if (clearSessionKey)
            PpapCryptography.Clear(_sessionKey);
        _ephemeralPrivateKey = null;
        _ephemeralSecret = null;
        _initiatorIdentitySecret = null;
        _responderIdentitySecret = null;
        _initiatorFinishedKey = null;
        _responderFinishedKey = null;
        if (clearSessionKey)
            _sessionKey = null;
        foreach (var message in _transcript)
            PpapCryptography.Clear(message);
        _transcript.Clear();
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_state == HandshakeState.Disposed)
                return;
            ClearHandshakeSecrets(clearSessionKey: true);
            PpapCryptography.Clear(_initiatorMask);
            PpapCryptography.Clear(_responderMask);
            PpapCryptography.Clear(_transcriptHash);
            PpapCryptography.Clear(_authenticationContext);
            ClearRotationSecrets();
            _state = HandshakeState.Disposed;
        }
    }
}
