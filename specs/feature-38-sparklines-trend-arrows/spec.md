# Spec: Sparklines and trend arrows in tables

Source: Issue #38
Status: Draft

## Problem

The goods table and the island list show only the latest tick: whether a good's balance or an island's growth is
rising needs a chart, one row at a time. The issue asks for every row's trend at a glance: a sparkline and an arrow
(up, down, stable) over a defined window.

## Scenarios

- S1: When the player opens an island's page, the goods table has a column `Trend` / `Trend` after `Delta`: per row a
  sparkline of the good's delta on the island over the window, at the points and values of the good page's island chart
  ([`web-charts.md`](../../docs/areas/web-charts.md), *Points, gaps and hourly means*), and an arrow after it.
- S2: When the player opens a region's island list, the table has a column `Trend` / `Trend` after `Buildings`: per row a
  sparkline of the island's buildings at the points and figures of its overview chart, and an arrow.
- S3: The window is a fixed `Last 4 hours`, by the start rule of the charts' ranges: from the start of the UTC hour that
  holds the latest tick's receive time minus 4 hours, up to and including that tick (so 4 to 5 hours; at the recorded
  spacing of about 120 s between ticks, [`protocol.md`](../../docs/protocol.md), about 120 to 150 points). It ignores
  the range picker and the time-axis switch (always real time). The column heading's tooltip: `Last 4 hours up to the latest tick` / `Letzte 4 Stunden bis zum letzten Tick`.
- S4: The arrow compares the newest point of the sparkline that has a value with the oldest one that has a value, each
  rounded as the table rounds that value (delta to two decimals, midpoint away from zero; buildings to a whole number):
  newer higher is `↑` named `Rising` / `Steigend`, lower is `↓` named `Falling` / `Fallend`, equal is `→` named `Stable` /
  `Gleichbleibend` (the name is the arrow's accessible name and tooltip). The arrow takes the text colour.
- S5: Given an open page, when a tick of its profile is stored, then the sparklines and arrows follow without a reload, as
  the charts do ([`web-charts.md`](../../docs/areas/web-charts.md), *Live updates, theme and language*); a tick of another
  profile, a choice of range or axis changes none; a theme or language switch redraws them without a read.

## Errors and edge cases

- E1: A row with fewer than two points that have a value in the window shows `–` in the trend cell, no sparkline and no
  arrow (a new good, an island the history lacks).
- E2: The sparkline breaks by the charts' one rule, *Gaps* of [`web-charts.md`](../../docs/areas/web-charts.md) (*Points,
  gaps and hourly means*), never drawn as zero; the arrow skips points without a value.
- E3: Before the trends are read, and when prerendered, the trend cells are empty.
- E4: A failed read shows `–` in every trend cell and under the table `The trends cannot be read right now. Reload the
  page to try again.` / `Die Trends können gerade nicht gelesen werden. Laden Sie die Seite neu, um es erneut zu
  versuchen.`; it logs the charts' warning (`web-charts.md`, *Logging*), is not retried until a reload. A cancelled read shows and logs nothing.
- E5: An island the latest tick lacks keeps its `–` figures in the island list and still gets its trend from the history.
  An island page that shows `No values for this island in the latest tick.` instead of the goods table has no trend
  cells and reads no trends.
- E6: Only the shown rows of the goods table (at most 500) get trends.
- E7: Values whose rounded figures stay equal while the raw values drift (delta -0.004 to 0.004) give `Stable`.
- E8: A good the island carries twice in the latest tick has two rows; the history keeps the first entry of a good
  ([`history.md`](../../docs/areas/history.md), *The series read*), so the row of that first entry (the first of the two
  in the tick's order) gets the trend and the other row shows `–`.

## Non-functional requirements

- N1: The table and its figures appear as fast as today: they never wait for the trends. One table's trends, on opening
  and on each refresh, are one read for all its rows (all shown goods of the island; all islands of the region), not one
  per row, of at most two read transactions in the charts' trend gate: the times and one part of at most 240 points. A
  refresh holds the gate (and, in memory, the ingest) no longer than one region part of a chart read at the bounds
  (about 0.3 to 0.5 s warm, [`history.md`](../../docs/areas/history.md), *Measurements*). On an island page the overview chart is read first, then the trends; a failure of one
  does not skip the other.
- N2: A page holds at most 240 points per shown row for its trends: a window with more (a replay faster than about one
  tick per 75 s) shows its newest 240, without a cap line, and the arrow compares the oldest and newest of those; the
  heading tooltip still names 4 hours, which README and user guide state as a limit.
- N3: No name reaches a sparkline's markup; nothing is logged beyond E4's warning.
- N4: No number is formatted with a culture; every new text is in English and German in `UiTexts`.
- N5: The sparkline's colour reaches 3:1 against the surface in both themes. A sparkline has no animation and is hidden
  from the accessibility tree: the arrow's name is the row's only trend text.
- N6: No new setting; the history already stored shows trends at once.
- N7: A sparkline is one SVG line of coordinates only: no hover points, tooltip, axes, labels or legend (not MudBlazor's
  chart). Its markup at 240 points is at most 4,000 characters, so 500 rows render at most about 2 MB; the plan measures
  the render batch and heap of 500 rows by 240 points as [`web-charts.md`](../../docs/areas/web-charts.md) (*Memory and
  size of a page*) measures the charts.

## Acceptance criteria

- AC1: A shown goods row's sparkline is the good's delta on the island at each point of S3's window (the newest 240 at
  most), from stored history; unshown rows and the second row of a good carried twice get none (S1, S3, E6, E8, N2, N6).
- AC2: An island row's sparkline is the island's buildings figure at each point of the window (the newest 240 at most),
  also for an island the latest tick lacks; an island page without a goods table reads no trends (S2, S3, E5, N2).
- AC3: The arrow and its names follow S4 from the rounded oldest and newest valued points, in both languages; the
  sparkline's colour reaches 3:1 in both themes, without animation, hidden from the accessibility tree (S4, E7, N4, N5).
- AC4: A row with fewer than two valued points shows `–` and no arrow; the line breaks as E2 says (E1, E2).
- AC5: Trend cells are empty before the read and when prerendered; a failed read shows `–`, the line of E4 and logs the
  warning once; a cancelled read shows and logs nothing (E3, E4, N3).
- AC6: A stored tick of the page's profile updates the trends without a reload; a tick of another profile, a range or
  axis choice reads nothing for them; a theme or language switch redraws them without a read (S5).
- AC7: The figures render before the trend read answers; a table's trends, opened or refreshed, are one read of at most
  two trend-gate transactions, each scanning at most 240 ticks or hourly points, after the overview chart's read on an
  island page, neither failure skipping the other (N1).
- AC8: A sparkline is one SVG line of coordinates only (no name, hover point, axis or legend), and its markup at 240
  points is at most 4,000 characters (N7).

## Out of scope

- Trends in the region overview, on the good's page and for production, consumption, productivity, produced goods or
  goods in deficit.
- Another or user-chosen window, 15-minute or hourly means, game time, a tooltip per point, sorting by trend.

## Open questions

- Which value and window do a row's sparkline and arrow follow? Answered: by the Product Manager, delta (goods) and
  buildings (islands) over a fixed real-time window of 4 hours, not the recommended 24 ("24h ist eventuell zu viel, lass
  uns 4h nehmen"; "Delta und Gebäude").
- At which resolution? Answered: by the Lead, every point, as the charts' `Points` resolution reads it
  (`TrendRange.ResolutionOf`). Hourly means give 4 to 5 points over 4 hours, too few for a trend; 15-minute means would
  need a new resolution in `HistoryStore` for no gain at about 120 to 150 ticks. The newest point is the latest tick,
  so the sparkline ends at the value its row shows (E8).

## Area documents

- `docs/areas/web-charts.md`: a section *Sparklines and trend arrows* (window, points, arrow, texts, states, updates,
  reads, sizes); *Scope* and *Not here* drop #38.
- `docs/areas/web-pages.md`: the new `Trend` column of the goods table and the island list; *Not here* drops sparklines.
- `docs/areas/history.md`: the batch read of N1 under *The trend reads*; its *Scope* and *Not here* lines on #38 point
  to web-charts.
- `README.md` (lines 22 and 204): the limit "no sparklines or trend arrows (#38)" gives way to the trend column, its
  window and the 240-point limit of a fast replay.
- `docs/USER_GUIDE.md`, English and German: the pages item describes the column; *Latest data only* (line 441, "the one
  place that shows the history") names it.

## Challenge

- M1 (total trend work unbounded, one read per row): accepted. N1 and AC7 now require one read per table and refresh for
  all its rows, of at most two transactions in the trend gate, with the time it holds the gate at the bounds, and fix the
  order on the island page (overview chart first, `IslandDetail.razor.cs` `ReadDetailsAsync`). Today's reads are one
  good or one island each (`TrendReader.ReadGoodAsync`, `ReadFiguresAsync`), so this needs a new batch read; the
  `history.md` line under *Area documents* is now mandatory.
- M2 (render size and memory): accepted. N7 and AC8 fix the markup (one SVG line, coordinates only, not MudBlazor's
  chart) and a per-row size a unit test pins; the plan measures the batch and heap at 500 rows by 240 points.
- m1 (README, user guide): accepted; both are under *Area documents* with their passages.
- m2 (a good carried twice): accepted as E8: the row of the first entry gets the trend, the other `–`, so a trend never
  ends at another row's delta (checked: `ProfileViewBuilder` keeps every entry, the reads take the first).
- m3 (gap rule): accepted; E2 cites the charts' rule with its end times.
- m4 (cap versus tooltip): accepted. N2 says the arrow compares the shown 240; the tooltip stays and the limit is
  documented, because the cap is reached only by a replay faster than about one tick per 75 s, never at the game's
  120 s, and a tooltip per row would add text to 500 rows for that case.
- m5 (accessibility): accepted in N5 and AC3.
- m6 (no goods table): accepted in E5 and AC2; that page reads no trends.
