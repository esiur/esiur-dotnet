# Release notes

## 3.1.0

- Add generation/revision cursors, resource journals, historical subscriptions,
  and reconnect replay for live properties and events.
- Improve dynamic resource/type definition handling and concurrent attachment
  behavior, with cross-runtime conformance coverage.
- Negotiate and enforce remote parser, allocation, collection, metadata-depth,
  and encrypted-record limits.
- Add opt-in EP connection diagnostics for traffic, queues, and resource state.
- Standardize the default EP port at 51018 when no explicit port is supplied.
- Update the ASP.NET Core adapter and add journal support to EntityCore.
- Release the adapters, stores, and CLI as 3.1.0 with an Esiur 3.1.0 dependency;
  previously published 3.0.x packages are not overwritten.
- Correct the standalone example's endpoint display and exclude nested/stale
  build outputs when building with an external artifacts directory.

### Compatibility and publishing

Use matching 3.1 runtimes when using journals, replay, or negotiated limits.
Keep explicit endpoint ports where an installation does not use port 51018.
Existing .NET target frameworks are unchanged.

Publish `Esiur.3.1.0.nupkg` first, then `Esiur.AspNetCore`,
`Esiur.Stores.EntityCore`, `Esiur.Stores.MongoDB`, and `Esiur.CLI` 3.1.0.
The core security/cryptography project and test/example projects are not
standalone NuGet products.
