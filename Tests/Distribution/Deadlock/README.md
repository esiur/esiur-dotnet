# Distributed deadlock test (two nodes / WAN)

Two console apps that evaluate the recursive-attachment deadlock-prevention algorithm over a real
TCP connection between two machines:

- **Server** (`Server/`) hosts a configurable graph of `Node` resources whose references may form
  cycles, and prints the *cycle census* of the deployed graph (so the experiment can state that
  circular dependencies were actually generated).
- **Client** (`Client/`) connects, fetches the graph concurrently, and classifies each run as
  **Completed / Deadlocked / Slow** using a *stall detector* — a deadlock is detected as the absence
  of attachment progress while requests are still pending, which distinguishes it from slow WAN
  processing rather than relying on a blunt timeout.

Authentication is disabled (`AllowUnauthorizedAccess`, anonymous `None` mode), so no credentials are
needed.

## Build

```
dotnet build Tests/Distribution/Deadlock/Server/Esiur.Tests.Deadlock.Server.csproj -c Release
dotnet build Tests/Distribution/Deadlock/Client/Esiur.Tests.Deadlock.Client.csproj -c Release
```

## Run

**On node A (server):**
```
dotnet run --project Tests/Distribution/Deadlock/Server -c Release -- \
    --port 10950 --topology ring --nodes 8
```
It prints, e.g.: `topology=ring nodes=8 edges=8 cyclic=True backEdges=1` and the node count to pass
to the client. Leave it running (Ctrl+C to stop).

Topologies (`--topology`):

| name | cyclic | description |
|------|:------:|-------------|
| `ring` | yes | `i → (i+1) mod n`; every node fetched as an independent request (cross-chain cycles) |
| `cycle` | yes | single-root cycle `0→1→…→n-1→0` (fetch only `--roots 0`) |
| `complete` | yes | every ordered pair `i → j` |
| `staggered` | no | two roots share a deep dependency reached at different depths (stresses non-cyclic contention; `--nodes` is derived) |
| `random` | usually | Erdős–Rényi directed graph (`--nodes`, `--seed`, `--edge-prob`) |
| `chain` | no | acyclic control `0→1→…→n-1` |
| `diamond` | no | acyclic control |

**On node B (client):**
```
dotnet run --project Tests/Distribution/Deadlock/Client -c Release -- \
    --host <NODE_A_IP> --port 10950 --nodes 8 \
    --mode WaitWithCycleDetection --iterations 20 --stall-ms 5000 --hard-ms 60000
```

Modes (`--mode`):
- `WaitWithCycleDetection` (default, the production algorithm) — completes; breaks only genuine cycles.
- `NaiveWait` (control) — no cycle handling; **deadlocks** on any cyclic graph (detected via the stall window).
- `LegacyCrossChainPlaceholder` — for reference only.

Other client options: `--roots all|0,1,2` (which nodes to fetch; default all `n0..n{N-1}`),
`--stall-ms` (no-progress window ⇒ deadlock; set comfortably above your WAN round-trip × graph depth),
`--hard-ms` (progress-but-unfinished ⇒ slow).

## Output

The client prints per-iteration rows and a summary, and writes `deadlock_<mode>_<host>_<port>.csv`:

```
iteration,outcome,ms,cycle_breaks,unnecessary_placeholders,unpublished
```

- `outcome` — `Completed` / `Deadlocked` / `SlowTimeout`.
- `ms` — fetch time (deadlocked rows equal the stall window).
- `cycle_breaks` — placeholders returned to break a cycle on this connection.
- `unnecessary_placeholders` — placeholders returned where no genuine cycle existed (always 0 for the
  production resolver; non-zero only for the legacy reference mode).
- `unpublished` — resources delivered to the application whose dependency graph was not fully attached
  at delivery (`-1` for a deadlocked/failed run).

## Suggested WAN runs for the paper

1. **Detection works and cycles exist.** Server `--topology ring --nodes 8`; client
   `--mode WaitWithCycleDetection` (expect all *Completed*, `cycle_breaks > 0`) and then
   `--mode NaiveWait` (expect *Deadlocked* — validates the detector on the same cyclic graph).
2. **Random pool census.** Server `--topology random --nodes 12 --seed 20260603`; the server prints
   whether the deployed graph is cyclic; run the client in `WaitWithCycleDetection`.
3. **Threshold justification.** Compare the client's reported completion `ms` (median/p99) against
   `--stall-ms`; the stall window should be orders of magnitude larger.
