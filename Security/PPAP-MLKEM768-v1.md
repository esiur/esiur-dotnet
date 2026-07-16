# PPAP ML-KEM-768 v1

## Status and scope

This document specifies the byte-for-byte interoperable form of Esiur's fixed
PPAP suite named `ppap-mlkem768-v1`, including its required encrypted
post-authentication registration rotation. It describes the protocol currently
implemented by `PpapWire`, `PpapCryptography`, `PpapAuthenticationHandler`, and
`EpConnection`.

The key words **MUST**, **MUST NOT**, **REQUIRED**, **SHOULD**, and **SHOULD NOT**
are normative requirements. There is no algorithm negotiation inside PPAP v1.

## 1. Notation and canonical encodings

- `A || B` is octet concatenation.
- `U8(n)` is one unsigned octet.
- `U32(n)` is an unsigned 32-bit integer in big-endian order. The current wire
  reader accepts only `0..2^31-1`; values with the high bit set are invalid.
- `U64(n)` is an unsigned 64-bit integer in big-endian order. The implementation
  uses a non-negative signed 64-bit value, so the accepted range is
  `0..2^63-1`.
- `F(x)` is `U32(len(x)) || x`.
- `FRAMES(x1, ..., xn)` is `F(x1) || ... || F(xn)`. A null or absent input is
  encoded as a zero-length field, `00 00 00 00`.
- `H(x)` is SHA3-256 over `x`.
- `MAC(k, x)` is HMAC-SHA3-256 with key `k` over `x`.
- `HKDF(ikm, salt, info, L)` is RFC 5869 HKDF using SHA3-256 and producing `L`
  octets.
- `UTF8(s)` is UTF-8 without a BOM. Encoders and decoders MUST reject invalid
  Unicode/UTF-8. Identities and domains MUST be normalized to Unicode NFC before
  UTF-8 encoding and before ordinal comparison.

An identity MUST be non-null, non-empty, and not entirely whitespace. A domain
may be empty; a null domain is the empty domain. An encoded identity or domain
MUST NOT exceed 512 octets. Implementations MUST use the canonical encodings in
this section even when their host language has a different default byte order,
text encoding, or string comparison.

## 2. Fixed suite

| Parameter | Required value |
|---|---:|
| Protocol name | `ppap-mlkem768-v1` |
| PPAP wire version | `1` |
| KEM | ML-KEM-768 |
| ML-KEM public key | 1184 octets |
| ML-KEM private-key encoding | 2400 octets |
| ML-KEM ciphertext | 1088 octets |
| ML-KEM shared secret | 32 octets |
| Deterministic ML-KEM seed | 64 octets |
| Hash/MAC/Finished | 32 octets |
| Identity mask and registration nonce | 32 octets each |
| Session key | 32 octets |
| Descriptor AEAD | AES-256-GCM, 128-bit tag |
| Descriptor plaintext/key/nonce/tag | 128/32/12/16 octets |
| Protected descriptor | 144 octets |
| Maximum password | 4096 octets, non-empty |
| Maximum complete PPAP envelope | 8192 octets |

ML-KEM key parsing MUST validate the encoded key as well as its exact length.
ML-KEM encapsulation randomness and every randomly generated ephemeral/static
key seed, identity mask, and registration nonce MUST come from a
cryptographically secure random source.

### 2.1 Domain-separation labels

Every label below is the exact sequence of displayed ASCII octets, without a
terminating NUL:

| Purpose | ASCII label |
|---|---|
| Password seed | `esiur/ppap-mlkem768-v1/password-seed` |
| Masked identity | `esiur/ppap-mlkem768-v1/masked-identity` |
| Descriptor salt | `esiur/ppap-mlkem768-v1/descriptor-salt` |
| Descriptor key | `esiur/ppap-mlkem768-v1/descriptor-key` |
| Descriptor nonce | `esiur/ppap-mlkem768-v1/descriptor-nonce` |
| Descriptor AAD | `esiur/ppap-mlkem768-v1/descriptor-aad` |
| Initiator descriptor role | `esiur/ppap-mlkem768-v1/initiator-registration` |
| Responder descriptor role | `esiur/ppap-mlkem768-v1/responder-registration` |
| Transcript | `esiur/ppap-mlkem768-v1/transcript` |
| Key-schedule input | `esiur/ppap-mlkem768-v1/key-schedule` |
| Session-key info | `esiur/ppap-mlkem768-v1/session-key` |
| Initiator Finished-key info | `esiur/ppap-mlkem768-v1/initiator-finished-key` |
| Responder Finished-key info | `esiur/ppap-mlkem768-v1/responder-finished-key` |
| Initiator Finished | `esiur/ppap-mlkem768-v1/initiator-finished` |
| Responder Finished | `esiur/ppap-mlkem768-v1/responder-finished` |
| Rotation proof | `esiur/ppap-mlkem768-v1/rotation-proof` |

## 3. Codes and PPAP envelope

All numeric codes in this section are hexadecimal. Authentication modes use
these exact octets:

| Code | Name | Authenticated role(s) |
|---:|---|---|
| `01` | `InitializerIdentity` | Initiator |
| `02` | `ResponderIdentity` | Responder |
| `03` | `DualIdentity` | Initiator and responder |

Mode `00` (`None`) is not supported by PPAP. The code spelling
`InitializerIdentity` is retained for compatibility; it means the connection
initiator.

Identity roles are initiator `01` and responder `02`. Identity kinds are
password-derived `01` and static ML-KEM `02`.

Every PPAP message is encoded as:

```text
50 50 41 50 || 01 || U8(messageType) || U32(payloadLength) || payload
```

`50 50 41 50` is ASCII `PPAP`. The total envelope MUST be between 10 and 8192
octets, so the payload is at most 8182 octets. The declared payload length MUST
equal the exact remaining length, and trailing data is forbidden.

| Type | Code | Type | Code |
|---|---:|---|---:|
| ClientHello | `01` | RotationStart | `10` |
| ServerHello | `02` | RotationOffer | `11` |
| InitiatorProof | `03` | RotationChallenge | `12` |
| ResponderProof | `04` | RotationProof | `13` |
| InitiatorFinished | `05` | RotationCommit | `14` |
|  |  | RotationCommitAck | `15` |
|  |  | RotationDone | `16` |

All variable fields below use `F(value)`, including absent fields. A decoder
MUST enforce each field's exact required size and MUST reject trailing payload
octets.

## 4. Registration material

### 4.1 Password-derived ML-KEM key

The default KDF profile is Argon2id version `0x13` (1.3), memory 32768 KiB,
three iterations, and parallelism one. An accepted profile MUST satisfy all of:

- version is exactly `0x13`;
- memory is 8192 through 262144 KiB, inclusive;
- iterations are 1 through 10, inclusive;
- parallelism is 1 through 16, inclusive; and
- memory in KiB is at least `parallelism * 8`.

Given a 32-octet registration nonce, password octets are converted to a
deterministic ML-KEM-768 private key as follows:

```text
argonInput = FRAMES(
    ASCII("esiur/ppap-mlkem768-v1/password-seed"),
    UTF8(domain),
    UTF8(identity),
    passwordBytes,
    registrationNonce)

seed = Argon2id(
    password = argonInput,
    salt = registrationNonce,
    version/memory/iterations/parallelism = registration profile,
    outputLength = 64)

d = seed[0..32)
z = seed[32..64)
(publicKey, privateKey) = ML-KEM-768.KeyGen_internal(d, z)
```

`KeyGen_internal` is the deterministic internal key-generation operation from
FIPS 203; the public and private values use the standard ML-KEM-768 encapsulation
and decapsulation-key encodings. This is the cross-language meaning of the .NET
implementation's `MLKemPrivateKeyParameters.FromSeed(seed)`. The text-password
API normalizes the password to NFC and then UTF-8 encodes it. The byte-password
API uses the supplied octets unchanged. Provisioning and authentication
implementations MUST make the same choice.

A static identity instead uses a provisioned ML-KEM-768 key pair and has neither
a registration nonce nor a KDF profile. The `CreateStatic` API generates a new
pair from a cryptographically random seed; the `FromStaticKey` API may import an
existing validated 2400-octet private-key encoding.

### 4.2 Canonical registration descriptor

```text
U8(kind)
|| U64(version)
|| F(nonce)
|| U8(hasProfile)
|| profile-if-present

profile-if-present =
    U32(argonVersion)
    || U32(memoryKiB)
    || U32(iterations)
    || U32(parallelism)
```

The version MUST be at least one. A password-derived descriptor has a 32-octet
nonce, `hasProfile = 01`, and the 16-octet profile, for a canonical total of 62
octets. A static descriptor has an empty nonce and `hasProfile = 00`, for a
canonical total of 14 octets. Other marker values, kinds, combinations, and
trailing data MUST be rejected. The generic descriptor decoder bounds input to
10 through 128 octets.

### 4.3 Protected handshake descriptor

Descriptors in `InitiatorProof` and `ResponderProof` are protected independently
of the later EP transport encryption. The plaintext is:

```text
F(canonicalDescriptor) || zeroPadding
```

It is exactly 128 octets. The descriptor length MUST be 1 through 124, and every
padding octet MUST be zero. Define the role-specific values:

| Described registration | `role` | `roleLabel` | Containing type | `binding` |
|---|---:|---|---:|---|
| Initiator | `01` | initiator descriptor role label | `04` ResponderProof | `01 01 04` |
| Responder | `02` | responder descriptor role label | `03` InitiatorProof | `01 02 03` |

Here the first binding octet is the PPAP wire version. Let `D = UTF8(domain)`:

```text
salt  = H(FRAMES(descriptor-salt-label, roleLabel, D, binding))
key   = HKDF(ephemeralSecret, salt,
             FRAMES(descriptor-key-label, roleLabel, D, binding), 32)
nonce = HKDF(ephemeralSecret, salt,
             FRAMES(descriptor-nonce-label, roleLabel, D, binding), 12)
aad   = FRAMES(descriptor-aad-label, roleLabel, D, binding)
```

The protected descriptor is AES-256-GCM encryption of the 128-octet plaintext,
with `key`, `nonce`, and `aad`, serialized as 128 octets of ciphertext followed
by the 16-octet tag. The derived nonce is not sent. An implementation MUST
encrypt at most one descriptor for each role under one ephemeral secret. In
particular, the responder MUST reuse the same protected initiator descriptor
when constructing `ResponderProofCore` and the transmitted `ResponderProof`.

## 5. Authentication messages

Let `ephemeralPublicKey` be the initiator's fresh ML-KEM-768 public key; `mI`
and `mR` are fresh 32-octet masks selected by initiator and responder.

```text
ClientHello (01) payload =
    U8(mode)
    || F(UTF8(domain))
    || F(ephemeralPublicKey[1184])
    || F(mI[32])

ServerHello (02) payload =
    F(ephemeralCiphertext[1088])
    || F(mR[32])
    || F(maskedResponderIdentity[32] or empty)

InitiatorProof (03) payload =
    F(maskedInitiatorIdentity[32] or empty)
    || F(responderIdentityCiphertext[1088] or empty)
    || F(protectedResponderDescriptor[144] or empty)

ResponderProof (04) payload =
    F(initiatorIdentityCiphertext[1088] or empty)
    || F(protectedInitiatorDescriptor[144] or empty)
    || F(responderFinished[32])

InitiatorFinished (05) payload =
    F(initiatorFinished[32])
```

Field presence is exact:

| Field | `InitializerIdentity` | `ResponderIdentity` | `DualIdentity` |
|---|:---:|:---:|:---:|
| `maskedResponderIdentity` | empty | 32 | 32 |
| `maskedInitiatorIdentity` | 32 | empty | 32 |
| responder identity ciphertext + descriptor | empty | present | present |
| initiator identity ciphertext + descriptor | present | empty | present |

An absent field is encoded as `F(empty)`, not omitted.

### 5.1 KEM and masked identities

The responder encapsulates to `ephemeralPublicKey`; both endpoints call the
resulting 32-octet value `ephemeralSecret`. For each authenticated identity, the
endpoint holding that identity's verifier registration encapsulates to its
registered public key. The identity subject decapsulates using its static or
password-derived private key. The resulting values are
`initiatorIdentitySecret` and `responderIdentitySecret`; the secret for an
unauthenticated role is empty in the key schedule.

An identity is masked as:

```text
maskedIdentity = MAC(
    ephemeralSecret,
    FRAMES(masked-identity-label, UTF8(domain), UTF8(identity), mask))
```

The responder identity uses `mI`; the initiator identity uses `mR`. A verifier
store resolves the result only within the normalized domain. It MUST compare
candidates in constant time, reject no match or more than one match, and MUST
NOT persist the per-handshake `ephemeralSecret` or masked value as a stable
lookup token.

This masking provides identity confidentiality against a passive observer of
the clear authentication exchange. It does not provide anonymity from an active
peer: a peer participating in the ephemeral KEM knows `ephemeralSecret` and can
test identities it is otherwise able to enumerate.

### 5.2 Authentication context, transcript, and keys

The authentication context is:

```text
FRAMES(
    ASCII("ppap-mlkem768-v1"),
    U8(mode),
    UTF8(domain),
    UTF8(initiatorIdentity) or empty,
    UTF8(responderIdentity) or empty,
    U8(initiatorKind or 0) || U8(responderKind or 0))
```

`ResponderProofCore` is a complete PPAP envelope of type `04` whose payload is
only:

```text
F(initiatorIdentityCiphertext or empty)
|| F(protectedInitiatorDescriptor or empty)
```

It omits the Finished field entirely; it does not append `F(empty)`. Its PPAP
payload length therefore differs from the transmitted `ResponderProof`.

```text
transcriptHash = H(FRAMES(
    transcript-label,
    authenticationContext,
    full ClientHello envelope,
    full ServerHello envelope,
    full InitiatorProof envelope,
    ResponderProofCore envelope))

ikm = FRAMES(
    key-schedule-label,
    ephemeralSecret,
    initiatorIdentitySecret or empty,
    responderIdentitySecret or empty)

sessionKey = HKDF(
    ikm, transcriptHash,
    FRAMES(session-key-label, authenticationContext), 32)

initiatorFinishedKey = HKDF(
    ikm, transcriptHash,
    FRAMES(initiator-finished-key-label, authenticationContext), 32)

responderFinishedKey = HKDF(
    ikm, transcriptHash,
    FRAMES(responder-finished-key-label, authenticationContext), 32)

initiatorFinished = MAC(
    initiatorFinishedKey,
    FRAMES(initiator-finished-label, transcriptHash))

responderFinished = MAC(
    responderFinishedKey,
    FRAMES(responder-finished-label, transcriptHash))
```

Finished values MUST be compared in constant time. A successfully returned
ML-KEM decapsulation alone MUST NOT be treated as authentication; the Finished
exchange confirms possession and the complete transcript.

### 5.3 Authentication state sequence

All three supported modes use the same ordered state sequence; only the field
presence from the preceding table differs.

| Step | Sender | PPAP message | EP carriage |
|---:|---|---|---|
| 1 | Initiator | ClientHello | `Initialize`, `SessionHeaders.AuthenticationData` |
| 2 | Responder | ServerHello | `ProceedToHandshake`, `SessionHeaders.AuthenticationData` |
| 3 | Initiator | InitiatorProof | `Handshake` action |
| 4 | Responder | ResponderProof | `Handshake` action |
| 5 | Initiator | InitiatorFinished | `FinalHandshake` action |
| 6 | Responder | no PPAP message | encrypted `Established` event |

The initiator validates the responder Finished before sending step 5. The
responder validates the initiator Finished before step 6. The session key,
local identity, and remote identity become handshake results only on successful
validation. PPAP still is not application-ready at this point: the protected
post-authentication phase in section 7 is mandatory.

## 6. Rotation messages and proof

The following are ordinary PPAP envelopes using the type codes in section 3.
All role fields MUST be initiator `01` or responder `02`.

```text
RotationStart payload =
    U8(role)

RotationOffer payload =
    U8(role)
    || F(UTF8(identity))
    || U64(expectedVersion)
    || F(newPublicKey[1184])
    || F(newPasswordDescriptor[62])

RotationChallenge payload =
    U8(role)
    || F(ciphertext[1088])

RotationProof payload =
    U8(role)
    || F(proof[32])

RotationCommit payload =
    U8(role)
    || U64(committedVersion)

RotationCommitAck payload =
    U8(role)
    || U64(committedVersion)

RotationDone payload = empty
```

For an offer, `expectedVersion` MUST be in `1..2^63-2`. The embedded descriptor
MUST be password-derived and its version MUST equal `expectedVersion + 1`.
Commit and acknowledgement versions MUST be at least two. The descriptor in an
offer is not protected by the descriptor AEAD from section 4.3; the complete
rotation exchange is instead REQUIRED to be inside the negotiated encrypted EP
transport.

The subject derives a new password key using a fresh 32-octet nonce, the same
identity/domain, and the unchanged KDF profile. The verifier encapsulates to the
offered public key, producing `challengeSecret` and the challenge ciphertext.
Using the exact, complete PPAP envelope octets as received or sent:

```text
rotationContext = H(FRAMES(
    rotation-proof-label,
    sessionKey,
    full RotationOffer envelope,
    full RotationChallenge envelope))

proof = MAC(challengeSecret, rotationContext)
```

The verifier MUST compare the proof in constant time. ML-KEM decapsulation does
not by itself validate a challenge.

### 6.1 Atomic registration commit

After proof verification, the verifier MUST perform one linearizable atomic
compare-and-swap keyed by the normalized domain and identity. A separate read
followed by an unconditional write is not conforming. The operation MUST return
failure without mutation unless all of these are true:

- the current record exists and its version equals `expectedVersion`;
- the replacement identity is exactly the same normalized identity under
  ordinal comparison;
- the replacement version is exactly `expectedVersion + 1`;
- both records are password-derived;
- the Argon2 version, memory, iterations, and parallelism are unchanged;
- the new nonce is not reused; and
- the new encapsulation public key is not reused.

Material equality checks SHOULD be constant-time. At minimum, equality with the
current nonce and key MUST be rejected; a durable store that retains historical
material MUST also reject known historical reuse. A database implementation
MUST use a conditional update or transaction that provides the same CAS
semantics.

The verifier MUST NOT send `RotationCommit` unless the CAS succeeds. The subject
MUST retain its pending registration until it receives a commit for the exact
role and pending version, and only then adopt it. A CAS failure, version mismatch,
or concurrent update terminates the protocol rather than being retried in the
same session.

Consequently, when multiple successfully authenticated sessions concurrently
offer replacements for the same registration version, exactly one CAS can win.
The other sessions fail rotation and MUST begin a fresh authentication attempt
against the newly committed version before retrying.

The two role updates in `DualIdentity` mode are sequential CAS operations, not
one transaction spanning both records. A committed initiator-role update is not
rolled back if the later responder-role rotation fails; a retry starts from the
versions then present in the store.

## 7. Required post-authentication state sequence

A role requires a real rotation exactly when it was authenticated and its
registration kind is password-derived. Static identities are never rotated.
The initiator role is processed before the responder role. `I` and `R` below are
the connection initiator and responder; a parenthesized role is the registration
being rotated.

```text
No password-derived role:
    I -> R  RotationDone

Initiator role only:
    I -> R  RotationOffer(I)
    R -> I  RotationChallenge(I)
    I -> R  RotationProof(I)
    R -> I  RotationCommit(I)       # after responder-side CAS
    I -> R  RotationDone

Responder role only:
    I -> R  RotationStart(R)
    R -> I  RotationOffer(R)
    I -> R  RotationChallenge(R)
    R -> I  RotationProof(R)
    I -> R  RotationCommit(R)       # after initiator-side CAS
    R -> I  RotationCommitAck(R)
    I -> R  RotationDone

Both roles:
    I -> R  RotationOffer(I)
    R -> I  RotationChallenge(I)
    I -> R  RotationProof(I)
    R -> I  RotationCommit(I)       # after responder-side CAS
    I -> R  RotationStart(R)
    R -> I  RotationOffer(R)
    I -> R  RotationChallenge(R)
    R -> I  RotationProof(R)
    I -> R  RotationCommit(R)       # after initiator-side CAS
    R -> I  RotationCommitAck(R)
    I -> R  RotationDone
```

After accepting `RotationDone`, the responder queues the encrypted EP
`KeyRotationEstablished` event first, then sets `Session.Authenticated` and
publishes local readiness. Even when no password-derived role exists, the
encrypted `RotationDone`/`KeyRotationEstablished` exchange is mandatory. The
initiator MUST NOT publish readiness before receiving that event.

Messages for another role, duplicates, reordered messages, unexpected
acknowledgements, or messages received after completion MUST fail the exchange.

## 8. EP transport binding and encryption requirement

PPAP is registered only under the exact protocol string
`ppap-mlkem768-v1`; implementations MUST NOT silently accept aliases.

The EP method codes used by this suite are:

| Logical EP method | Code |
|---|---:|
| `Handshake` action | `80` |
| `FinalHandshake` action | `81` |
| `KeyRotation` action | `82` |
| `ProceedToHandshake` acknowledgement | `44` |
| `Established` event | `C0` |
| `ErrorTerminate` event | `C1` |
| `ErrorMustEncrypt` event | `C2` |
| `KeyRotationEstablished` event | `C9` |

The `Initialize` method octet, when its required headers TDU is present, is:

```text
20 | (authenticationMode << 2) | encryptionMode
```

The encryption-mode codes are `00` none, `01` session key, and `02` session key
plus addresses. PPAP MUST use `01` or `02`; `00` MUST be rejected because PPAP
always requires the protected post-authentication phase.

`Initialize` and `ProceedToHandshake` carry the PPAP envelope as the byte-array
value of indexed `SessionHeaders.AuthenticationData` (index 15), using the
standard EP indexed-structure codec. `SessionHeaders.AuthenticationProtocol`
(index 14) is the exact PPAP protocol name. The domain header (index 1) carries
the connection's configured domain; the PPAP handler normalizes that value to
NFC for its context. ClientHello carries the normalized domain, and the
responder compares the two normalized values.

For subsequent messages, a non-null PPAP envelope is encoded as an EP RawData
TDU. Since a PPAP envelope is 10 through 8192 octets, its exact direct carriage
is one of:

```text
U8(logicalMethod | 20) || 48 || U8(L)  || ppapEnvelope   # 10 <= L <= 255
U8(logicalMethod | 20) || 50 || U16(L) || ppapEnvelope   # 256 <= L <= 8192
```

Here `20`, `48`, and `50` are hexadecimal octets and `U16` is unsigned
big-endian. Thus data-bearing Handshake, FinalHandshake, and KeyRotation method
octets are respectively `A0`, `A1`, and `A2`. A method without data is its lone
logical method octet; PPAP's final `KeyRotationEstablished` is therefore `C9`
before record protection.

The authentication messages through `InitiatorFinished` are sent before full
bidirectional transport protection. After verifying `InitiatorFinished`, the
responder enables encryption and sends `Established` as its first protected EP
message. The initiator must already accept protected inbound data; on receiving
that protected event it enables protected outbound data and begins rotation.

Every `KeyRotation` action, `RotationDone`, and `KeyRotationEstablished` event
MUST be encrypted. EP encrypted records are:

```text
U32(len(protectedPayload)) || protectedPayload
```

where `protectedPayload` is produced by the separately negotiated EP encryption
provider over the complete method/TDU plaintext. Its provider-specific format is
outside this PPAP suite. Receipt of rotation data while encryption is inactive
MUST terminate authentication. `Session.Authenticated` and application readiness
MUST remain false until rotation succeeds and readiness is published.

## 9. Interoperability and security invariants

A conforming implementation MUST fail closed on any of the following:

- wrong magic, wire version, expected message type, role, state, mode, or domain;
- a total, payload, field, key, ciphertext, mask, tag, proof, or Finished length
  outside the exact bounds above;
- a declared length mismatch, integer outside the accepted range, truncated
  input, or trailing input;
- an optional field whose presence does not exactly match the authentication
  mode;
- invalid UTF-8, identity kind, KDF profile, descriptor marker, descriptor
  combination, AEAD tag, or nonzero descriptor padding;
- an unresolved, ambiguous, or unexpected masked identity;
- a registration kind/profile/version/identity mismatch;
- a Finished, rotation proof, commit, acknowledgement, or CAS failure; or
- a rotation message outside active encrypted transport.

Proofs, Finished values, masked-identity candidates, and secret-derived material
comparisons MUST use constant-time equality. Implementations SHOULD erase
passwords, private keys, KEM secrets, Finished keys, derived AEAD material, and
pending rotation secrets as soon as their state no longer needs them. A handler
instance is single-connection state and MUST NOT be reused.

The clear authentication exchange carries only per-handshake masked identities;
it does not carry clear identity strings. A `RotationOffer` does contain its
normalized identity and descriptor, which is why rotation is forbidden outside
the encrypted EP transport. The deterministic descriptor nonce is safe only
under the one-descriptor-per-role-per-ephemeral-secret rule.

ML-KEM's implicit rejection behavior MUST remain indistinguishable at the
decapsulation API boundary. Only the transcript-bound Finished exchange or the
session-bound rotation proof establishes success. Expensive Argon2 work SHOULD
also be concurrency-limited so unauthenticated peers cannot exhaust local
memory.

### 9.1 Current local abuse controls (not wire protocol)

These controls do not change any PPAP octet and are not cross-language
interoperability requirements, but deployments need equivalent resource policy:

- The shared .NET password-derivation limiter allows
  `max(1, min(processorCount, 4))` concurrent Argon2 operations. When more than
  one processor is available, one slot is reserved for post-authentication
  rotation. A saturated limiter rejects a derivation instead of queueing it.
- The default Warehouse connection-admission policy permits 120 connection
  attempts per IP address in a one-minute window. Both values are
  configurable, and setting the attempt limit to zero disables it.
- `InMemoryPpapRegistrationStore.ResolveMasked` deliberately scans the entire
  store, evaluating candidates in the normalized domain, and is therefore
  `O(N)` in total registrations per masked lookup. It is intended for tests and
  small deployments. A large store needs a bounded or optimized lookup strategy
  that still avoids persistent/stable masked tokens, uses constant-time
  candidate comparison, and rejects ambiguity.

## 10. Implementation correspondence

The constants and encodings in this document are derived from these source
files:

- `Libraries/Esiur/Security/Authority/Providers/Ppap/PpapCryptography.cs`
- `Libraries/Esiur/Security/Authority/Providers/Ppap/PpapWire.cs`
- `Libraries/Esiur/Security/Authority/Providers/Ppap/PpapRotationWire.cs`
- `Libraries/Esiur/Security/Authority/Providers/Ppap/PpapModels.cs`
- `Libraries/Esiur/Security/Authority/Providers/Ppap/PpapAuthenticationHandler.cs`
- `Libraries/Esiur/Security/Authority/Providers/Ppap/PpapAuthenticationHandler.Rotation.cs`
- `Libraries/Esiur/Security/Authority/Providers/Ppap/PpapAuthenticationProvider.cs`
- `Libraries/Esiur/Security/Authority/Providers/Ppap/PpapPasswordDerivationLimiter.cs`
- `Libraries/Esiur/Security/Authority/AuthenticationMode.cs`
- `Libraries/Esiur/Security/Authority/Session.cs`
- `Libraries/Esiur/Security/Cryptography/EncryptionMode.cs`
- `Libraries/Esiur/Protocol/EpConnection.cs`
- `Libraries/Esiur/Protocol/EpConnectionProtocol.cs`
- `Libraries/Esiur/Protocol/EpServer.cs`
- `Libraries/Esiur/Net/Packets/EpAuthPacket.cs`
- `Libraries/Esiur/Net/Packets/EpAuthPacketMethod.cs`
- `Libraries/Esiur/Data/Tdu.cs`
- `Libraries/Esiur/Data/TduIdentifier.cs`
- `Libraries/Esiur/Resource/WarehouseConfiguration.cs`
