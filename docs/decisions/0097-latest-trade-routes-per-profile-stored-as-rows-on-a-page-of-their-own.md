# 0097: The latest trade routes of a profile are stored as rows and shown on a page of their own

- **Status:** Proposed
- **Date:** 2026-11-20
- **Area:** web-pages
- **Source:** Issue #225
- **Supersedes:** —

## Context

The game sends the whole set of trade routes about every two minutes (fixture `protocol-3-trade-routes.jsonl`; the
11 frames of its original were byte-identical). The player wants to see which routes have problems or lost ships
now, not how they changed. The server keeps a profile's state across a restart and holds at most two profiles in
memory ([server-state](../areas/server-state.md), record
[0069](0069-current-state-of-the-newest-profiles-held-in-memory-and-restored-at-start.md)); ticks are stored as rows
(record [0067](0067-ticks-stored-as-rows-with-float-bit-patterns.md)) and a deletion must leave no name in the
database files ([history](../areas/history.md), *Deleting a profile*).

## Options considered

1. **The latest set per profile as rows, replaced whole, shown on its own page** (chosen) — restored at start like the
   rest of the state, read on demand for a profile that is not held, deleted with the profile. Con: a migration and
   three tables.
2. **The contract message's JSON as one value per profile** — one table and the reader's checks on reading back, but
   the stored data then depends on the contract version, so version 3 would need a conversion of stored messages.
3. **In memory only** — no schema change, but a restart shows nothing until the next message, and a profile that is
   not held has no routes at all.
4. **Every message as history** — would allow charts nobody asked for, and at one set every two minutes it grows the
   database faster than the ticks' hourly folding allows.
5. **A column on the island pages instead of a page** — a route spans islands of several regions, so no island page
   can show it whole.

## Decision

The server keeps only the latest trade-routes message of each profile, as rows replaced in one write transaction, and
shows it on the page `Trade routes`; the rules are in [web-pages](../areas/web-pages.md), [server-state](../areas/server-state.md)
and [server-data](../areas/server-data.md).

## Consequences

- An older message (by receive time) replaces nothing; a resend of the stored one is acknowledged without a write.
- The page's as-of time is the receive time of the trade-routes message, not of the latest tick.
- Pages other than the trade-routes page ignore the trade-routes notice, so the two-minute messages cause no
  re-read of their trends.
- The meanings of the problem and lost-ship counts rest on the reference reader's names, confirmed once on the
  Product Manager's game (spec AC9).
