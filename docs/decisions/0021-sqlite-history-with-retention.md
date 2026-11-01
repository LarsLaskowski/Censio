# 0021: SQLite from the start; full resolution for 7 days, then hourly averages without limit; deletion per profile

- **Status:** Accepted
- **Date:** 2026-11-01
- **Area:** —
- **Source:** Issue #8; Issue #29 (SQLite), Issue #35 (time series and retention)
- **Supersedes:** —

## Context

The server keeps the live state and a history of every island and good, one point per tick. A long game produces
many ticks, the server runs on a NAS or the gaming PC ([0015](0015-two-deliverables-and-local-mode.md)), and the
operator should not have to run a database server. The data directory (`Censio:DataDirectory`,
[0011](0011-data-protection-key-ring-in-the-data-directory.md)) is the one place that survives a container
recreation.

## Options considered

1. **In memory first, a database later** — fastest start; the history is lost on every restart, and a later
   migration of the storage model.
2. **A database server (PostgreSQL) or a time-series database** — scales and aggregates well; a second container or
   service the operator must run.
3. **SQLite in the data directory from the first stored byte** — one file, nothing to operate, backed up with the
   directory.

For the retention: **keep everything at full resolution** (grows without bound), **delete after a fixed time** (the
long view is lost), or **full resolution for 7 days, then hourly averages kept without limit** — chosen: recent
detail, the long-term trend, and a size that grows slowly.

## Decision

Option 3 with the third retention rule. The server stores its data in SQLite in its data directory (#29); ticks keep
full resolution for 7 days and are then reduced to hourly averages that are kept without limit, and the history of
one profile can be deleted on its own (#35). The exact rules go into the area document #35 introduces.

## Consequences

- Data older than 7 days loses its per-tick detail; short peaks inside an hour are no longer visible.
- Gaps without data stay gaps, never zeros, also after the reduction (#35, [0031](0031-bounded-local-tick-buffer-gaps-accepted.md)).
- SQLite allows one writer at a time; with one Companion per server
  ([0020](0020-one-companion-per-server-with-savegame-profiles.md)) that is enough.
