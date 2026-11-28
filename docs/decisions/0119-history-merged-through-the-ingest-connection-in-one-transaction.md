# 0119: A profile's history is merged into another through the ingest connection, in one write transaction

- **Status:** Proposed
- **Date:** 2026-11-28
- **Area:** history-deletion
- **Source:** Issue #262
- **Supersedes:** —

## Context

A merge in the Companion (record [0118](0118-profile-merged-in-the-companion-one-ordered-queue-with-the-deletions.md))
needs the server to move one profile's ticks, hourly points, islands and trade routes into another. A deletion reaches
the server as `profileDeletion` through `/ingest` with the token (record
[0116](0116-history-deleted-through-the-ingest-connection-with-the-token.md)) and hides its rows behind a tombstone that
the retention job removes in steps (record [0084](0084-profile-deleted-in-steps-and-cleared-from-the-file.md)). Contract
version 3 is not released (latest tag `v0.4.0`).

## Options considered

1. **A message `profileMerge` in contract version 3, answered like a deletion** (chosen) — the token guards it, the
   Companion's queue already waits for an acknowledgement; no version for both sides.
2. **Contract version 4** — needed only for a released version 3.
3. **One write transaction that relabels the source's rows and combines the hourly points of hours both profiles hold**
   (chosen) — readers see the merge whole or not at all, the rows are moved set-wise and nothing is held in memory; the
   write gate is held for its duration, bounded by a measurement.
4. **A marker like the tombstone and steps of the retention job** — a short first transaction, but until the steps end
   the target's hours that both profiles held miss the source's part, and readers would need the marker.
5. **A merge on the pages** — record 0118's option 2.

## Decision

Options 1 and 3: `profileMerge` names the merged profile and its target; the server moves the merged profile's history
into the target in one write transaction under the state's write gate, keeps the target's identity and name, and reads
its profile list and held states again as after a deletion. The rules are in
[history-deletion](../areas/history-deletion.md), [server-state](../areas/server-state.md) and
[ingest](../areas/ingest.md).

## Consequences

- A token holder can merge any stored profile into any identity, as one can delete any (accepted as in record 0116).
- A merge keeps the history, so no clean-up of the database file follows it; the rows it removes are overwritten by
  secure delete only.
- Ingest writes wait for a merge; its duration grows with the hours both profiles hold. Measured: at most 3 s up to
  about 450 shared hours, which covers the histories one Companion writes (it sends one profile at a time, so two
  profiles share only the hours of a switch); two profiles sharing a whole year take about 55 s. Accepted rather than
  option 4: that size needs two profiles fed in the same hours all year, and beyond the Companion's 30 s wait the merge
  still ends whole, the waiting messages are sent again and stored once, and the resent merge is acknowledged without a
  change. Option 4 would leave the shared hours incomplete for readers on every merge to spare a case that hardly occurs.
  The figures are in the area document.
