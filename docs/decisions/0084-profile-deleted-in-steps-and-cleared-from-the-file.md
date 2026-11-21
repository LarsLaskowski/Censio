# 0084: A profile is deleted at once for its readers, its rows are removed in steps, and the file is cleared of them

- **Status:** Accepted
- **Date:** 2026-11-16
- **Area:** history-deletion
- **Source:** Issue #35
- **Supersedes:** —

## Context

Record [0021](0021-sqlite-history-with-retention.md) lets the history of one profile be deleted; the issue asks that it
is removed completely. A profile holds up to 7 days of ticks at full resolution (about 55 MB a day) and every hourly
point since it began, millions of rows, while the ingest must store a tick within the Companion's 30 seconds behind
the one write lock. Profile and island names are private ([`server-data.md`](../areas/server-data.md));
SQLite leaves deleted content in free pages and in the write-ahead log unless told otherwise. The Companion may still be
sending under the profile that is deleted.

## Options considered

1. **One transaction that deletes every row of the profile** — simple and atomic; holds the write lock for minutes on a
   large profile, so the ingest misses its 30 seconds and the Companion reconnects again and again.
2. **Remove the profile and its islands in one short transaction and give its ticks and hourly points a fresh tombstone
   identity as their profile, then remove the tombstoned rows in steps of their own** (chosen) — the profile is gone for every reader at once, the lock
   is held for one step at a time, and a step left undone after a stop is resumed by the next run.
3. **Keep the profile and its name and delete only its history** — the profile would stay in the switch with nothing to
   show; the issue asks for complete removal.

For the residue: **accept it** (the names stay readable in the file), **`VACUUM` after each deletion** (rewrites the
whole file, needs as much free space again and blocks every reader and writer meanwhile) or **SQLite's secure delete on
the server's writing connection plus a truncating checkpoint after the steps** (chosen; it overwrites what this version
deletes, not what an older version left in free space).

## Decision

Option 2 with secure delete and a truncating checkpoint: the rows of a deleted profile move to a tombstone no profile
carries, so no read sees them, and are removed one tick or hourly point per transaction, and the write-ahead log is truncated once they are gone; a checkpoint
blocked by a reader is retried by the next run. The rules are in [`history-deletion.md`](../areas/history-deletion.md).

## Consequences

- Secure delete writes zeros over everything deleted, retention included, which costs write time; the measured step
  time stands in the area document.
- A tick of the deleted profile that arrives during the steps creates the profile again, with rows under its own
  identity that the steps leave alone. Row numbers would not do as a mark: SQLite reuses the highest number of a deleted
  row.
- The first transaction rewrites one row per tick and hour of the profile; its measured time stands in the area
  document.
- Copies of names that a version before this one left in the file's free space stay until the operator runs `VACUUM`
  with the server stopped (user guide).
- A resent tick of the deleted profile that arrives during the steps is reported as already stored and then removed:
  it belonged to the deleted history.
