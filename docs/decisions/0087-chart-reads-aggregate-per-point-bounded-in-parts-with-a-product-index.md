# 0087: Chart reads aggregate per point, read the newest 2,880 points in parts, and use an index of tick entries by good

- **Status:** Accepted
- **Date:** 2026-11-16
- **Area:** history
- **Source:** Issue #36
- **Supersedes:** —

## Context

The charts of #36 need a good summed over a region's islands and an island's key figures per point, besides one good
on one island. The series read of [`history.md`](../areas/history.md) returns one good on one island with every value
and amount, and is unbounded in time. A tick at the contract's bounds holds 256 islands of 512 entries, and an entry of
a tick is found by its good only by scanning its island's entries (the tick entries are indexed by island and
position); the hourly entries are indexed by island and good. On the in-memory database every read takes the write gate
([`server-data.md`](../areas/server-data.md)), which the ingest waits for within the Companion's 30 seconds
([0068](0068-ingest-acknowledges-a-message-only-after-it-is-stored.md)). The pages can be opened by anyone, and an open
chart page reads again on every tick. After a deletion, the clean-up truncates the write-ahead log once per retention
run, and a reader open on the database file blocks that truncation
([0084](0084-profile-deleted-in-steps-and-cleared-from-the-file.md)).

## Options considered

1. **Call the series read per island and sum on the server** — no new read; one read per island of a region, each with
   the amounts the chart does not need, and a scan of all entries of every tick for the good.
2. **Two aggregate reads (a good over a set of islands, an island's figures), the newest points only, without a new
   index** — bounded in points; a region at the bounds scans 131,072 entries per tick, about 377 million rows for 2,880
   ticks.
3. **The same reads with an index of tick entries by island, good and position** (chosen) — a region at the bounds
   needs 256 index lookups per tick; each stored entry costs the index's bytes and its write.
4. **Keep pre-computed region and island series at ingest** — cheap reads; a second store of the history that the
   fold and the deletion must keep in step.

For the clean-up: **let it retry an hour later when a chart read blocks it**, as for any reader — chart pages re-read
on every tick, so any visitor could keep a deleted profile's names in the `-wal` file indefinitely
([0021](0021-sqlite-history-with-retention.md)); **a gate of the chart reads that the clean-up takes** (chosen).

## Decision

Option 3. The history offers two reads that return one aggregated point per tick or hourly point: a good summed over a
set of islands, and the key figures of one island. Each reads at most the newest 2,880 points of its range and their
total count, in parts of at most 240 points with a read transaction each, and a migration adds an index of the tick
entries by island, good and position. Each part's transaction runs under a trend gate of the database that the
clean-up takes before the write gate and holds through the truncation. The rules are in [`history.md`](../areas/history.md) and
[`web-charts.md`](../areas/web-charts.md).

## Consequences

- The in-memory database's write gate is held for one part at a time, so the ingest waits at most for a part.
- The clean-up waits at most for one part, and no chart read holds the deleted rows in the write-ahead log.
- A fold that runs between two parts of one read can miss a tick in that drawing (a gap point), never show it twice,
  only for a profile whose range reaches back 7 days (idle); the next read draws it right.
- The 10 seconds of a read hold where the file's pages are in the page cache. Uncached, the region read at the bounds
  (256 islands) takes about 70 seconds, the fixture's region of 24 islands about 1 second. Accepted: that chart shows
  `Loading…` meanwhile, the pages' reads are not held, and the ingest and the clean-up wait at most for one part. Bounding
  the region read further (a lower cap for the region, or option 4) costs more than a first read of a region far
  beyond any played game.
- Every stored tick entry writes the index as well, and the migration builds it over the stored ticks at the first
  start; both costs are measured in [`history.md`](../areas/history.md) (*Measurements*).
- The series read stays as it is, for later charts of other values (#49 to #51).
