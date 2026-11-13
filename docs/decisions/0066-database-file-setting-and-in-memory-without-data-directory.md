# 0066: The database lies in the data directory unless `Censio:Database:File` names a file; without either it is kept in memory with a warning

- **Status:** Accepted
- **Date:** 2026-11-13
- **Area:** server-data
- **Source:** Issue #29
- **Supersedes:** —

## Context

The data directory ([0011](0011-data-protection-key-ring-in-the-data-directory.md)) is optional outside the image: a
plain `dotnet run` has none, and the ingest token then degrades with a warning
([0061](0061-ingest-token-generated-into-a-file-of-the-data-directory.md)). On a NAS, `/config` is often a network
share, where SQLite's locking is unreliable, and local mode (#39) wants the file under the local app data folder. The
issue asks for a configurable location.

## Options considered

1. **Only `<data directory>/censio.db`; no data directory stops the start** — one rule; breaks `dotnet run` and every
   test host that starts without a data directory.
2. **A file in the working or temporary folder without a data directory** — keeps data; writes private data to a place
   the operator did not choose.
3. **A setting of its own for the file; without it and without a data directory an in-memory database with a warning**
   (chosen) — the image needs no new setting, a NAS operator can keep the file on a local disk, and a start without any
   place to write writes nothing.

## Decision

The database is the file `Censio:Database:File` names (an absolute path), else `censio.db` in the data directory, else a
database in memory, announced by a warning and lost when the server stops. The rules are in
[`server-data.md`](../areas/server-data.md).

## Consequences

- A server started by mistake without a data directory loses what it stored; the warning is the only sign.
- The in-memory database has no write-ahead log, so every read and write takes the store's one gate in turn.
- The setting is one more entry of the configuration table; it never names a token or a secret, so its value is logged.
