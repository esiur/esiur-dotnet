# TypeDef cache synchronization and in-place proxy rebinding

Status: design for Esiur 3.2.0 (breaking protocol change). Applies to the .NET
and TypeScript implementations, which must stay wire compatible.

## Why

A client that keeps type definitions between connections (a long-lived
`Warehouse`, `AutoReconnect`, or a new connection created on the same
`Warehouse`) saves one `TypeDefById` round trip per type on every connect.
For a service with a few dozen types that is seconds on a high-latency link.

That cache is only safe if TypeDef ids stay valid. They do not: a node assigns
a TypeDef id from an incrementing counter when the type is first registered
(`Warehouse.RegisterLocalTypeDef`, or lazily on first use in TypeScript), so
the same class can receive a different id after the node restarts, and a
different class can reuse an old id. Today the .NET `Warehouse` keeps remote
definitions keyed by `(domain, remote id)` across reconnects, and a resource
attach resolves its type through that key, so after a restart a resource can be
bound to the wrong definition.

Therefore ids are never trusted across connections. A cached definition is
identified by its **full class name** and validated by a **signature**.

## Concepts

* **Full class name** - `TypeDef.Name` including the namespace, for example
  `Delta.Flow.Runtime.ControlPlane.FleetView`. Unique per node.
* **Id** - 64-bit, assigned by the serving node, valid for one process
  lifetime of that node only.
* **Version** - the existing `TypeDefInfo.Version` integer.
* **Signature** - 64-bit hash of the definition that does not depend on any id.
  Two definitions have the same signature exactly when every member, type
  descriptor, flag, annotation, documentation field and the version are equal.
* **Domain** - the identity of the serving node's type id space. Cached
  definitions are scoped to `(domain, full class name)`. One domain must not be
  served by nodes with different type id spaces on concurrent connections.

## Signature

`signature = LE64(first 8 bytes of SHA-256(canonical))`.

`canonical` is the normal wire encoding of the definition's `TypeDefInfo`
(`Codec.Compose`) with three substitutions, so that nothing depends on an id:

1. `TypeDefInfo.Id` is written as `0`.
2. `TypeDefInfo.Parent`, when present, is written as `name64(parent full name)`.
3. Every type descriptor that references a type definition (record, resource or
   enum types, at any nesting depth) is written as the fixed identifier
   `LocalType64` followed by `name64(referenced full class name)`, never as the
   narrowest id width.

`name64(n) = LE64(first 8 bytes of SHA-256(UTF-8(n)))`.

Because the signature is computed from the same encoding the node sends, any
field added to `TypeDefInfo` later is covered automatically. The signature is a
change detector, not a security feature. A node computes it only for its own
definitions; a client never recomputes it, it stores and echoes what the node
sent. Implementations therefore do not need to produce the same value for the
same type.

The signature is transmitted rather than derived by the receiver, on purpose. A
client that re-composes the definition it parsed does not reproduce the node's
canonical bytes: the node fills `TypeDefInfo.Namespace` from the CLR type while a
client folds it into the name (a .NET client matched only once `Namespace` was
left out), and a TypeScript client's re-composition of a metadata-rich definition
from a .NET node did not match in testing. A value derived that way would never
equal the node's, so every cached definition would be reported as changed on every
connect and the cache would never help. Deriving it would need a normative
canonical form implemented identically in every library, and every field added to
`TypeDefInfo` later would have to be added to that form or it would silently stop
invalidating caches. Transmitting costs about 10 bytes per definition and cannot
drift.

`TypeDefInfo` gains `Signature` (index 0x0A, UInt64). Every definition a node
sends (`TypeDefById`, `TypeDefByResourceId`, `LinkTypeDefs`, the sync reply)
carries it.

## Request: `SyncTypeDefs` (0xF)

Sent by the **client** during connection establishment, after authentication
has completed and before the connection is reported ready (before the first
resource attach, so reattach after a reconnect already sees correct types). It
is sent only when the client's cache holds at least one definition for the
remote domain.

Argument: a list of `TypeDefCacheEntry` (indexed structure):

| index | field       | type          |
|-------|-------------|---------------|
| 0     | `Id`        | UInt64        |
| 1     | `Name`      | string        |
| 2     | `Kind`      | `TypeDefKind` |
| 3     | `Version`   | Int32         |
| 4     | `Signature` | UInt64        |

`Id` is the id the client last used for this definition.

## Reply: `TypeDefSyncInfo` (indexed structure)

| index | field     | type                    | meaning |
|-------|-----------|-------------------------|---------|
| 0     | `Moved`   | list of `TypeDefIdMove` | signature unchanged, id changed |
| 1     | `Removed` | list of UInt64          | cached `Id`s that no longer exist or may not be viewed |
| 2     | `Updated` | list of `TypeDefUpdate` | definitions whose signature differs, complete and with their new ids |

`TypeDefIdMove`: `OldId` (0, UInt64), `NewId` (1, UInt64).

`TypeDefUpdate`: `OldId` (0, UInt64), `NewId` (1, UInt64), `Definition` (2, byte[]: the
complete definition exactly as `TypeDefById` returns it). The ids are explicit so the client can
re-key its cache before it parses any definition.

For each request entry the node looks up its own definition by `Name`
(`LocalTypeDef` or a dynamic definition it serves, never a definition it holds
as a client):

* none, or the requester is not allowed to view it (same check as
  `TypeDefById`): `Id` goes into `Removed`;
* signature equal and id equal: nothing is returned (unchanged);
* signature equal and id different: a `TypeDefIdMove`;
* signature different: a `TypeDefUpdate` with the full definition.

Definitions the client does not have are never returned; they are fetched later
with `TypeDefById` as today. Failure of the request (any error reply) is treated
by the client as "the whole cache is invalid": it discards the domain's cached
definitions and continues with lazy fetching.

## Client algorithm

1. Collect cached definitions for the remote domain. If none, skip.
2. Send `SyncTypeDefs`, wait for the reply.
3. In one critical section under the warehouse typedef lock:
   * `Removed`: remove from the cache, mark the definition object `Removed`
     (live resources keep it; see rebinding).
   * `Moved` and `Updated`: set the object's `Id` to the new id. Re-key the
     cache in two phases (remove all old keys, then insert all new keys) so a
     permutation of ids cannot collide.
   * Seed the connection's own `id -> definition` map with every surviving
     definition under its new id.
4. Parse each `Updated` definition **into the existing definition object**
   (object identity never changes), so every type descriptor and every resource
   already pointing at it sees the new members. Referenced definitions that are
   not cached are fetched on demand as usual.
5. Rebind the resources already attached to an updated definition (below) and
   flag the resources of a removed one.
6. Report the connection ready.

Type descriptors and resources hold references to definition objects, never
ids, so no other structure needs an id patch.

The client's resolution of a resource's type at attach time uses the
connection's own map (seeded by the sync, extended by fetches), not a
warehouse-wide id key.

## Rebinding in place

A definition update must be accepted even when compiled code (a generated proxy
class) was built against an older definition. The member **name** is the stable
identity; the member **index** is only valid for one definition.

Runtime (`EpResource`) on a definition change:

* values: new property array built by name; values of properties that still
  exist are kept, new properties start empty until the next attach/reattach
  delivers them, removed properties are dropped;
* listeners, event subscriptions and event cursors keyed by member index are
  re-keyed by name; a listener on a removed event is dropped and reported;
* the resource is reattached with a cursor no node generation matches, so the
  node answers with a full property snapshot (the existing `ReattachResource`
  request; no wire change) and the new properties get their values.

Generated proxies must not rely on member indexes. The index they were generated
with is only a fast path: when the resource's current definition still has that
name at that index nothing is looked up, otherwise the current index is resolved
by name, and a clear error is thrown when the member cannot be resolved:

* function removed: `EpMemberUnavailableException` (`EpMemberUnavailableError` in
  TypeScript) - "The function 'X' is no longer available on 'T'. The server
  definition changed; update the client code that uses it.";
* property removed: same error on get and set;
* event removed: subscribing throws; existing handlers are dropped.

Arguments are not validated client-side: a changed signature of a surviving
function is reported by the node as an invocation error.

## Binding compiled code

The member **name** is the identity; compiled code carries the index it was generated with only
as a fast path.

* .NET generated proxies (`TypeDefGenerator`): functions call
  `_Invoke(_FunctionIndex("Name", generatedIndex), args)` (and `_InvokeStream`), static functions
  call `connection.StaticCallByName(typeName, functionName, generatedIndex, args)`, properties
  read `_PropertyValue("Name", generatedIndex)` and write through `_PropertyIndex(...)`, and
  events are dispatched by `_EmitEventByName`. Values received from the wire are converted with
  `RuntimeCaster` (the wire carries numbers in the narrowest width that fits), not unboxed.
* TypeScript stub classes use the same helpers on `EpResource`: `_FunctionIndex`,
  `_PropertyIndex`, `_EventIndex`, `_PropertyValue` and the `_EmitEventByName` override point.
  Type-only stubs (what `esiur generate typescript` emits) already resolve members by name
  through the dynamic proxy.
* When a client attaches with a compiled contract (a template or a stub class), the node's
  current definition decides the layout and the contract is kept as `expectedTypeDef`: a
  renumbered member still binds by name, and a member the contract names that the node no longer
  serves fails with `EpMemberUnavailableException` / `EpMemberUnavailableError` (function call,
  property get/set, event subscription), naming the member and the type. The node's definition is
  fetched once per type per connection (or comes from the validated cache); if the node does not
  allow viewing it, the contract is used as before.

## Compatibility

* Only the **client** sends `SyncTypeDefs`, and only when its cache holds a definition for the
  domain, so a first connection never sends it.
* A node that predates the request ignores it. Connection establishment waits
  `EpConnection.TypeDefSyncTimeout` (.NET, default 15 s) / `EpConnection.typeDefSyncTimeoutMs`
  (TypeScript), then drops the cache and carries on with lazy fetching, instead of waiting for
  ever.
* Clients that predate the change are unaffected: `TypeDefInfo.Signature` is an index they do not
  know, and sparse structures ignore unknown indexes.
* `TypeDefIdsByNames` now always sends its names as one list argument. A node accepts a bare
  string as well, so a one-element list from an older client is answered (previously it was cast
  to `string[]`, failed and left the client waiting).

