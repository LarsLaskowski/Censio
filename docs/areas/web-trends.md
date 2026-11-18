# Web trends

## Scope

The sparklines and trend arrows of the goods table of an island's page and of a region's island list: the `Trend` column
with its window, values, line, arrow, states, live updates and texts, the batch reads that feed it (the goods of one island
and the buildings of a set of islands) with their island-point parts, and the measurements and limits of those reads and of
the page's memory. Users of the web application rely on it; the charts are in [`web-charts.md`](web-charts.md), the stored
history and the other trend reads in [`history.md`](history.md).

## Behavior

### Sparklines and trend arrows

- **Where.** The goods table of an island's page has a column `Trend` / `Trend` after `Delta`, and the island list of a region
  has one after `Buildings` ([`web-pages.md`](web-pages.md)). The column heading's tooltip is `Last 4 hours up to the latest
  tick` / `Letzte 4 Stunden bis zum letzten Tick`. A cell shows a sparkline and, after a space, an arrow. An island page
  without a goods table (`No values for this island in the latest tick.`) has no trend cells and reads no trends.
- **What a row shows.** A goods row shows the good's delta on the island at each point of the window, the values and points
  of the good page's island chart ([`web-charts.md`](web-charts.md), *Points, gaps and hourly means*); an island row shows the island's buildings figure at
  each point, also for an island the latest tick lacks. Only the shown rows of the goods table (at most 500) get trends. A
  good the island carries twice in the latest tick has two rows: the row of its first entry gets the trend and the other
  shows `–`, because the history keeps the first entry of a good ([`history.md`](history.md), *The series read*).
- **The window** is fixed: the points from the start of the UTC hour that holds the latest tick's receive time minus 4
  hours, up to and including the latest tick, so 4 to 5 hours (about 120 to 150 ticks at the game's 120 seconds,
  [`protocol.md`](../protocol.md)), always in real time, whatever the range picker and the time-axis switch say. Every point of
  it is drawn (`Points` resolution): each tick, and an hour the retention has folded (older than 7 days, so only for a
  profile not played for a week) as its one hourly point ([`history.md`](history.md)). A window with more than 240 points, which only a replay faster than
  about one tick per 75 seconds makes, shows its newest 240 without a cap line; the arrow then compares the oldest and the
  newest of those, and the heading's tooltip still says 4 hours.
- **The line** is one SVG path of coordinates in a box of 80 by 20 units, a stroke of 1.5 with round caps, the window's
  start at the left and the latest tick's end at the right. The smallest value of the row's points sits at the bottom, the
  largest at the top, and equal values in the middle. It breaks by the rule of *Points, gaps and hourly means*: where a point
  has no value, and after more than 15 minutes between the end of a point and the next one; it is never drawn as zero. A
  point alone between two breaks is drawn as a dot. A cell with fewer than two points that have a value shows `–` and no
  line or arrow. The path has no more than one decimal a coordinate, and its markup at 240 points is at most 4,000
  characters, checked by a unit test; the cell holds no name, hover point, axis, label or legend.
- **The arrow** compares the newest point that has a value with the oldest one that has a value, each rounded as the table
  rounds the value (the delta to two decimals, midpoint away from zero; buildings to a whole number): a higher newest value
  is `↑` named `Rising` / `Steigend`, a lower one `↓` named `Falling` / `Fallend`, an equal one `→` named `Stable` /
  `Gleichbleibend`. So a delta that drifts from -0.004 to 0.004 is `Stable`. The name is the arrow's accessible name
  (`role="img"`, `aria-label`) and its tooltip; the arrow has the text colour.
- **Colour, animation and accessibility.** The line has the palette's secondary colour of the theme shown, which reaches 3:1
  against the surface in both themes ([`web-charts.md`](web-charts.md), *Colours*, a unit test). A sparkline has no animation: the reveal of [`web-charts.md`](web-charts.md), *Animation* applies
  to the charts' lines only, and a sparkline is neither in a `censio-chart` element nor a `mud-chart-line`. The SVG is hidden
  from the accessibility tree (`aria-hidden="true"`), so the arrow's name is the row's only trend text.
- **States.** A cell is empty before its trends are read and on the prerender, which reads none ([`web-pages.md`](web-pages.md),
  *Reads and memory*). After a failed read every cell shows `–`, the line `The trends cannot be read right now. Reload the
  page to try again.` / `Die Trends können gerade nicht gelesen werden. Laden Sie die Seite neu, um es erneut zu versuchen.`
  shows under the table (after the line `Showing 500 of <n> goods.` where that shows), the warning of [`web-charts.md`](web-charts.md), *Logging* is logged once,
  and the trends of that address are not read again until the page is reloaded or its address changes. A cancelled read
  shows and logs nothing. A good or island the held read lacks (new since it was read) has an empty cell until the next read.
- **Live updates.** A stored tick of the page's profile reads the trends again without a reload and the sparklines and
  arrows follow; until it answers the cells show the held ones, by good or island. A tick of another profile, a range choice
  and an axis choice read none, and a range choice neither cancels a running trend read nor repeats it. A theme or language
  switch redraws the sparklines and arrows from the read the page holds, without a read.
- **Reads and their order.** A table's trends are one read for all its rows (the shown goods of the island; the islands of the
  region), not one a row: a times transaction and parts of at most 240 island points under the trend gate
  (*The batch reads of the trend column*, below), asked for the newest 240 points of the window. The
  trends never run inside the page's load loop ([`web-pages.md`](web-pages.md), *Live updates*): the page starts them and goes
  on, so the table's figures render before they answer and a stored tick updates the figures while a trend read is
  running. On an island page the overview chart is read first and then the trends, and a failure of one does not skip the
  other. A page keeps at most one trend read running and one request waiting, the newest; an address change (another profile,
  island or region) cancels the running read, which frees the trend gate at its next part, and drops what the page held.
  A read the page leaves is cancelled with it.
- **The refresh.** The first read of an address reads the whole window. A later read for the same keys (the same goods or
  islands) starts just after the newest point the page holds, never before the window start, appends the new points to the
  held ones, drops those before the new window start and keeps the newest 240, so a tick costs a part or two. A read for other
  keys (a good or an island added or gone) or without held points reads the whole window again. A tick that
  is stored with the receive time of the held newest point or earlier, and a tick folded into an hourly point inside the held
  part, show only at the next whole read ([record 0092](../decisions/0092-row-trends-read-once-per-table-in-island-point-parts.md)).
- **No names.** The cell writes only numbers, the hex colour and fixed texts; a good's or island's
  name never reaches it, and a failed read logs nothing beyond the warning of *Logging*.

### The batch reads of the trend column

Two reads beside the charts' reads of [`history.md`](history.md) give a table's sparklines in one read for all its rows,
each of one profile, a range and the newest `MaxPoints` points (the server asks for 240, over the fixed four hours of
  *Sparklines and trend arrows*) at `Points` resolution, with the times of [`history.md`](history.md), *The trend reads* (*Times first*)
  and then the values in parts, each part one read transaction under the trend gate.
  - *Goods of one island.* The read names one island and a set of product GUIDs (at most the 500 shown rows). A point holds,
    for each good of the set, the `Delta` of the good's first entry of the island in stored position in that tick (no value
    where that delta has none, and the second entry of a repeated good is never used), or for an hourly point the mean
    `DeltaSum / DeltaCount` of the island's hourly entry; a good with no value is absent from the point. A part is one
    statement over at most 240 ticks, the goods read through `json_each` of one JSON parameter, ordered by tick and
    position.
  - *Buildings of a set of islands.* The read names a set of islands (the island list's region, unbounded in count). A point
    holds, for each island that has entries, its buildings: the sum of the entries' `BuildingCount` for a tick, and for an
    hourly point `Math.Round(Σ BuildingCountSum / EntryCount, MidpointRounding.AwayFromZero)`, the quotients of the island's
    hourly entries added in C# in the order of the entries, as the figures read does, never summed in SQL; an island with no entries in
    the point is absent. The island set is one JSON parameter as the figures read has it.
  - *Parts of island points.* A part reads at most 240 island points (a tick or hourly point of one island), so that it scans
    no more entries than a part of the figures read: the islands are cut in chunks of 240, the last chunk taking the rest,
    and a chunk of `c` islands reads its points in slices of `240 / c` points (integer division, at least one). 24 islands by
    240 points are 24 parts, 240 islands by 240 points 240 parts and 241 islands by 240 points 241 parts; no point or no
    island gives no part. A part of the goods read is a part of 240 points like the others.
  - Like the other trend reads they return at most the maximum of points asked and the count of all points of the range.

## Limits and errors

- **The island list's opening read.** The whole opening read of an island list at the contract's bounds (256 islands by 240
  points, 256 parts, scaled from 60 ticks, *Measurements*, below) takes about 12.8 to 13.4 s warm and
  17 to 19.7 s with the page cache dropped, which exceeds the 10 seconds a step of that section, while each part takes 90 to
  150 ms and the page shows its figures meanwhile (its cells stay empty). Every open island list holds `TrendReader`'s gate
  for that long when it is opened, and again at each refresh, which reads the points after the held newest one and takes
  100 to 120 ms; a chart read of another page waits behind it. The time
  grows with the region's islands, whose number the island list does not limit ([`server-state.md`](server-state.md)). The
  goods table's opening read of one island takes 0.36 to 0.7 s.
- **Memory and size of the trend column.** Measured with a scratch program that renders a goods table of 500 rows by 240
  points through the cell component and a Blazor renderer: the render batch of the 500 trend cells is 1.40 MiB, the heap of
  the renderer and its tree 3.56 MB, and the read a page holds (the samples and lines of 500 goods) 6.91 MB. One whole goods
  read holds 3.39 MB of points (`HistoryGoodsPoint` dictionaries of 240 points by up to 500 goods) and allocates about 26 MB
  in all; while a refresh is in flight and the held read is still shown, the trends take about 13.9 MB of the page's heap,
  and the page about 17.5 MB.

### Measurements

Measured as the sandbox and scratch program of [`history.md`](history.md) (*Measurements*) describe, at the contract's bounds
(256 islands of 512 entries per tick), 60 ticks scaled to 240 points; an order of magnitude, not a guarantee.

| What | Result |
| ---- | ------ |
| The batch reads of the trend column, 60 ticks at the bounds (256 islands, 512 entries) scaled to 240 points: one part of 240 island points | 90 to 150 ms |
| The whole opening read of the island list, 256 islands by 240 points (256 parts), warm and with the page cache dropped | about 12.8 to 13.4 s warm and 17 to 19.7 s cold |
| A refresh of the island list after one new tick (a times transaction and two parts: 256 islands by 1 point) | 100 to 120 ms |
| The opening read of a goods table (the goods of one island by 240 points) | 0.36 to 0.7 s |

The whole opening read of the island list at the bounds exceeds the 10 seconds a step of [`history.md`](history.md), *Measurements*: 256 islands by
240 points take about 12.8 to 13.4 s warm and 17 to 19.7 s with the page cache dropped, while each part stays far within the
limit (90 to 150 ms) and the figures of the page never wait for it, because the page shows them while the trends are read
(*Limits and errors*). The refresh after a
tick reads only the points after the held newest one and takes 100 to 120 ms.

## Related decisions

- [`decisions/0091-row-trends-show-delta-and-buildings-over-a-fixed-four-hours.md`](../decisions/0091-row-trends-show-delta-and-buildings-over-a-fixed-four-hours.md) — a row's trend is the delta of a good or the buildings of an island over a fixed four hours.
- [`decisions/0092-row-trends-read-once-per-table-in-island-point-parts.md`](../decisions/0092-row-trends-read-once-per-table-in-island-point-parts.md) — a table's trends are one read in parts of at most 240 island points, refreshed by the new points.
- [`decisions/0093-sparklines-drawn-as-one-svg-path-of-our-own.md`](../decisions/0093-sparklines-drawn-as-one-svg-path-of-our-own.md) — a sparkline is one SVG path of our own, not MudBlazor's chart.

## Not here

- The charts, their range, gaps, tooltip, colours, animation, the chart reads and the logging rule: [`web-charts.md`](web-charts.md).
- The stored history, the series and chart trend reads, the trend gate and the other measurements: [`history.md`](history.md), [`server-data.md`](server-data.md).
- The tables' frame, columns and live updates, and the pages' reads and memory: [`web-pages.md`](web-pages.md).
- Trends in the region overview, on the good's page and for other values, another or user-chosen window, hourly means as a resolution of the window, a tooltip per point and sorting by trend.
