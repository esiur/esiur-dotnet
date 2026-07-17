# CLI repository analysis

This report records the analysis performed before the cross-platform CLI foundation was implemented.

## Reusable Esiur.Net APIs

- `Warehouse.Get<EpConnection>(endpoint, EpConnectionContext)` parses `ep://` URLs, creates the protocol store, opens the connection, and performs authentication.
- `EpConnectionContext` selects authentication mode, provider protocol, identity, domain, reconnect behavior, and authentication timeout.
- `PasswordAuthenticationProvider` and its handler implement the current `password-sha3-v1` exchange. The provider obtains client credentials through `GetSelfCredential` or `GetSelfIdentityAndCredential`. The exchange does not expose a reusable login token.
- `EpConnection.Get(path)` resolves and attaches one stable resource path. `EpConnection.Query(path)` queries children, and `EpConnection.GetLinkDefinitions(path)` obtains reachable remote definitions.
- `EpResource` contains the peer's session-local resource ID and stable link, its attached property snapshot, and the remote definition installed on `Instance.Definition`.
- `TypeDef`, `PropertyDef`, `FunctionDef`, `EventDef`, `ConstantDef`, and `ArgumentDef` expose names, member indexes, inheritance, annotations, flags, and TRU type descriptors.
- `EpResource.TryGetPropertyValue(index, out value)` reads the attached property cache without generated stubs. Dynamic invocation and subscriptions are available through `_Invoke`, `_InvokeStream`, `Subscribe`, and `Unsubscribe` for later phases.
- `Warehouse.Close` and `EpConnection.Destroy` provide deterministic one-shot cleanup.

Esiur.Net currently represents type identity as an unsigned 64-bit ID, not `System.Guid`. The CLI renders this losslessly as a 16-digit hexadecimal value.

## Existing C# generators

Two generators have distinct roles:

- `Libraries/Esiur/Proxy/ResourceGenerator.cs` is the incremental compile-time source generator for locally attributed resources.
- `Libraries/Esiur/Proxy/TypeDefGenerator.cs` consumes remote definitions and emits C# proxies, records, enums, and registration code.

`TypeDefGenerator` is the later CLI generation reference. It emits `EpResource` subclasses, preserves member indexes, attaches remote instances using connection/instance ID/age/link constructors, maps TRUs including nullable and composite types, carries annotations and remote names, handles inheritance, and emits record, enum, static-call, event, and streaming shapes. It should be refactored behind the future `ICodeGenerator` abstraction rather than replaced.

## Relevant esiur-ts APIs

The TypeScript v3 runtime already provides:

- `EpConnection.get`, resource attachment, indexed `invoke` and `set`, remote TypeDef fetching, and property/event notification handling.
- `EpResource` with a property cache, type definition, dynamic proxy access, and property/event notification sources.
- `RemoteTypeDef` decoding with remote property, function, event, constant, flag, annotation, and TRU metadata.
- resource decorators and `TypeDef` templates, warehouse type registration, record classes, enum wrappers, and list/map/nullable TRU descriptors.
- `bigint`-capable integer codecs and typed wire descriptors needed to avoid mapping 64-bit integers to JavaScript `number`.

Generated TypeScript stubs still need a public generated-resource base or factory, stable static type metadata and UUID/ID registration, typed indexed property/function/event wrappers, an inheritance metadata convention, and a documented generated record/enum registration shape. Those changes belong to the TypeScript generation phase and were not made here.

## Browsing gaps and changes

The protocol already supports the first-phase operations, so the CLI does not parse EP packets. `AsyncReply` has no `CancellationToken` overload; the CLI adapts it to a task and disposes the connection when cancellation or timeout ends the operation. Native cancellation-aware library overloads remain a useful future addition.

Testing exposed one reusable browsing defect: `MemoryStore.Children` returned no children for a root store and returned all descendants for a non-root resource. It now returns true direct children, including at the store root, and honors the optional child name filter. This makes remote `query` traversal consistent and lets recursion remain an application-level policy.

The protocol query reply does not carry child counts, so the CLI obtains each displayed direct child count with an additional query. A richer batched browsing API may be worthwhile for high-latency servers.

## CLI structure

- `Configuration`: JSON configuration, named profiles, endpoint validation, duration parsing, and precedence resolution.
- `Authentication`: a replaceable credential service. The default implementation prompts or reads explicit standard input and persists nothing.
- `Client`: session creation/disposal and reusable resource inspection services.
- `Rendering`: table, JSON, JSON Lines, raw output, TRU formatting, and safe value normalization.
- `CliApplication`: parsing and dispatch only; protocol operations remain in services shared by future shell commands.
- `Tests/Esiur.CLI.Tests`: unit tests plus an in-process Esiur server integration test.

## Compatibility risks

- Password profiles require another prompt because the current authentication provider does not return a reusable token and the foundation intentionally adds no platform-specific secret-store dependency.
- Recursive queries require multiple network round trips; child counts add another query per result.
- Session IDs are peer-local and cannot be reused by another CLI process.
- Type identity is currently `ulong`; output consumers must not assume a GUID string.
- Trimming and NativeAOT remain disabled because dynamic resource attachment, reflection, and serialization have not been validated under those deployment modes.
