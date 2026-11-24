# 0109: A stored icon tells a notice of its own, which re-renders the pages without reading their trends

- **Status:** Proposed
- **Date:** 2026-11-24
- **Area:** web-pages
- **Source:** Issue #247
- **Supersedes:** —

## Context

A Companion uploading its catalog to a fresh server sends one `iconImage` per missing icon, hundreds for a real game
([`live-sending.md`](../areas/live-sending.md), *Catalog*). Each stored icon told the same `Catalog` notice as a stored
catalog, and every open data page answered it with its full load: the island page read its overview chart again, the
good page both charts, and each new read gave its chart a new element, so the reveal animation ran once per icon
([`web-charts.md`](../areas/web-charts.md), *Animation*). The pages flickered until the upload ended. An icon changes
only which rows show an icon; it changes no figure, no name and no region.

## Options considered

1. **A notice kind of its own for a stored icon, on which a page rebuilds its view and reads none of its trends**
   (chosen) — the page knows exactly what changed; the trade-routes notice already set the pattern
   ([record 0097](0097-latest-trade-routes-per-profile-stored-as-rows-on-a-page-of-their-own.md)).
2. **The server merges icon notices** (one at the catalog's store, one at the end, at most one per interval) — fewer
   reads, but each merged notice still reads the charts and runs the reveal; adds a timer to the state and a choice of
   interval, and the last icon's notice depends on knowing that the upload ended.
3. **The chart keeps its element when its figures did not change** — still one trend read per icon and page under the
   trend gate, only the redraw is saved; comparing reads needs an equality over every point.
4. **A page reads its charts only when the latest tick moved** — also skips the read after a late tick of the page's
   profile (stored, but not the latest tick), which the ranges read whole show at once today
   ([`web-charts.md`](../areas/web-charts.md), *Live updates*).

## Decision

Option 1: a stored icon tells the followers the notice kind `Icons`, without a profile; a data page answers it by
reading and rebuilding its view as for any notice, but reads no chart and no row trends unless the view it then shows
has another latest tick than the one it showed, or it showed none. A stored catalog keeps the `Catalog` notice and the
full load. The rules are in [`web-pages.md`](../areas/web-pages.md), *Live updates*, and
[`server-state.md`](../areas/server-state.md), *Followers and notices*.

## Consequences

- An upload of hundreds of icons costs each open page one view rebuild per icon, merged while a read runs, and no trend
  read; a page whose profile is not held still reads its state from the database once per such rebuild.
- A notice that arrives while a view-only read runs still reads the trends afterwards when it would on its own.
- A future follower that cares about icons must handle the new kind; one that switches on `Catalog` alone no longer
  sees icons.
