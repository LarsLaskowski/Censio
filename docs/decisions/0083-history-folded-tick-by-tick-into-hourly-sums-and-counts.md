# 0083: The history is folded tick by tick into hourly sums and counts; the latest hour and the tick identities are kept

- **Status:** Accepted
- **Date:** 2026-11-16
- **Area:** history
- **Source:** Issue #35
- **Supersedes:** —

## Context

Record [0021](0021-sqlite-history-with-retention.md) keeps ticks at full resolution for 7 days and hourly averages after
that, without limit, and leaves the rules to #35. A tick is stored as rows per island, entry and map pair with each
float as its 32-bit pattern, `NULL` for no value (record [0067](0067-ticks-stored-as-rows-with-float-bit-patterns.md)).
SQLite allows one writer, and the ingest acknowledges a tick only after it is stored within the Companion's 30 seconds
(record [0068](0068-ingest-acknowledges-a-message-only-after-it-is-stored.md)). A tick at the contract's bounds takes
about 3 seconds to store and 1 second to read ([`server-data.md`](../areas/server-data.md), *Measurements*), and the
number of ticks in one hour is not bounded, since the receive time is the Companion's stamp. A replay keeps the
recorded receive times, so ticks can arrive for an hour that was reduced long ago, and an acknowledgement can be lost
after a tick was stored, so the same tick can arrive again.

## Options considered

1. **One statement per profile-hour that aggregates and deletes its ticks** — short to write; holds the write lock for
   an unbounded number of ticks, and a late tick of that hour cannot join the average.
2. **Store the hourly means** — the smallest rows; a late tick cannot be added exactly, since a mean over the ticks that
   carried each value needs the number of those ticks.
3. **Store per value the sum and the number of ticks that carried it, folding one tick per transaction** (chosen) — a
   late tick is folded like any other, the lock is held for one tick at a time, and every mean is computed on read.
4. **Keep every tick and thin the series on read** — no reduction at all; the file keeps growing (55 MB a day).

For the hour of a profile's latest tick: **reduce it like any other** (the pages, which show the latest tick, would show
nothing for a profile not played for a week) or **keep it at full resolution** (chosen). For a tick that was folded:
**forget its identity** (a resent tick would count twice) or **keep it in its own table** (chosen, about 108 bytes a tick with
its unique index, measured in [history.md](../areas/history.md)). For two entries of one good on one island: **sum them** (wrong for productivity), **keep both by position** (positions are
not stable across ticks) or **use the first in stored position** (chosen).

## Decision

The retention job folds a tick older than 7 days into its profile's hourly point in one write transaction per tick,
oldest first, storing sums and counts per value and keeping the folded tick's identity; the hour of a profile's latest
tick is never folded. The rules are in [`history.md`](../areas/history.md).

## Consequences

- A float sum is a double, so a late tick changes it within the rounding of another order of additions; counts and
  integer sums stay exact.
- An hour can hold an hourly point and ticks at once (a fold in progress, a late tick); a read returns both, never a
  tick beside the point that holds it.
- A step at the bounds must stay below 10 seconds; the measured figure stands in the area document. If it does not, a
  tick is folded in smaller steps, which this record would have to describe.
- The kept identities grow by one row per folded tick without end; the measured bytes per tick stand in the area
  document.
- A negative zero becomes a sum of zero: an hourly mean does not keep the bit pattern that a tick keeps.
