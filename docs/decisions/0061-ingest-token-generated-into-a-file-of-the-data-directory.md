# 0061: The ingest token is generated into a file of the data directory or read from a configured file, and compared by its hash

- **Status:** Accepted
- **Date:** 2026-11-12
- **Area:** ingest
- **Source:** Issue #28; Issue #176 (token from a configured file)
- **Supersedes:** —

## Context

Record [0019](0019-websocket-transport-with-shared-token.md) requires a shared token, enabled by default, compared in
constant time and never logged, and leaves its lifecycle to #28. The issue asks for a token "generated on first start and
shown once in the logs/settings". The web UI needs no login ([0006](0006-server-plain-http-and-open-health.md)), the image
keeps its data in `/config`, and outside the image the data directory may be unset (`dotnet run`). `SECURITY.md` asked
for a token set through an environment variable. #176 adds operators who keep secrets in Docker, compose or swarm
secrets or an orchestrator's store, which hand a secret to a container as a file at a path of their own
(`/run/secrets/<name>`); a hand-chosen token is often short.

## Options considered

1. **Log the generated token once** — easiest to find; against 0019, and container logs are collected and shared.
2. **Show it on a settings page** — convenient; anyone who reaches the unauthenticated web UI reads it.
3. **A token value set by configuration** (`Censio:Ingest:Token`) — no file; the value shows in `docker inspect`, the
   process environment and a NAS UI's container settings, and the secret stores of #176 deliver files anyway.
4. **A fresh token in memory per start** — no file; the Companion has to be reconfigured after every restart.
5. **A generated token in an owner-only file of the data directory, rotated by deleting the file** (chosen) — survives
   restarts with `/config`, the operator reads it with one command, the log names only the file.
6. **In addition, a setting that names a file holding the token** (chosen) — reaches the secret where the store puts
   it; the server only reads it, so its access rules stay the operator's.

Without a data directory: **stop the start** (breaks the documented start without configuration) or **start and refuse
every Companion** (chosen, fails closed with a warning).

When both the configured file and `ingest-token` exist: **the setting wins** (chosen; one place for the secret) or
**stop the start** (forces the operator to delete a file the setting already makes irrelevant). With
`Censio:Ingest:RequireToken` `false` and the setting: **stop the start** (chosen) or **ignore the file** (runs open
although a token was configured).

Minimum length: **none** (a one-character token passes), **32 characters for the configured file only** (a short token
still passes by writing `ingest-token` by hand), or **32 characters for every token** (chosen; the generated 43 pass).
Length cannot prove strength, but it bounds the worst hand-chosen tokens.

## Decision

Options 5 and 6. With the token required and the setting `Censio:Ingest:TokenFile` set, the server reads the token
from that file only, never creates it and ignores `ingest-token`; the configured path and value are never logged,
since an operator may have put the token itself there. Otherwise it reads `ingest-token` from the data directory or
generates it there at start; without a data directory it refuses every Companion. Every token has 32 to 1,024 visible
ASCII characters. The server keeps only the SHA-256 of the token and compares the SHA-256 of a presented token with a
fixed-time comparison, so neither the position of a difference nor the length shows in the time. Rules and texts are in
[`ingest.md`](../areas/ingest.md).

## Consequences

- The operator needs access to the data directory (`docker exec`, the volume) to read or rotate a generated token, or to
  the configured file and a restart to change a configured one.
- `SECURITY.md` names the file setting, not an environment variable that holds the token.
- The documented procedure for the setting covers Linux Docker Engine only; other hosts keep the generated file.
- Local mode (#39) sets a data directory and reads the same file, or switches the token off.
