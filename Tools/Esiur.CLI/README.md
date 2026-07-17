# Esiur CLI

`esiur` is the cross-platform command-line client for Esiur.Net. This first implementation supports profiles, authentication, resource browsing, schema inspection, and property reads. It runs directly on .NET and does not require Node.js, PowerShell, Bash, or Dart.

## Build and install

The project targets .NET 10:

```console
dotnet build Tools/Esiur.CLI/Esiur.CLI.csproj
dotnet pack Tools/Esiur.CLI/Esiur.CLI.csproj -c Release
dotnet tool install --global --add-source Tools/Esiur.CLI/nupkg Esiur.Cli
```

The project is configured for self-contained, single-file, non-trimmed publishing. Platform release archives and installers are planned for the packaging phase.

## Login and profiles

Create and verify a password-authenticated profile:

```console
esiur login production ep://host --provider password --identity ahmed
```

The password prompt does not echo input. For automation, supply it on standard input:

```console
printf '%s' "$ESIUR_PASSWORD" | esiur login production ep://host \
  --provider password --identity ahmed --password-stdin
```

Esiur.Net's password provider does not currently issue reusable tokens. The CLI therefore stores endpoint and identity metadata, but never the plaintext password. A later command using that profile prompts again when authentication is required.

Manage profiles with:

```console
esiur profile list
esiur profile show production
esiur profile use production
esiur profile remove production
esiur logout production
```

`logout` removes credential material through the credential abstraction and retains the profile. With the default prompt-only implementation there is no persisted credential to remove.

Configuration is stored at `%APPDATA%\Esiur\config.json` on Windows and `${XDG_CONFIG_HOME:-~/.config}/esiur/config.json` on Linux.

## Browse resources

List direct children or recurse through a resource tree:

```console
esiur query sys
esiur query sys --depth 2
esiur query sys --recursive
esiur query sys --type Service --output json
```

Results include the stable path, session-local ID, type name, type ID, and direct child count. Numeric IDs are informational in one-shot commands and are not stable between processes.

## Describe resources

Read the remote `TypeDef` schema:

```console
esiur describe sys/service
esiur describe sys/service --values
esiur describe sys/service --output json
```

Descriptions include resource identity and age, inheritance, annotations, properties, functions, events, and constants. `--values` includes the values attached with the resource; `--schema-only` explicitly suppresses them.

## Read properties

Properties can be selected by name or numeric member index:

```console
esiur get sys/service Name
esiur get sys/service Name Running Status
esiur get sys/service 0 --output raw
```

## Connection overrides and automation

Every operational command accepts a saved profile or a temporary endpoint:

```console
esiur --profile production describe sys/service
esiur --endpoint ep://host query sys --output json
esiur get sys/service Name --timeout 30s
```

Configuration precedence is explicit option, environment variable, selected profile, global configuration, then built-in default. Supported variables are `ESIUR_PROFILE`, `ESIUR_ENDPOINT`, `ESIUR_PROVIDER`, `ESIUR_IDENTITY`, `ESIUR_OUTPUT`, and `ESIUR_TIMEOUT`. There is intentionally no long-lived password environment variable.

Output formats are `table`, `json`, `jsonl`, and `raw`. Command results go to standard output; errors and password prompts go to standard error. JSON output contains no status text.

## Exit codes

| Code | Meaning |
|---:|---|
| 0 | Success |
| 1 | General failure |
| 2 | Invalid command or arguments |
| 3 | Authentication failed |
| 4 | Connection failed |
| 5 | Resource not found |
| 6 | Member not found |
| 7 | Access denied |
| 8 | Invalid value |
| 9 | Invocation failed |
| 10 | Timeout |
| 11 | Cancelled |

## Security and troubleshooting

- Passwords are read using a non-echoing prompt or explicit standard input and are not serialized into configuration.
- Endpoint user-info such as `ep://user:password@host` is rejected.
- Diagnostics redact values labelled as passwords or tokens. Use `--debug` for exception details; never paste debug output without reviewing it.
- If a connection fails, verify the `ep://` host and port, the server's allowed authentication provider, identity, and domain.
- `query` requires attach permission for the parent and returned children. `describe --values` requires attaching the target resource.

Interactive shell, mutation, invocation, subscriptions, code generation, and release packaging are intentionally deferred to their focused implementation phases.
