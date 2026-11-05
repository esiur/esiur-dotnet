# Network cache interceptor

Status: design proposal, not implemented. Intended as a non-breaking, opt-in feature of the release
after 3.2 for the .NET and TypeScript libraries. The Dart library follows once it reaches protocol
parity (see "Compatibility").

Names below are written in TypeScript style (`camelCase`); the .NET library uses the same names in
`PascalCase`.

## Why

An Esiur client pays the network at every step of using a service: a link lookup
(`GetResourceIdByLink`), a definition (`TypeDefById`), an attach, then every call. On a link with a
100 ms round trip a fresh page spends seconds on this before it can show anything. An application that
fetches an already attached service by link before each call (`get(link)`, then the call) pays a
link lookup every time, so each call costs two round trips instead of one. And without a connection a
client can show nothing at all, not even what it showed a minute ago.

HTTP answered this with caches, but Esiur is not HTTP, and that is an advantage here:

* an attached resource is live: the server pushes its property changes;
* reattaching with a cursor returns only what changed since the cursor (or everything when the
  resource's history restarted);
* definitions carry a signature, and `SyncTypeDefs` validates a client's copies in one request.

A cache built on those can treat a stale answer as a starting point that the connection then
corrects, instead of a copy nobody ever checks.

The design is an interceptor layer on the connection that, where the application allows it, answers
from a cache store. The store is an adapter (memory, IndexedDB, SQLite, Hive), so the same behavior
runs in a browser, in a service and on a phone.

## Goals and non-goals

Goals:

* Nothing changes unless an application configures it. With no policy configured the library behaves
  exactly as it does today.
* A cached answer always says what it is: live, fresh, stale or offline. It is never silently
  presented as current.
* A configured cache can save round trips online (link resolution, definitions, repeated reads) and
  can serve the last known state offline.
* The cache never widens access: it is partitioned by identity, bounded in age, and emptied when
  access is revoked or the user signs out.
* The same entry format, keys and semantics in every language; only the store adapters differ.

Non-goals:

* Offline writes. A write fails while offline. A queue of pending writes (an outbox) needs its own
  design (idempotency, ordering, conflicts) and is left for later.
* A replacement for authorization. The server stays the authority; the cache only ever holds what
  the server already sent to this identity.
* Caching of streams, events or authentication traffic.

## What Esiur already provides

| Need | Existing piece |
| --- | --- |
| Fetch only what changed | `ReattachResource` takes a link and a cursor `(generation, revision)`; the reply carries only the properties modified after the cursor, or all of them (`reset`) when the generation differs. `EpResource` keeps `cursor`, `reattachCursor` and `needsFullReattach`. |
| Detect "a different resource behind the same link" | The cursor's `generation` identifies the history of one resource; the attach reply carries the resource's link and type id; the definition carries a signature. |
| Reuse definitions safely | `Warehouse` keeps remote definitions by domain and full class name; `SyncTypeDefs` validates them by signature when a connection is established (docs/typedef-sync.md). |
| Type-faithful storage | The wire codec encodes every Esiur value, including records, enums and resource references. |
| Which calls may be cached | Members can declare `OperationEffects`, and types and members carry a free-form `annotations` map that the wire already transports. |
| Persist remote resources locally | A remote resource is an `IResource`; it can be `put` into a warehouse and a store (the relay test does this). |

What is missing, and part of the work:

* **A formal interception seam.** Today the only way in is replacing `sendRequest`, which the tests do.
* **`EpResource` without a live connection.** A resource restored from the store has no connection
  until it is reattached, and today `EpResource` uses its connection in about twenty places (invoke,
  subscribe, detach, reattach, ...), each of which needs a defined behavior when there is none.
* **Resource notifications.** `ResourceDestroyed`, `ResourceReassigned` and `ResourceMoved` exist in
  the protocol, but only `ResourceDestroyed` is ever sent (by the .NET server), the .NET client acts
  only on that one (the other two are empty handlers), and the TypeScript library neither sends nor
  handles any of the three. Anything that wants to trust a remembered link needs these specified and
  implemented first.
* **Dart.** The Dart library is at 2.2.2 and (by a search for the relevant names) has none of the
  3.x pieces above: no cursors, definition signatures, effects or journals. It is a separate port.

A caution on `OperationEffects`: its default is `None` in both libraries, which is also the value of
every member that declares nothing. `None` is therefore **not** evidence that a function is
read-only, and the design never infers cacheability from it. It only uses a declared side effect
(anything other than `None`) as a reason to refuse caching.

## Concepts

**Entry kinds.**

| Kind | What is stored | Persisted |
| --- | --- | --- |
| `Definition` | The definition payload exactly as the node sent it, with its domain, full class name, version and signature. | yes |
| `Resource` | A mirror of an attached resource: link, type, cursor and the last known value of each property. | yes |
| `Call` | The reply of one function call for one argument list. | yes |
| `Link` | The id a link resolved to on this connection (a "pin"). | never: ids are valid for one server process only |

**Freshness.** Everything the cache returns carries one of four states.

| State | Meaning |
| --- | --- |
| `live` | Attached on a connected connection; property changes arrive as they happen. |
| `fresh` | Served from the cache, within the policy's `ttl` of the last time the server confirmed it; a background revalidation may still be running. |
| `stale` | Served from the cache, past `ttl` but within `maxStale`; a revalidation is running or pending. |
| `offline` | Served from the cache because there is no connection. |

**Identity of a cached resource.** An entry is only ever reused for the same *server, domain,
identity, tenant, link, type name, type signature and generation*. A link is a name, not an
identity: it can point at a different resource for a different user (`sys/me`), after a move, after
the resource was recreated (new generation) or after a redeploy (new type or signature). Each case
is caught by one of those parts and the entry is dropped instead of merged.

**Scope.** The partition an entry lives in: `server | domain | identity | tenant` (the first three
always, the tenant when the policy says so). The default is identity. Scopes are never shared.

## Interception seam

The seam is the connection's **operations**, not its raw requests, because the useful decisions span
several requests (resolving a link and attaching is one decision). A connection runs an ordered chain
of interceptors; each one may answer an operation, change it, observe its result, or let it through.

```ts
interface EpInterceptor {
  /** Everything `connection.get(link, target)` does: resolve the link, attach, return the resource. */
  get?(context: GetContext, next: () => Promise<EpResource>): Promise<EpResource>;
  /** One function call on a resource (or a static call). */
  invoke?(context: InvokeContext, next: () => Promise<unknown>): Promise<unknown>;
  /** A definition by id or full class name. */
  definition?(context: DefinitionContext, next: () => Promise<RemoteTypeDef>): Promise<RemoteTypeDef>;

  /** The server changed a property of an attached resource. */
  propertyChanged?(resource: EpResource, property: string, value: unknown, age: bigint, date: Date): void;
  connected?(connection: EpConnection): void;
  disconnected?(connection: EpConnection, reason: DisconnectReason): void;
}
```

A context carries what a policy needs to decide: the connection and its state, the scope, the link,
the full type name, the function or property name, the arguments, and the matching policy. The cache
is one interceptor (`CacheInterceptor`); applications can add others (metrics, retries) in the same
chain. With no interceptor installed, `next()` is called directly and nothing changes.

Where it attaches in the code: in TypeScript the operations are `EpConnection.get`, `EpResource.invoke`
and `EpConnection.fetchTypeDef`; in .NET they are `EpConnection.Get`, `EpResource._Invoke` and
`EpConnection.FetchTypeDef`. Generated proxies and the dynamic proxy already reach the connection
through those, so they need no change.

Configuration lives on the warehouse (`warehouse.cache`), which owns the policies and the store and
installs a `CacheInterceptor` on every connection it creates.

## Policies

A policy says what may be cached, how, and for how long. There is no default policy: a request that
matches none is passed through untouched.

```ts
interface CachePolicy {
  /** First match wins; list `deny` policies first. */
  match: { link?: Glob; type?: Glob; function?: Glob };
  /** What the policy covers. */
  kind: "link" | "resource" | "call" | "definition" | "deny";

  mode: "networkOnly" | "networkFirst" | "cacheFirst" | "staleWhileRevalidate" | "cacheOnly";
  ttl?: Duration;        // how long an entry counts as fresh
  maxStale?: Duration;   // how old an entry may be and still be served stale or offline; default: ttl
  networkTimeout?: Duration; // networkFirst: how long to wait before falling back

  scope?: "identity" | "tenant" | "server"; // default identity
  tags?: string[];       // labels for invalidation
  invalidates?: string[]; // tags purged after a successful call (for write functions)
  trustServerHints?: boolean; // honor server `cache.*` annotations other than `cache.deny`
  maxEntryBytes?: number;
}
```

Matching uses the normalized link (no leading slash, no trailing slash, case as the server sent it),
the full type class name, and the function name as declared. Globs support `*` and `**` only.

Server hints are optional annotations on a type or member: `cache.deny=true` (never cache this; always
honored), and `cache.ttl`, `cache.scope`, `cache.tags` (honored only when the policy sets
`trustServerHints`). They ride in the existing annotations map and need no protocol change.

## Behavior

### Link pins

`kind: "link"` with `mode: "cacheFirst"` pins a link for the life of **one connection**. After the
first resolution, `get(link)` returns the attached resource without a lookup while all of these hold:

1. the resource is still attached to this connection;
2. its `link` still equals the requested link;
3. no `ResourceDestroyed`, `ResourceReassigned` or `ResourceMoved` notification names its id (once
   those are sent and handled; see "Compatibility");
4. the pin's `maxStale` (default: the connection's life) has not passed.

Otherwise the link is resolved again. A pin saves one round trip per repeated `get`. It is never
persisted, and it must not be enabled for links whose target depends on the caller (for example a
`sys/me`): the policy is opt-in per link pattern for exactly that reason. Until point 3 can be
checked, a pinned resource that the node destroyed is found out by the failure of the next call, which
ends the pin.

### Definitions

`kind: "definition"` persists the definition payloads a connection receives. At start-up they are
loaded into the warehouse's remote-definition cache, so the **first** connection already has them;
`SyncTypeDefs` validates them with the server by signature as it does for any cache, and the
attach path (docs/typedef-sync.md, "Client algorithm") finds them by the type id the attach reply
names. A page load therefore skips the definition downloads, and nothing stale can be shown because
a definition that changed is replaced before any resource uses it. This is safe by construction and
needs no `ttl`.

### Resources

`kind: "resource"` keeps a mirror of each attached resource that the policy matches: on every
successful attach or reattach, and on every `propertyChanged`, the link, type name, type signature,
cursor and property values are written (debounced) to the store. Properties are stored by name, with
their age and date, so a renumbered definition does not break a mirror.

`get(link)` then behaves by mode:

| Mode | Online | Offline |
| --- | --- | --- |
| `networkOnly` | as today | fails |
| `networkFirst` | network; on failure or after `networkTimeout`, the mirror (`offline` if there is no connection, else `stale`) when within `maxStale` | the mirror when within `maxStale`, else fails |
| `cacheFirst` | the mirror if within `ttl` (`fresh`), reattached in the background; otherwise as `networkFirst` | as `networkFirst` |
| `staleWhileRevalidate` | the mirror at once (`fresh` or `stale`), reattached in the background | the mirror (`offline`) when within `maxStale`, else fails |
| `cacheOnly` | the mirror or fails; never the network | the same |

"Reattached in the background" is the existing `ReattachResource` with the mirror's cursor, preceded
(once per connection) by `SyncTypeDefs`. When it completes, the resource is updated in place: changed
properties fire `propertyModified` as usual and the state becomes `live`. When the server answers
`reset`, or the type name or signature differs, the mirror is dropped and the resource is rebuilt
from a full attach; consumers see one state change and the new values.

A mirror restored without a connection is an ordinary `EpResource` in a *detached* state: property
reads return the stored values, `cacheInfo` says `offline`, event subscriptions are remembered and
sent at the next attach, and function calls go through the call policy.

References to other resources are stored as `(link, type name)` and mirrored lazily. If the referenced
mirror is not in the store, the value is `undefined` and the owner is marked `partial`. How much of a
graph to snapshot is an open question below.

### Function calls

Calls are **never cached unless a policy says so**. A matching `kind: "call"` policy is the
application's assertion that the function is a read; the library cannot know. The library refuses
regardless of policy when the definition declares an effect other than `None` (`ModifiesState`,
`CreatesResource`, `DeletesResource`, `External`, `EmitsEvents`) or the server set `cache.deny`.
Calls that return streams or resource references are not cached.

An entry's key is `scope | link | function | sha256(arguments)`. The arguments are encoded with the
normal codec and one normalization: map entries sorted by their encoded keys, so equal arguments
always give equal keys. The entry also records a fingerprint of the function's definition, so a
changed function is never served an old reply.

Modes for calls:

| Mode | Behavior |
| --- | --- |
| `networkFirst` | The network's reply, stored; if the call fails or there is no connection, the last reply when within `maxStale`. |
| `cacheFirst` | The stored reply when within `ttl`; otherwise the network's. |
| `staleWhileRevalidate` | The stored reply at once, a refresh in the background; `cache.onRevalidated` tells the consumer a newer reply arrived (a promise can carry only one value). |
| `cacheOnly` | The stored reply or `EpCacheMissError`. |

A write function's policy lists `invalidates` tags; after a successful call every entry carrying one of
those tags is purged. Reads declare `tags` to be reachable.

## Offline and reconnect

The interceptor reads the connection state from the connection itself and from `connected` and
`disconnected`.

* While disconnected, operations covered by a policy are served from the store as the tables above
  say; everything else fails with `EpOfflineError`.
* On connect: definitions are validated (`SyncTypeDefs`), then every `stale` or `offline` mirror is
  reattached with its cursor. A definition the node reports changed or removed purges the mirrors and
  call entries of that type.
* **Fail closed on denial.** If a revalidation is answered with an authorization failure
  (`AttachDenied`, `InvokeDenied`, `AccessDenied` or `NotAllowed`), the entries it covers are purged and the denial is
  surfaced; the cache never keeps showing what the server now refuses.
* `maxStale` is the security bound for offline use: past it an entry is unusable and deleted.

## Entries and keys

An entry is an indexed structure encoded with the Esiur codec, so it can grow by adding indexes the way
`TypeDefInfo` did.

| Index | Field | Notes |
| --- | --- | --- |
| 0 | `schema` | `1` |
| 1 | `kind` | `Definition`, `Resource`, `Call` |
| 2 | `key` | below |
| 3 | `storedAt` | UTC milliseconds |
| 4 | `expiresAt` | when it stops being `fresh`; 0 for definitions |
| 5 | `staleUntil` | when it stops being usable |
| 6 | `tags` | list of strings |
| 7 | `meta` | kind-specific map (cursor, type name and signature, function fingerprint, link) |
| 8 | `payload` | the wire-encoded values |

Keys: `esiur-cache/1 | server | domain | identity | [tenant] | kind | subject`, where `server` is the
endpoint's authority and `subject` is the normalized link, or `link#function#argumentsHash` for a
call, or `domain/className` for a definition. Reads verify that the entry's recorded scope equals the
requester's before returning it.

## Stores

```ts
interface CacheStore {
  get(key: string): Promise<CacheEntry | undefined>;
  put(entry: CacheEntry): Promise<void>;
  delete(key: string): Promise<void>;
  deleteByTag(scope: string, tag: string): Promise<number>;
  deleteScope(scope: string): Promise<number>; // sign-out, revocation
  keys(scope: string, kind?: CacheEntryKind): AsyncIterable<string>;
  size(): Promise<number>; // approximate bytes
}
```

Adapters: memory (reference implementation and tests), IndexedDB (browser), SQLite (.NET and mobile), and
a Hive or Isar adapter for Dart. Every adapter must make a write atomic, treat a corrupt or
unreadable entry as a miss (never an error), enforce a size budget with least-recently-used eviction
(definitions last), and carry a schema version. In a browser, writes from several tabs are reconciled
by revision (higher wins) and announced to other tabs (`BroadcastChannel`).

A language-independent conformance suite (key construction vectors, entry encoding vectors, and a
behavioral test every adapter must pass) is part of the deliverable.

## Security and privacy

* **Opt-in.** No policy, no persistence.
* **Encryption at rest.** An optional `EntryCipher` (AES-GCM; the tag also detects tampering) with a key
  from a platform key provider: a non-extractable WebCrypto key in a browser, the OS keystore on a
  phone or server. Deleting the key erases the cache.
* **Partitioning.** Entries are scoped by identity (and tenant when configured) and verified on read.
  Signing out deletes the scope.
* **Never cached:** anything from `Token` or authentication, and anything the server marks
  `cache.deny`.
* **Age bound.** `maxStale` caps how long revoked access can still be seen offline; applications pick
  it per policy, and it should be short for sensitive data.
* **Origin of entries.** An entry is created only from an authenticated reply on a connection of the
  same scope, never from application input.

## Invalidation and consistency

* A live resource writes every received property change through to its mirror, so the mirror is the last
  state the server sent plus the cursor.
* Call entries leave by `ttl`, by `invalidates` tags, and by definition change.
* A definition change (`Updated` or `Removed` in the sync reply, or a changed signature on a fresh
  attach) purges every mirror and call of that type.
* Optional, later: a server `CachePurge` notification (new notification kind) and a cache epoch in the
  authentication headers, so a server can order a client to forget a scope.

## Showing staleness

```ts
resource.cacheInfo: {
  state: "live" | "fresh" | "stale" | "offline";
  source: "network" | "store";
  syncedAt: Date;
  expiresAt?: Date;
  partial?: boolean;
}
connection.cache.onState(resource, handler);

// A call with its provenance; a plain call returns the value only.
const { value, state, storedAt } = await connection.cache.call(resource, "readReport", args);
```

The React hook gains the same data: `useResource(link, { cache }) -> { resource, cacheInfo }`. Errors:
`EpOfflineError` (no connection and no usable entry), `EpCacheMissError` (`cacheOnly` and nothing
stored), `EpStaleExpiredError` (an entry past `maxStale`).

Applications decide what a stale value looks like. A live map should not show positions that old as
current; a report list can say "as of 10:42". That is a product decision per screen, which is why the
policy is explicit.

## Compatibility

* **No wire change** for definitions, mirrors, pins and call results: they use the existing requests
  (`GetResourceIdByLink`, `AttachResource`, `ReattachResource`, `SyncTypeDefs`, `TypeDefById`,
  `InvokeFunction`) and annotations. Old nodes work (they just offer no hints).
* **Needs finishing before it can be relied on:** handling of `ResourceDestroyed` (TypeScript), and
  sending and handling of `ResourceReassigned` and `ResourceMoved` (both), which first need their meaning
  specified (`Reassigned` is documented in a comment as carrying an old and a new id; what `Moved`
  carries and when a node would send it is not written down). Until then a link pin ends when the
  resource is found missing, not when the node announces it.
* **Needs a change to `EpResource`:** it must be able to exist, and be read, without a live connection.
* **Dart** needs the cursor/reattach, definition-signature and effects parts of the 3.x protocol
  ported before it can use anything but the memory store.
* Optional protocol additions (`CachePurge`, cache epoch) are separate proposals.

## Phasing

| Phase | Content | Exit criterion |
| --- | --- | --- |
| 0 | This spec reviewed; the interception seam; policy model; memory store | Existing tests unchanged with no policy; policy tests pass |
| 1 | Persistent definitions (IndexedDB for the web client, SQLite for .NET) and per-connection link pins | A warm page load sends no `TypeDefById`; a pinned link sends no repeated lookup |
| 2 | Persistent resource mirrors, offline `get`, reattach with cursor, fail-closed purge | Cold start with no network shows the last state with `offline`; reconnect converges to `live` |
| 3 | Call cache, invalidation tags, staleness API, React hook | Stale-while-revalidate and write invalidation behave as the tables say |
| 4 | SQLite and Hive adapters, encryption at rest, multi-tab coordination, the Dart port | Conformance suite passes on every adapter |
| 5 | `CachePurge` notification, outbox for offline writes | Separate proposals |

Phase 1 is the safest first slice: it holds nothing that can be stale, because a definition is validated
before use, and it removes the definition downloads from every page load.

## Open questions

* Which identity names the server: the URL authority, or a server-chosen id sent at authentication?
* How much of a resource graph a mirror snapshots (references to depth N, or on demand), and how large
  a single property may be before it is chunked or compressed (Delta's fleet snapshot is one large
  property).
* Whether `OperationEffects` should gain an explicit read-only value, or its default become `Unknown`,
  so a server can declare a read and a client can rely on it. Either is a breaking change to the
  definition.
* The meaning of `ResourceMoved` and `ResourceReassigned`, and whether a moved resource keeps its
  mirror.
* Whether a resource's generation survives a node restart. If a restart always starts a new
  generation, a mirror saved before it always gets a full (`reset`) answer afterwards: correct, but
  without the saving of a delta.
* Whether a pin should be allowed to span a reconnect (re-resolved lazily) or always end with the
  connection.
* How a promise-returning call reports that a newer reply arrived after a stale one (event, async
  iterator, or a second call).

## Testing

* The store conformance suite above, run against every adapter.
* A deterministic offline harness: a fake clock and a transport that can be cut and restored, used to
  assert the tables in "Behavior" cell by cell.
* Restart scenarios: cold start offline, cold start online, node restart with renumbered types,
  generation change, signature change, revoked access during revalidation.
* Property tests for key canonicalization (equal arguments, equal keys) and for scope isolation (no
  entry is ever returned across identities).
* With no policy configured, the existing suites run unchanged.

## Appendix: an example configuration (non-normative)

How a Delta client might use it; every line is an application decision, not a library default.

```ts
warehouse.cache.configure({
  store: new IndexedDbCacheStore("delta-flow"),
  policies: [
    { kind: "deny", match: { function: "*Token*" } },
    { kind: "definition", match: { type: "**" }, mode: "cacheFirst" },
    // The service links are the same for every user and stable for a connection.
    { kind: "link", match: { link: "sys/*" }, mode: "cacheFirst" },
    // Directory screens: show the last list at once, refresh behind it.
    { kind: "resource", match: { link: "sys/list-display" }, mode: "staleWhileRevalidate", ttl: "5m", maxStale: "1h" },
    // Reports are reads; a slow report can be shown from the last run while offline.
    { kind: "call", match: { link: "sys/reports", function: "Get*ForClient" }, mode: "networkFirst", maxStale: "10m", tags: ["reports"] },
    { kind: "call", match: { link: "sys/reports", function: "Upsert*ForClient" }, mode: "networkOnly", invalidates: ["reports"] },
    // The live map and alerts stay on the network: stale positions must not look current.
    { kind: "resource", match: { link: "fleet/**" }, mode: "networkOnly" },
  ],
});
```

Fleet today hides its data when a connection drops ("a disconnect may be a permission revocation").
Showing cached data there is a change of product behavior, so each screen would opt in separately.
