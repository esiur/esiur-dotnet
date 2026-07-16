using System;
using System.IO;

namespace Esiur.Security.Authority.Providers.Ppap;

public sealed partial class PpapAuthenticationHandler
{
    enum RotationState
    {
        NotStarted,
        SubjectAwaitChallenge,
        SubjectAwaitCommit,
        VerifierAwaitProof,
        ResponderAwaitNext,
        InitiatorAwaitResponderOffer,
        InitiatorAwaitResponderCommitAck,
        ResponderAwaitDone,
        InitiatorDoneSent,
        Complete,
        Failed,
    }

    RotationState _rotationState;
    PpapIdentityRole _rotationRole;
    PpapRegistrationRecord _pendingRotation;
    byte[] _rotationPrivateKey;
    byte[] _rotationChallengeSecret;
    byte[] _rotationOffer;
    byte[] _rotationChallenge;

    /// <summary>
    /// True for every live PPAP handler because protocol completion requires an
    /// encrypted post-authentication phase. Static identities use a no-op Done exchange.
    /// </summary>
    public bool RequiresKeyRotation
    {
        get
        {
            lock (_sync)
            {
                // PPAP always requires the encrypted post-authentication phase. It is
                // a no-op Done exchange when every authenticated identity is static.
                return _state != HandshakeState.Failed
                    && _state != HandshakeState.Disposed;
            }
        }
    }

    public AuthenticationKeyRotationResult BeginKeyRotation()
    {
        lock (_sync)
        {
            try
            {
                if (_direction != AuthenticationDirection.Initiator
                    || _state != HandshakeState.Complete
                    || _rotationState != RotationState.NotStarted
                    || !RequiresKeyRotation)
                    throw new InvalidOperationException("PPAP key rotation cannot be started in the current state.");

                if (RoleRequiresRotation(PpapIdentityRole.Initiator))
                    return CreateRotationOffer(PpapIdentityRole.Initiator);

                if (!RoleRequiresRotation(PpapIdentityRole.Responder))
                {
                    _rotationState = RotationState.InitiatorDoneSent;
                    var done = PpapWire.EncodeRotationDone();
                    ClearCompletedRotationSecrets();
                    return RotationSucceeded(done);
                }

                _rotationState = RotationState.InitiatorAwaitResponderOffer;
                return RotationInProgress(PpapWire.EncodeRotationStart(
                    PpapIdentityRole.Responder));
            }
            catch
            {
                _rotationState = RotationState.Failed;
                ClearCompletedRotationSecrets();
                return RotationFailed();
            }
        }
    }

    public AuthenticationKeyRotationResult ProcessKeyRotation(object data)
    {
        lock (_sync)
        {
            try
            {
                if (_state != HandshakeState.Complete || !RequiresKeyRotation)
                    throw new InvalidOperationException("PPAP key rotation is not available.");

                if (_direction == AuthenticationDirection.Initiator)
                    return ProcessInitiatorRotation(data);
                return ProcessResponderRotation(data);
            }
            catch
            {
                _rotationState = RotationState.Failed;
                ClearCompletedRotationSecrets();
                return RotationFailed();
            }
        }
    }

    AuthenticationKeyRotationResult ProcessInitiatorRotation(object data)
    {
        if (_rotationState == RotationState.SubjectAwaitChallenge)
            return ProcessRotationChallenge(data, PpapIdentityRole.Initiator);

        if (_rotationState == RotationState.SubjectAwaitCommit)
        {
            var committedVersion = PpapWire.DecodeRotationCommit(data,
                PpapIdentityRole.Initiator);
            CommitSubjectRotation(PpapIdentityRole.Initiator, committedVersion);

            if (RoleRequiresRotation(PpapIdentityRole.Responder))
            {
                _rotationState = RotationState.InitiatorAwaitResponderOffer;
                return RotationInProgress(PpapWire.EncodeRotationStart(
                    PpapIdentityRole.Responder));
            }

            _rotationState = RotationState.InitiatorDoneSent;
            var done = PpapWire.EncodeRotationDone();
            ClearCompletedRotationSecrets();
            return RotationSucceeded(done);
        }

        if (_rotationState == RotationState.InitiatorAwaitResponderOffer)
            return ProcessRotationOffer(data, PpapIdentityRole.Responder);

        if (_rotationState == RotationState.VerifierAwaitProof)
            return ProcessRotationProof(data, PpapIdentityRole.Responder);

        if (_rotationState == RotationState.InitiatorAwaitResponderCommitAck)
        {
            var version = PpapWire.DecodeRotationCommitAck(data,
                PpapIdentityRole.Responder);
            if (_responderRegistration == null
                || version != _responderRegistration.Version)
                throw new InvalidDataException("Unexpected responder rotation version.");
            _rotationState = RotationState.InitiatorDoneSent;
            var done = PpapWire.EncodeRotationDone();
            ClearCompletedRotationSecrets();
            return RotationSucceeded(done);
        }

        throw new InvalidDataException("Unexpected PPAP initiator rotation message.");
    }

    AuthenticationKeyRotationResult ProcessResponderRotation(object data)
    {
        if (_rotationState == RotationState.NotStarted)
        {
            var type = PpapWire.PeekMessageType(data);
            if (type == PpapMessageType.RotationDone
                && !RoleRequiresRotation(PpapIdentityRole.Initiator)
                && !RoleRequiresRotation(PpapIdentityRole.Responder))
                return ProcessRotationDone(data);
            if (type == PpapMessageType.RotationOffer
                && RoleRequiresRotation(PpapIdentityRole.Initiator))
                return ProcessRotationOffer(data, PpapIdentityRole.Initiator);
            if (type == PpapMessageType.RotationStart
                && !RoleRequiresRotation(PpapIdentityRole.Initiator)
                && RoleRequiresRotation(PpapIdentityRole.Responder))
            {
                var role = PpapWire.DecodeRotationStart(data);
                if (role != PpapIdentityRole.Responder)
                    throw new InvalidDataException("Unexpected rotation role.");
                return CreateRotationOffer(PpapIdentityRole.Responder);
            }
            throw new InvalidDataException("Unexpected initial PPAP rotation message.");
        }

        if (_rotationState == RotationState.VerifierAwaitProof)
            return ProcessRotationProof(data, PpapIdentityRole.Initiator);

        if (_rotationState == RotationState.ResponderAwaitNext)
        {
            var type = PpapWire.PeekMessageType(data);
            if (type == PpapMessageType.RotationStart
                && RoleRequiresRotation(PpapIdentityRole.Responder))
            {
                var role = PpapWire.DecodeRotationStart(data);
                if (role != PpapIdentityRole.Responder)
                    throw new InvalidDataException("Unexpected rotation role.");
                return CreateRotationOffer(PpapIdentityRole.Responder);
            }
            if (type == PpapMessageType.RotationDone
                && !RoleRequiresRotation(PpapIdentityRole.Responder))
                return ProcessRotationDone(data);
            throw new InvalidDataException("Unexpected PPAP rotation continuation.");
        }

        if (_rotationState == RotationState.SubjectAwaitChallenge)
            return ProcessRotationChallenge(data, PpapIdentityRole.Responder);

        if (_rotationState == RotationState.SubjectAwaitCommit)
        {
            var committedVersion = PpapWire.DecodeRotationCommit(data,
                PpapIdentityRole.Responder);
            CommitSubjectRotation(PpapIdentityRole.Responder, committedVersion);
            _rotationState = RotationState.ResponderAwaitDone;
            return RotationInProgress(PpapWire.EncodeRotationCommitAck(
                PpapIdentityRole.Responder, committedVersion));
        }

        if (_rotationState == RotationState.ResponderAwaitDone)
            return ProcessRotationDone(data);

        throw new InvalidDataException("Unexpected PPAP responder rotation message.");
    }

    AuthenticationKeyRotationResult CreateRotationOffer(PpapIdentityRole role)
    {
        if (!LocalIsSubject(role) || !RoleRequiresRotation(role)
            || _localIdentity == null
            || _localIdentity.Kind != PpapIdentityKind.PasswordDerived)
            throw new InvalidOperationException("The local endpoint cannot rotate this identity role.");

        var current = GetRoleRegistration(role);
        var nonce = PpapCryptography.RandomBytes(PpapProtocol.RegistrationNonceLength);
        byte[] publicKey = null;
        try
        {
            _rotationPrivateKey = _provider.DerivePrivateKey(_localIdentity,
                _domain, nonce, _localIdentity.KdfProfile,
                postAuthentication: true);
            publicKey = PpapCryptography.GetPublicKey(_rotationPrivateKey);
            _pendingRotation = new PpapRegistrationRecord(current.Version + 1,
                current.Identity, PpapIdentityKind.PasswordDerived, nonce,
                publicKey, _localIdentity.KdfProfile);
            var offer = PpapWire.EncodeRotationOffer(role, current.Identity,
                current.Version, nonce, publicKey, _localIdentity.KdfProfile);
            ReplaceRotationBytes(ref _rotationOffer, offer);
            _rotationRole = role;
            _rotationState = RotationState.SubjectAwaitChallenge;
            return RotationInProgress(offer);
        }
        finally
        {
            PpapCryptography.Clear(publicKey);
            PpapCryptography.Clear(nonce);
        }
    }

    AuthenticationKeyRotationResult ProcessRotationOffer(object data,
        PpapIdentityRole expectedRole)
    {
        if (LocalIsSubject(expectedRole))
            throw new InvalidOperationException("A subject cannot verify its own rotation.");
        var raw = RequireBytes(data);
        var offer = PpapWire.DecodeRotationOffer(raw);
        if (offer.Role != expectedRole)
            throw new InvalidDataException("Unexpected rotation role.");

        var current = GetRoleRegistration(expectedRole);
        if (current == null
            || current.Kind != PpapIdentityKind.PasswordDerived
            || !string.Equals(current.Identity, offer.Identity, StringComparison.Ordinal)
            || current.Version != offer.ExpectedVersion
            || !current.KdfProfile.Equals(offer.KdfProfile))
            throw new InvalidDataException("The rotation offer does not match the authenticated registration.");

        _pendingRotation = new PpapRegistrationRecord(current.Version + 1,
            current.Identity, PpapIdentityKind.PasswordDerived, offer.Nonce,
            offer.EncapsulationKey, offer.KdfProfile);
        PpapCryptography.Encapsulate(offer.EncapsulationKey,
            out var ciphertext, out _rotationChallengeSecret);
        try
        {
            var challenge = PpapWire.EncodeRotationChallenge(expectedRole,
                ciphertext);
            ReplaceRotationBytes(ref _rotationOffer, raw);
            ReplaceRotationBytes(ref _rotationChallenge, challenge);
            _rotationRole = expectedRole;
            _rotationState = RotationState.VerifierAwaitProof;
            return RotationInProgress(challenge);
        }
        finally
        {
            PpapCryptography.Clear(ciphertext);
            PpapCryptography.Clear(offer.Nonce);
            PpapCryptography.Clear(offer.EncapsulationKey);
        }
    }

    AuthenticationKeyRotationResult ProcessRotationChallenge(object data,
        PpapIdentityRole expectedRole)
    {
        if (!LocalIsSubject(expectedRole) || _rotationRole != expectedRole
            || _rotationPrivateKey == null || _pendingRotation == null)
            throw new InvalidOperationException("Unexpected rotation challenge.");
        var raw = RequireBytes(data);
        var challenge = PpapWire.DecodeRotationChallenge(raw);
        if (challenge.Role != expectedRole)
            throw new InvalidDataException("Unexpected rotation challenge role.");

        var secret = PpapCryptography.Decapsulate(_rotationPrivateKey,
            challenge.Ciphertext);
        try
        {
            ReplaceRotationBytes(ref _rotationChallenge, raw);
            var proof = PpapCryptography.ComputeRotationProof(secret,
                _sessionKey, _rotationOffer, _rotationChallenge);
            try
            {
                _rotationState = RotationState.SubjectAwaitCommit;
                return RotationInProgress(PpapWire.EncodeRotationProof(
                    expectedRole, proof));
            }
            finally
            {
                PpapCryptography.Clear(proof);
            }
        }
        finally
        {
            PpapCryptography.Clear(secret);
            PpapCryptography.Clear(challenge.Ciphertext);
            PpapCryptography.Clear(_rotationPrivateKey);
            _rotationPrivateKey = null;
        }
    }

    AuthenticationKeyRotationResult ProcessRotationProof(object data,
        PpapIdentityRole expectedRole)
    {
        if (LocalIsSubject(expectedRole) || _rotationRole != expectedRole
            || _rotationChallengeSecret == null || _pendingRotation == null)
            throw new InvalidOperationException("Unexpected rotation proof.");
        var proof = PpapWire.DecodeRotationProof(data);
        if (proof.Role != expectedRole)
            throw new InvalidDataException("Unexpected rotation proof role.");
        var expected = PpapCryptography.ComputeRotationProof(
            _rotationChallengeSecret, _sessionKey, _rotationOffer,
            _rotationChallenge);
        var verified = PpapCryptography.FixedTimeEquals(expected, proof.Proof);
        PpapCryptography.Clear(expected);
        PpapCryptography.Clear(proof.Proof);
        if (!verified)
            throw new InvalidDataException("Rotation proof verification failed.");

        var current = GetRoleRegistration(expectedRole);
        if (!_provider.Registrations.TryRotate(_domain, current.Identity,
            current.Version, _pendingRotation))
            throw new InvalidOperationException("The registration changed concurrently.");

        SetRoleRegistration(expectedRole, _pendingRotation);
        var committedVersion = _pendingRotation.Version;
        _pendingRotation = null;
        ClearRotationExchangeSecrets();

        if (expectedRole == PpapIdentityRole.Initiator)
            _rotationState = RotationState.ResponderAwaitNext;
        else
            _rotationState = RotationState.InitiatorAwaitResponderCommitAck;

        return RotationInProgress(PpapWire.EncodeRotationCommit(expectedRole,
            committedVersion));
    }

    void CommitSubjectRotation(PpapIdentityRole role, long committedVersion)
    {
        if (!LocalIsSubject(role) || _rotationRole != role
            || _pendingRotation == null
            || _pendingRotation.Version != committedVersion)
            throw new InvalidDataException("Unexpected rotation commit.");
        SetRoleRegistration(role, _pendingRotation);
        _pendingRotation = null;
        ClearRotationExchangeSecrets();
    }

    AuthenticationKeyRotationResult ProcessRotationDone(object data)
    {
        PpapWire.DecodeRotationDone(data);
        _rotationState = RotationState.Complete;
        ClearCompletedRotationSecrets();
        return RotationSucceeded(null);
    }

    bool RoleRequiresRotation(PpapIdentityRole role)
    {
        var registration = GetRoleRegistration(role);
        return registration != null
            && registration.Kind == PpapIdentityKind.PasswordDerived;
    }

    bool LocalIsSubject(PpapIdentityRole role)
        => (_direction == AuthenticationDirection.Initiator
                && role == PpapIdentityRole.Initiator)
            || (_direction == AuthenticationDirection.Responder
                && role == PpapIdentityRole.Responder);

    PpapRegistrationRecord GetRoleRegistration(PpapIdentityRole role)
        => role == PpapIdentityRole.Initiator
            ? _initiatorRegistration : _responderRegistration;

    void SetRoleRegistration(PpapIdentityRole role,
        PpapRegistrationRecord registration)
    {
        if (role == PpapIdentityRole.Initiator)
            _initiatorRegistration = registration;
        else
            _responderRegistration = registration;
    }

    static AuthenticationKeyRotationResult RotationInProgress(byte[] data)
        => new AuthenticationKeyRotationResult(
            AuthenticationKeyRotationRuling.InProgress, data);

    static AuthenticationKeyRotationResult RotationSucceeded(byte[] data)
        => new AuthenticationKeyRotationResult(
            AuthenticationKeyRotationRuling.Succeeded, data);

    static AuthenticationKeyRotationResult RotationFailed()
        => new AuthenticationKeyRotationResult(
            AuthenticationKeyRotationRuling.Failed, null,
            "PPAP authentication key rotation failed.");

    static void ReplaceRotationBytes(ref byte[] target, byte[] value)
    {
        PpapCryptography.Clear(target);
        target = value == null ? null : (byte[])value.Clone();
    }

    void ClearRotationExchangeSecrets()
    {
        PpapCryptography.Clear(_rotationPrivateKey);
        PpapCryptography.Clear(_rotationChallengeSecret);
        PpapCryptography.Clear(_rotationOffer);
        PpapCryptography.Clear(_rotationChallenge);
        _rotationPrivateKey = null;
        _rotationChallengeSecret = null;
        _rotationOffer = null;
        _rotationChallenge = null;
    }

    void ClearRotationSecrets()
    {
        ClearRotationExchangeSecrets();
        _pendingRotation = null;
    }

    void ClearCompletedRotationSecrets()
    {
        ClearRotationSecrets();
        PpapCryptography.Clear(_sessionKey);
        _sessionKey = null;
    }
}
