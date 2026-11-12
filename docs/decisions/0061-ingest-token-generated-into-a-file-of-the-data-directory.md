# 0061: The ingest token is generated into a file of the data directory and compared by its hash

- **Status:** Accepted
- **Date:** 2026-11-12
- **Area:** ingest
- **Source:** Issue #28
- **Supersedes:** —

## Context

Record [0019](0019-websocket-transport-with-shared-token.md) requires a shared token, enabled by default, compared in
constant time and never logged, and leaves its lifecycle to #28. The issue asks for a token "generated on first start and
shown once in the logs/settings". The web UI needs no login ([0006](0006-server-plain-http-and-open-health.md)), the image
keeps its data in `/config`, and outside the image the data directory may be unset (`dotnet run`). `SECURITY.md` asked
for a token set through an environment variable.

## Options considered

1. **Log the generated token once** — easiest to find; against 0019, and container logs are collected and shared.
2. **Show it on a settings page** — convenient; anyone who reaches the unauthenticated web UI reads it.
3. **A token the operator sets by configuration** — no file; a hand-chosen token is often weak, an environment value is
   visible in `docker inspect`; can be added later without changing this one.
4. **A fresh token in memory per start** — no file; the Companion has to be reconfigured after every restart.
5. **A generated token in an owner-only file of the data directory, rotated by deleting the file** (chosen) — survives
   restarts with `/config`, the operator reads it with one command, the log names only the file.

Without a data directory: **stop the start** (breaks the documented start without configuration) or **start and refuse
every Companion** (chosen, fails closed with a warning).

## Decision

Option 5. With the token required, the server reads `ingest-token` from the data directory or generates it there at
start; without a data directory it refuses every Companion. It keeps only the SHA-256 of the token and compares the
SHA-256 of a presented token with a fixed-time comparison, so neither the position of a difference nor the length
shows in the time. Rules and texts are in [`ingest.md`](../areas/ingest.md).

## Consequences

- The operator needs access to the data directory (`docker exec`, the volume) to read or rotate the token.
- `SECURITY.md` no longer asks for an environment variable; a configured token would be a new decision.
- Local mode (#39) sets a data directory and reads the same file, or switches the token off.
