# 0092: Row trends are read once per table, in parts of 240 island points

- **Status:** Proposed
- **Date:** 2026-11-18
- **Area:** web-trends
- **Source:** Issue #38
- **Supersedes:** —

## Context

The trend reads of the charts serve one good or one island per read and scan at most 240 ticks or hourly points per
transaction under the trend gate ([0087](0087-chart-reads-aggregate-per-point-bounded-in-parts-with-a-product-index.md));
on the in-memory database each transaction also holds the ingest. The sparklines of #38 need up to 500 goods of one island
or every island of a region ([0091](0091-row-trends-show-delta-and-buildings-over-a-fixed-four-hours.md)), read again on every
tick. The history stores no island total: an island's buildings are the sum over its entries.

## Options considered

1. **One existing read per row** — no new read; 500 serialized reads per goods table and tick, each with its times
   transaction, holding the trend gate for minutes at the bounds.
2. **One read per table with parts of 240 ticks** — two transactions for the goods table; for the island list a part
   scans every entry of every island of 240 ticks, about 30 s at the contract's bounds (256 islands of 512 entries).
3. **One read per table with parts of 240 island points** (chosen) — the goods table is one island, so one part; the
   island list takes one part per 240 island points, each scanning what a part of the overview chart scans.
4. **An island building total stored at ingest** — cheap island reads; a schema change with a backfill of the stored
   history, a second value the fold and the deletion keep in step, and a security-area change of the ingest.

For a new tick: **read the whole window again** — about 160 parts for a region of 256 islands every 120 s per open page;
or **read only the points after the held newest one** (chosen), merged as the wide chart ranges do
([0089](0089-chart-ranges-wide-ones-as-hourly-means-read-at-request.md)), a part or two per tick. Sharing one read across
the pages of a region was not taken: a cache with its own lifetime and invalidation is a design of its own.

## Decision

Two new trend reads, one for the deltas of a set of goods on one island and one for the buildings of a set of islands,
each read the newest 240 points' times in one transaction and the values in parts of at most 240 island points under the
trend gate; a refresh reads only the points after the held newest one, and a page never waits for them before showing its
figures. The rules and measurements are in [`web-trends.md`](../areas/web-trends.md), *The batch reads of the trend column*.

## Consequences

A transaction holds the trend gate and, in memory, the ingest no longer than a part of the overview chart's read. The
island list's opening read grows with the region's islands (about 13 s warm and up to 20 s cold at the bounds, each
part 150 ms at most, measured in `web-trends.md`) and runs in the charts' read gate, so a chart read can wait behind it; its refresh is a part or two. A tick folded inside the
held window shows as held until the next whole read. Option 4 is the way out if that proves too slow.
