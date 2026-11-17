# 0089: Charts offer four ranges kept in the browser, and the wide ones show hourly means computed when read

- **Status:** Accepted
- **Date:** 2026-11-17
- **Area:** web-charts
- **Source:** Issue #37
- **Supersedes:** —

## Context

The charts showed only the day up to a profile's latest tick ([0088](0088-charts-show-the-day-up-to-the-latest-tick-with-gaps-after-15-minutes.md)).
Issue #37 asks for a range picker (an hour, a day, a week, all) whose choice applies to every chart of the page and is
remembered. A chart holds at most the newest 2,880 points ([0087](0087-chart-reads-aggregate-per-point-bounded-in-parts-with-a-product-index.md)),
measured at about 53 MB for a page of two charts; ticks arrive about every two minutes and stay unfolded for 7 days
([0021](0021-sqlite-history-with-retention.md)), so a week holds about 5,040 ticks. An open chart page reads again on
every tick, and the chart reads share one trend gate. The choices of the frame live in cookies written by a script
([0071](0071-web-ui-choices-kept-in-browser-cookies-written-by-a-script.md)).

## Options considered

1. **Every range at tick resolution under the cap** — no new read; a week shows its newest four days with the cap line,
   and `All` only the newest 2,880 points.
2. **A higher cap for the wide ranges** — the whole week at tick resolution; memory and render batch grow with it (3.2 MiB
   per chart at 2,880 points).
3. **Hourly means for `Last 7 days` and `All`, computed from ticks and hourly points when read, in C#** (chosen) — at
   most 169 points for a week, 2,880 hours for `All`; a read scans every tick of the week.
4. **Hourly means kept at ingest** — cheap reads; a second store of the history that the fold and the deletion must keep in
   step (rejected in 0087 too).

For a new tick on a wide range: **read the whole range again**, as the narrow ranges do — a region read of a week at
the contract's bounds every two minutes; **read only the newest hour and keep the older ones** (chosen). For the choice:
**one cookie per choice** (chosen, as 0071), **one cookie holding both** (a compound value to parse), **browser
storage** (rejected in 0071).

## Decision

Option 3. The charts offer `Last hour`, `Last 24 hours` (the default, 0088's range), `Last 7 days` and `All`; the two wide
ranges show one mean per UTC hour over the hour's folded and unfolded ticks, read in parts of at most 240 scanned ticks or
hourly points, and a new tick reads only the newest hour of a wide range again. Range and axis are kept in one cookie
each, as 0071 keeps the theme. The rules are in [`web-charts.md`](../areas/web-charts.md),
[`history.md`](../areas/history.md) and [`web-ui.md`](../areas/web-ui.md).

## Consequences

- A week of a region at the contract's bounds with a cold page cache may take about two minutes to read, during which
  the other chart reads wait in the trend gate; measured in [`web-charts.md`](../areas/web-charts.md).
- A tick that arrives late for an older hour of a wide range shows only at the next whole read (page opened, choice made).
- The narrow ranges keep their tick resolution and the cap line.
