# Spec: Sparklines and trend arrows in tables

Source: Issue #38
Status: Draft

## Problem

A player who opens an island's goods table or a region's island list sees only the values of the latest tick. Whether a
good's balance or an island's growth is rising or falling needs a click into the good's page or a look at the island's
overview chart, one row at a time. The issue asks for the trend of every row at a glance: a small line (sparkline) and
an arrow (up, down, stable) over a defined window.

The window and the values are the Product Manager's answer (*Open questions*).

## Scenarios

- S1: Given an island whose goods were stored over the last hours, when the player opens the island's page, then the goods
  table has a column `Trend` / `Trend` after `Delta`; each row shows a sparkline of the good's delta on this island over
  the window (one point per stored tick, and per hourly point of an hour the retention has folded, the values the island
  chart of the good's page shows, [`web-charts.md`](../../docs/areas/web-charts.md), *Points, gaps and hourly means*) and
  an arrow after it.
- S2: Given a region whose islands were stored over the last hours, when the player opens the region's island list, then the
  table has a column `Trend` / `Trend` after `Buildings`; each row shows a sparkline of the island's buildings (the
  points and figures of the island's overview chart) and an arrow.
- S3: The window is a fixed `Last 4 hours`, by the start rule of the charts' ranges: from the start of the UTC hour that
  holds the latest tick's receive time minus 4 hours, up to and including that tick (so 4 to 5 hours; at the recorded
  spacing of about 120 s between ticks, [`protocol.md`](../../docs/protocol.md), about 120 to 150 points). It does not
  follow the range picker or the time-axis switch; a sparkline is always on real time. The column heading carries the
  window as its tooltip: `Last 4 hours up to the latest tick` / `Letzte 4 Stunden bis zum letzten Tick`.
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
- E2: A point without a value, and more than 15 minutes between two points, is a break in the sparkline as in the
  charts, never drawn as zero; the arrow skips points without a value (S4).
- E3: Before the trends are read, and while the page is prerendered, the trend cells are empty; the rest of the table
  shows as today.
- E4: A failed read shows `–` in every trend cell and, under the table, `The trends cannot be read right now. Reload the
  page to try again.` / `Die Trends können gerade nicht gelesen werden. Laden Sie die Seite neu, um es erneut zu
  versuchen.`; it logs the charts' warning ([`web-charts.md`](../../docs/areas/web-charts.md), *Logging*) and is not
  retried until the page is loaded again. A cancelled read shows and logs nothing.
- E5: An island the latest tick lacks keeps its `–` figures in the island list and still gets its trend from the history.
- E6: The goods table shows trends only for its shown rows (at most 500); a profile deleted while the page is open shows
  `Not found.` as today.
- E7: Values whose rounded figures stay equal while the raw values drift (delta -0.004 to 0.004) give `Stable`.

## Non-functional requirements

- N1: The table and its figures appear as fast as today: they never wait for the trends, and a trend read never leaves
  another page on `Loading…` nor lets the ingest wait longer than the charts' reads do
  ([`web-charts.md`](../../docs/areas/web-charts.md), *The reads*).
- N2: A page holds at most 240 points per shown row for its trends: a window with more (a faster tick rate, a replay)
  shows its newest 240, without a cap line.
- N3: A sparkline carries only numbers: no name reaches its markup. Nothing new is logged beyond E4's warning; no name,
  value or address.
- N4: No number is formatted with a culture; every new text is in English and German in `UiTexts`.
- N5: The sparkline's colour reaches 3:1 against the surface in both themes; the direction is never carried by colour
  alone (S4). A sparkline has no animation.
- N6: No new setting; the history already stored shows trends at once.

## Acceptance criteria

- AC1: Each shown row of the goods table carries a sparkline whose points are, in time order, the good's delta on the
  island at each point of the window of S3 (at most the newest 240), from the history already stored; rows beyond the
  500 shown get none (S1, S3, E6, N2, N6).
- AC2: Each row of the island list carries a sparkline whose points are the island's buildings figure at each point of
  the window of S3 (at most the newest 240), also for an island the latest tick lacks (S2, S3, E5, N2).
- AC3: The arrow is `↑` / `↓` / `→` with the names of S4, decided from the oldest and newest point with a value rounded as
  the table rounds; the names come in English and German without culture formatting, the arrow takes the text colour,
  and the sparkline's colour reaches 3:1 against the surface in both themes, without animation (S4, E2, E7, N4, N5).
- AC4: A row with fewer than two valued points shows `–` and no arrow; a point without a value and a pause of more than 15 minutes
  break the line (E1, E2).
- AC5: Trend cells are empty before the read and when prerendered; a failed read shows `–`, the line of E4 and logs the
  warning once; a cancelled read shows and logs nothing (E3, E4, N3).
- AC6: A stored tick of the page's profile updates the trends without a reload; a tick of another profile, a range or
  axis choice reads nothing for them; a theme or language switch redraws them without a read (S5).
- AC7: The table's figures render before the trend read answers, and the trend read runs as the charts' reads are
  bounded: in the trend gate, at most 240 scanned rows per transaction (N1).

## Out of scope

- Trends in the region overview, on the good's page and for production, consumption, productivity, produced goods or
  goods in deficit.
- Any other window (24 hours, the page's chart choice), a window of the user's choosing, 15-minute or hourly means, a
  game-time sparkline, a tooltip per sparkline point, sorting by trend.

## Open questions

- Which value and window do a row's sparkline and arrow follow? Answered: by the Product Manager, delta (goods table)
  and buildings (island list) over a fixed window, real time, independent of the range picker, of 4 hours instead of the
  recommended 24 ("24h ist eventuell zu viel, lass uns 4h nehmen"; "Delta und Gebäude").
- At which resolution? Answered: by the Lead, every point as the charts' `Points` resolution reads it (`TrendRange.ResolutionOf`,
  `src/Censio.Server/TrendRange.cs`; [`history.md`](../../docs/areas/history.md), *The trend reads*). Hourly
  means give 4 to 5 points over 4 hours, too few to show a trend; the existing trend reads offer only `Points` and
  `Hours`, so 15-minute means would need a new read resolution in `HistoryStore` for no gain at about 120 to 150 ticks.
  The newest point is then the latest tick, so the sparkline ends at the value the table shows.

## Area documents

- `docs/areas/web-charts.md`: a section *Sparklines and trend arrows* with the window, points, arrow rule, texts, states,
  live updates and reads; its *Scope* and *Not here* drop "#38".
- `docs/areas/web-pages.md`: the new `Trend` column of the goods table and the island list; *Not here* drops sparklines.
- `docs/areas/history.md`: only if the plan adds a read; its *Not here* line on #38 points to web-charts.
