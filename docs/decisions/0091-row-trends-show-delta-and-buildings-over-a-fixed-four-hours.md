# 0091: Row trends show delta and buildings over a fixed four hours, at every point

- **Status:** Proposed
- **Date:** 2026-11-18
- **Area:** web-charts
- **Source:** Issue #38
- **Supersedes:** —

## Context

Issue #38 asks for a sparkline and a trend arrow (up, down, stable) per row of the goods table and the island list,
"based on a defined window". The charts already offer four ranges kept in the browser
([0089](0089-chart-ranges-wide-ones-as-hourly-means-read-at-request.md)), and a table has up to 500 rows. Ticks arrive
about every 120 seconds. The Product Manager chose the values and the window.

## Options considered

1. **Delta and buildings over a fixed 24 hours as hourly means** (the Lead's first proposal) — at most 25 points a row;
   the Product Manager found 24 hours too long.
2. **The page's stored chart choice** — the sparklines follow the range picker; the island list has no picker, and `All`
   would read a profile's whole history per table on every tick.
3. **Delta and buildings over a fixed 4 hours at every point** (chosen by the Product Manager, resolution by the Lead) —
   about 120 to 150 points a row at the game's rate; the newest point is the latest tick, so a row's line ends at the value
   it shows.
4. **4 hours as hourly or 15-minute means** — 4 to 5 hourly points carry no trend; 15-minute means need a read resolution
   the history does not have.
5. **Production instead of delta, goods in deficit instead of buildings** — rejected by the Product Manager.

## Decision

A goods row shows the good's delta and an island row the island's buildings, at every point of the fixed window
`Last 4 hours` up to the latest tick, at most the newest 240 points, independent of the range picker and the time axis;
the arrow compares the rounded oldest and newest valued points. The rules are in
[`web-charts.md`](../areas/web-charts.md), *Sparklines and trend arrows*.

## Consequences

The window never changes with the user's choice, so the tables cost the same read on every page. A replay faster than
one tick per 75 seconds fills the 240 points before 4 hours; the line and arrow then cover the newest 240, while the
heading still names 4 hours (stated in README and user guide). Other values or a chosen window would need a new
decision.
