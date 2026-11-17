# Spec: Range picker and real-time / game-time switch

Source: Issue #37
Status: Draft

## Problem

The charts of #36 ([`web-charts.md`](../../docs/areas/web-charts.md)) always show the 24 hours up to a profile's latest
tick on a real-time axis ([record 0088](../../docs/decisions/0088-charts-show-the-day-up-to-the-latest-tick-with-gaps-after-15-minutes.md)).
A player cannot look at the last hour, a week or the whole savegame, and every pause shows as a gap: the game time stops
while the game is paused ([`protocol.md`](../../docs/protocol.md), *Timestamp*, assumed), so clock time is not time played.

## Scenarios

- S1: Given the island's page or a good's page, when the user chooses `Last hour`, `Last 24 hours`, `Last 7 days` or `All`
  in the range picker, then every chart of the page shows that period up to the profile's latest tick, and the range line
  names it.
- S2: Given `Last 7 days` or `All`, when the charts are drawn, then each point is the mean of one UTC hour and its tooltip
  says `Hourly average` / `Stundenmittel`; `Last hour` and `Last 24 hours` show every tick, as today.
- S3: Given any range, when the user switches the time axis from `Real time` to `Game time`, then every chart of the page
  places its points at their game time, the tooltip adds the point's game time, and a pause of the game leaves no gap;
  switching back restores the real-time axis.
- S4: Given a choice made on one chart page, when the user opens another chart page, reloads, or comes back the next day in
  the same browser, then the charts open with that range and axis.
- S5: Given an open page with a chosen range and axis, when a new tick of the profile is stored, then the charts follow it
  with the same choice and the period moves with the latest tick; on `Last 7 days` and `All` only the newest hour changes.

## Errors and edge cases

- E1: A browser without a stored choice, or with a stored value the server does not know, opens the charts with
  `Last 24 hours` on `Real time`, as *Stored choices* of [`web-ui.md`](../../docs/areas/web-ui.md) treats the theme.
- E2: A browser that keeps no cookies, or a choice the browser fails to store: the open page keeps the new choice, the
  next page opens with the defaults; nothing is shown to the user.
- E3: The user switches again while the charts are still being read: the charts end up showing the last choice, never an
  earlier one; each chart shows `Loading…` / `Wird geladen…` until its points for the shown choice are there.
- E4: A read fails after a switch: every chart shows the reload text of [`web-pages.md`](../../docs/areas/web-pages.md); a
  later switch by the user reads again.
- E5: A period holding more points than the cap of 2,880: the newest 2,880 and the cap line, as today. `Last 7 days` holds at
  most 169 hourly points, so the cap line appears there never and for `All` only beyond 2,880 hours of history.
- E6: A tick in which no island holds an entry (*No data yet*, [`pipe.md`](../../docs/areas/pipe.md), the tick the game
  sends after a load) can carry a game time far below the others (19,000 against about 1.75 × 10⁹ in the recording). On the
  game-time axis it is left out, so it never stretches the axis; on the real-time axis it stays a point without values.
  Every other point without a value (a tick without the good, or without the island) stays a break on both axes.
- E7: An hourly mean on the game-time axis sits at the largest game time of its hour (the smallest can be that of E6), and
  joins the next point unless more than 15 minutes of game time pass between its largest game time and the next point's
  smallest.
- E8: Game time that does not rise with receive time (an older save played under the same profile, a tick split by a stall
  with one game time, [`pipe.md`](../../docs/areas/pipe.md)): on the game-time axis the points are drawn in game-time order,
  points of equal game time side by side; the real-time axis is unchanged.
- E9: One browser offset serves the whole period ([record 0078](../../docs/decisions/0078-times-shown-at-the-browsers-utc-offset.md)),
  so on `Last 7 days` and `All` a daylight-saving change inside the period shows every point before it one hour off, and an
  offset that is no whole hour (+5:30) puts the hourly points between the labels (`:30`).
- E10: A late tick for an older hour of `Last 7 days` or `All` shows when the charts are next read whole (page opened, choice
  made).

## Non-functional requirements

- N1: The bounds of [`web-charts.md`](../../docs/areas/web-charts.md) stay: at most 2,880 points held per chart, one chart
  read at a time across the server in the trend gate, no tick held beyond the one folded into a point, no read before the
  page is interactive. A transaction of a read scans at most 240 ticks or hourly points, so the ingest waits no longer than
  today. A new tick reads only the newest hour of `Last 7 days` and `All` again. Accepted worst case: a region chart over
  `Last 7 days` at the contract's bounds, cache cold, scans about 5,040 ticks in about two minutes (estimated from 68 to
  74 s for 2,880), showing `Loading…` while other chart reads wait; the plan measures it for the area document, and above
  five minutes it goes back to the Lead.
- N2: The choice is kept like the theme and the language ([record 0071](../../docs/decisions/0071-web-ui-choices-kept-in-browser-cookies-written-by-a-script.md),
  [`web-ui.md`](../../docs/areas/web-ui.md) *Stored choices*): fixed values only, nothing stored, logged or echoed by the
  server. No new route, no setting.
- N3: Every new text comes from `UiTexts` in English and German; no time or number is formatted with a culture.
- N4: Game time is read as milliseconds of game time ([`protocol.md`](../../docs/protocol.md), *Timestamp*, assumed); the
  tooltip's game time and the 15-minute rule depend on that unit.

## Acceptance criteria

- AC1: On either axis, each range shows the points from the start of the UTC hour that holds the latest tick's receive time minus 1 hour,
  24 hours or 7 days, up to and including that tick, and `All` every point of the profile up to it (S1, S5).
- AC2: `Last 7 days` and `All` show one point per UTC hour that holds a tick, folded or not, with each value the mean over
  the hour's ticks that carried it (for the region the sum of the islands' means, the overview's figures as
  [`web-charts.md`](../../docs/areas/web-charts.md) computes them for an hourly point); consecutive hours are joined, a
  missing hour is a gap; a new tick redraws the newest hour and keeps the older ones (S2, S5, E5, E10, N1).
- AC3: On `Game time` each point sits at its game time; the axis shows grid lines without time labels, and the tooltip
  shows the point's real time as on `Real time`, then the line `Game time <h>:<mm>` / `Spielzeit <h>:<mm>` (the tick's game
  time as hours and minutes, hours not limited; for an hourly mean its largest); a line breaks only where a value is
  missing or more than 15 minutes of game time pass between two points; E6, E7 and E8 hold (S3, N4).
- AC4: Choosing a range or an axis updates every chart on the page — both charts of a good's page, the overview of an
  island's page — to the new choice without a reload, and only the last of several quick choices is shown (S1, S3, E3, E4).
- AC5: A choice is kept in the browser and opens every chart page with it, and an absent or unknown stored value opens
  `Last 24 hours` on `Real time` without showing or logging the value (S4, E1, E2, N2).
- AC6: The picker offers `Last hour`, `Last 24 hours`, `Last 7 days`, `All` (German `Letzte Stunde`, `Letzte 24 Stunden`,
  `Letzte 7 Tage`, `Gesamt`) and the switch `Real time`, `Game time` (`Echtzeit`, `Spielzeit`); the range line reads
  `<range> up to the latest tick` / `<range> bis zum letzten Tick` (`All` as `All history` / `Gesamter Verlauf`) (S1, N3).
- AC7: The real-time axis labels are `HH:mm`, and `MM-dd HH:mm` when the points span more than one day, at the offset of
  E9. On both axes the spacing of labels or grid lines is the smallest of 10, 15 and 30 minutes, 1, 2, 3, 6 and 12 hours
  and 1, 2, 7, 14, 30, 90 and 365 days that gives at most 8 intervals over the span the points cover, and 365 days beyond
  that (a span of 169 hours: 1 day) (S1, S2, S3, E9).

## Out of scope

- A range of the user's own choosing (dates, zoom, dragging), a range per chart, and a range on the server's clock.
- Changing the retention, the fold or the cap of 2,880 points; sparklines and trend arrows (#38).
- Time labels on the game-time axis, and a chart other than MudBlazor's ([record 0086](../../docs/decisions/0086-charts-drawn-with-mudblazors-own-time-series-chart.md)).
- A choice per profile.

## Open questions

- Q1: On `Game time`, is a range a period of game time (the last hour of game time up to the latest tick's game time) or
  the same period of receive time as on `Real time`, with only the positions changed? Answered: the same receive-time period (Product Manager, 2026-11-17); AC1 holds on
  both axes.
- Q2: Does `Last 7 days` show the whole week? Answered: yes, the issue names a week; the cap of 2,880 points
  ([record 0087](../../docs/decisions/0087-chart-reads-aggregate-per-point-bounded-in-parts-with-a-product-index.md)) would cut
  its about 5,040 ticks to four days, hence hourly means (AC2).
- Q3: Default choice. Answered: `Last 24 hours` on `Real time`, today's charts (record 0088).

## Area documents

- [`web-charts.md`](../../docs/areas/web-charts.md): *Range* becomes the four ranges with their starts and resolution,
  the picker and the switch with their texts; a new *Game-time axis* section (placement, tooltip, gaps, E6 to E8); *Tooltip, axes and times* gains the
  label formats and spacing of AC7 and E9; *Live
  updates* and *States of a chart* gain the switch; the reads' bounds and measured times for the wide ranges; *Not here*
  drops #37.
- [`web-ui.md`](../../docs/areas/web-ui.md): *Stored choices* lists the two new cookies and their values; *Resources* if
  the script changes.
- [`history.md`](../../docs/areas/history.md): the trend reads by hour over ticks and hourly points, and their game times.
- Outside `docs/areas/`: `README.md` and `docs/USER_GUIDE.md` (English, then German) describe the picker and the switch,
  and the README's limitation line on #37 goes (issue).

## Challenge

- MAJOR 1 (game-time `h:mm` labels the chart cannot write) — accepted. MudBlazor 9.13.0 writes time labels only through a
  `DateTime` format (`TimeLabelFormat`, `TrendChart.razor.cs` line 25), which wraps at 24 hours and, with days, at a month;
  `d HH:mm` of a mapped time would repeat after 31 days, which `All` of a long savegame reaches, and the game time has no
  confirmed origin (MINOR 5). Rejected too: an own label row beside MudBlazor's chart (its alignment no unit test can pin)
  and another chart (record 0086). So the game-time axis shows grid lines without labels, and the tooltip carries the game
  time, beside the real time for orientation (AC3; *Out of scope*).
- MAJOR 2 (spacing for wide ranges) — accepted: AC7 gives one step list up to 365 days for both axes, no 3-hour fallback
  (`TrendChart.razor.cs` lines 44 and 203), and pins the 169-hour span to 1 day.
- MAJOR 3 (no bound for wide reads) — accepted: N1 bounds a transaction by 240 scanned ticks or hourly points, states the
  accepted worst case with its owner and threshold, and a new tick reads only the newest hour (S5, AC2, E10).
- MINOR 4 — accepted: E6 is the no-data-yet tick (`PipeTick.IsNoDataYet`); other no-value points stay breaks.
- MINOR 5 — accepted: AC3 says the tick's game time as hours and minutes, no origin.
- MINOR 6 — accepted: E9 (daylight saving and half-hour offsets), named by AC7 and in the area document.
