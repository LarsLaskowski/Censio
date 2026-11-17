# Web charts

## Scope

The trend charts of the data pages: the two charts of a good's page and the overview chart of an island's page, the
range picker and the time-axis switch with their stored choice, the range they show, the points and their gaps, hourly
means, the game-time axis, the region total, the cap of 2,880 points, the tooltip and the axes, the colours and the
animation, the reads that feed them with their bounds and failures, and the chart texts in
English and German. Users of the web application rely on it; the pages around the charts are in
[`web-pages.md`](web-pages.md), the history the charts read in [`history.md`](history.md).

## Behavior

### Where the charts are

- **The good's page** opens at `/profiles/{profile}/islands/{session}/{island}/{area}/goods/{good}` (the island's address
  of [`web-pages.md`](web-pages.md) and the good's product GUID). Every good name in the goods table of an island is a
  link to it. The heading is the good's shown name (a catalog name in the shown language, else in the other language,
  else `Good <GUID>` / `Ware <GUID>`); the tab title is `Censio – <heading>`. Under it, `<island> · <region> · <profile>`
  with the island a link to its detail and the region a link to its island list, then the banner and the as-of line as on
  every data page, the controls (below) and two charts:

  | Chart | English | German | Lines |
  | ----- | ------- | ------ | ----- |
  | The island's good | `This island` | `Diese Insel` | `Production`, `Consumption`, `Delta` / `Produktion`, `Verbrauch`, `Delta` |
  | The good in the region | `All islands of the region` | `Alle Inseln der Region` | the same three lines, each value the sum over the islands of the island's region ([record 0073](../decisions/0073-islands-grouped-by-the-catalog-region-of-their-session.md)) that carry a value at that point |

  The region's islands are those its island list shows ([`web-pages.md`](web-pages.md), *Regions*).
- **The island's overview** is the chart `Overview` / `Übersicht` on the island's page, between the as-of line and the
  goods table, with the controls under its heading. Its lines are `Buildings`, `Produced goods` and `Goods in deficit`
  (German `Gebäude`, `Produzierte Waren`, `Waren im Defizit`, the table columns). It shows also for an island the latest
  tick lacks, from the history in the range.
- The good segment of the address is parsed like the session segment: an integer of at most ten ASCII digits with an
  optional leading minus. An address whose profile, island or good segment is malformed, or whose profile or island is
  unknown, shows `Not found.` as the other data pages do; so does a profile deleted while the page is open. A well-formed
  good the island never carried opens its page with the empty-chart text (*States of a chart*).
- **The controls** (`TrendControls`) are one row above the charts of a good's page and under the heading `Overview` of an
  island's page: first a group of four toggle buttons for the range (accessible name `Period` / `Zeitraum`), then a group of
  two for the time axis (`Time axis` / `Zeitachse`), one item selected in each, wrapping on a narrow window, and under the
  row one line of text, the range line.

  | Range | English | German | Range line, English | Range line, German |
  | ----- | ------- | ------ | ------------------- | ------------------ |
  | last hour | `Last hour` | `Letzte Stunde` | `Last hour up to the latest tick` | `Letzte Stunde bis zum letzten Tick` |
  | last day (default) | `Last 24 hours` | `Letzte 24 Stunden` | `Last 24 hours up to the latest tick` | `Letzte 24 Stunden bis zum letzten Tick` |
  | last week | `Last 7 days` | `Letzte 7 Tage` | `Last 7 days up to the latest tick` | `Letzte 7 Tage bis zum letzten Tick` |
  | all | `All` | `Gesamt` | `All history up to the latest tick` | `Gesamter Verlauf bis zum letzten Tick` |

  The axes are `Real time` / `Echtzeit` (default) and `Game time` / `Spielzeit`. One choice serves every chart of the page
  and, through the cookies of [`web-ui.md`](web-ui.md) (*Stored choices*), every chart page of the browser, also after a
  reload and on the next day. A browser without a stored choice, or with an unknown stored value, opens `Last 24 hours`
  on `Real time`; a choice the browser fails to store stays on the open page and the next page opens with the defaults.
  Every chart text is a fixed text of `UiTexts`; no name is a line name, heading or note ([`web-ui.md`](web-ui.md), *Texts*).

### Range

- A chart shows the points from the start of the UTC hour that holds the receive time of the profile's latest tick minus 1
  hour (`Last hour`), 24 hours (`Last 24 hours`) or 7 days (`Last 7 days`), up to and including that tick, or, for
  `All`, every point of the profile up to it. So the hourly point of the start hour is shown whole, and a replayed or idle
  profile shows its last period, whatever the server's clock says ([record 0088](../decisions/0088-charts-show-the-day-up-to-the-latest-tick-with-gaps-after-15-minutes.md)).
  The same period of receive time serves both axes. The start is clamped at the beginning and the end at the end of the
  time scale, so no receive time throws.
- **Resolution.** `Last hour` and `Last 24 hours` show every tick (and the hourly points of folded hours), as before.
  `Last 7 days` and `All` show one point per UTC hour that holds a tick, folded or not: each value the mean over the hour's
  ticks that carried it (for the region the sum of the islands' means; the overview's figures as *Points, gaps and hourly
  means* says), the tooltip saying `Hourly average` / `Stundenmittel`. A week holds at most 169 such points, so the cap line
  appears there never, and for `All` only beyond 2,880 hours of history ([record 0089](../decisions/0089-chart-ranges-wide-ones-as-hourly-means-read-at-request.md)).
- A chart whose range holds more than 2,880 points shows the newest 2,880 and, under the chart, `Showing the newest 2,880
  of <n> points.` / `Die neuesten 2.880 von <n> Punkten angezeigt.`; `<n>` is the count of all points of the range, written as
  *Number formats* of [`web-pages.md`](web-pages.md) writes a count with grouped thousands.

### Points, gaps and hourly means

- A tick is one point at its receive time ([`contract.md`](contract.md)) and an hourly point one at the start of its hour
  ([`history.md`](history.md), *The series read*). Time without a tick has no point; nothing is filled in. The points are
  those the trend reads of [`history.md`](history.md) return, in time order; an hourly point comes before the ticks of its
  hour.
- A tick's values for the island's good chart are production, consumption and delta of the island's entry of the good in
  that tick (the first one when the island carries the good twice). The region chart adds them over the islands of the
  region that carry the value at that point: an island without the value adds nothing, and a point no island carries the
  value for is a point without value. The overview's values are the island's key figures for that tick as *Key figures* of
  [`web-pages.md`](web-pages.md) computes them for the latest tick, and no value when the tick lacks the island or the
  island has no entries.
- An hourly point shows the hour's means: each value is the sum divided by the number of ticks that carried it, summed over
  the islands for the region. Its overview figures are, from the means of the hour's goods: buildings the sum of the
  mean building counts rounded to a whole number (midpoint away from zero), produced goods the goods with a mean building
  count above 0, goods in deficit the goods whose mean delta, rounded to two decimals as *Number formats* says, is below 0.
  A good is in deficit when `Math.Round(delta, 2, MidpointRounding.AwayFromZero) < 0`, the formula of the pages
  ([`web-pages.md`](web-pages.md), *Number formats*), applied to the tick's delta or the hour's mean delta; a delta that rounds
  to `0.00` is no deficit.
- **Gaps.** A line is interrupted, never drawn to zero: at a point without the line's value, and between two points when the
  later starts more than 15 minutes after the earlier ends. A tick ends at its time, an hourly point one hour after its
  start. In the fixture `long-session.jsonl` as stored, before any fold, the 18-minute pause is such a gap and the
  9-minute session change is not; two consecutive hourly points are joined, a missing hour is a gap, and an hourly point is
  joined to the first tick after it. After the retention folds an hour, a pause inside it no longer shows
  ([record 0088](../decisions/0088-charts-show-the-day-up-to-the-latest-tick-with-gaps-after-15-minutes.md)); so a recording
  older than 7 days replayed into a server shows its pauses until the retention job's next run has folded it, and then as
  hourly means joined to each other and to the ticks of the latest hour.
- A chart draws a no-value point as a break; the value axis takes its range from the values only (the no-value point is
  given the line's first value, so it never stretches the axis).
- On the wide ranges consecutive hours are joined and a missing hour is a gap (more than 15 minutes between the end of one
  hour and the start of the next).

### Game-time axis

- On `Game time` each point sits at its game time, the Unix epoch plus its milliseconds as a `DateTime` (game times from
  -62,135,596,800,000 to 253,402,300,799,999 ms have a place, others none). The game time is read as milliseconds of game
  time ([`protocol.md`](../protocol.md), *Timestamp*, assumed); the tooltip's game time and the 15-minute rule depend on that unit.
- **Which points.** A tick or hour in which no island holds an entry (the tick the game sends after a load, `No data yet`
  of [`pipe.md`](pipe.md), whose game time can be far below the others) and a point whose game time has no place are left
  out, so they never stretch the axis; on `Real time` such a tick stays a point without values. Every other point without a
  value stays a break on both axes. Points are drawn in game-time order, whatever their receive times; points of equal game
  time lie at the same place and the tooltip names the first.
- **An hourly mean** sits at its largest game time and joins the next point unless more than 15 minutes of game time pass
  between its largest game time and the next point's smallest.
- **Gaps.** A pause of the game leaves no gap. A line breaks only where a value is missing or more than 15 minutes of game
  time pass between two points; the rule compares the places of the points, never the raw numbers, so no game time wraps it.
  A pause of 15 minutes of game time exactly does not break.
- **Labels.** The time axis carries no labels and no guide lines of its own: MudBlazor 9.13.0 writes time labels only
  through a `DateTime` format, which wraps at 24 hours and with days at a month, so the format is empty (`''`) and its axis
  group stays empty; it draws no vertical grid lines on either axis. The value axis keeps its horizontal grid lines as on
  `Real time`. Reading a time needs the tooltip, which names the real time and the game time
  ([record 0090](../decisions/0090-game-time-axis-places-points-without-time-labels.md)).
- **Limit of a folded hour.** The smallest game time of a folded hour covers all its folded ticks, a no-entry tick included,
  so it can be that tick's; on `All` such an hour then joins the point before it, because the rule measures from the
  earlier point's largest game time to this hour's smallest. A break the player expects there may not show.

### States of a chart

| Case | English | German |
| ---- | ------- | ------ |
| a chart without any value in the range | `No values in this period.` | `Keine Werte in diesem Zeitraum.` |
| a profile without a stored tick | `No data yet. …` of [`web-pages.md`](web-pages.md), instead of the charts | the German text there |
| the charts' read has not answered, or the page is prerendered, or the browser's offset has not answered for the first drawing | `Loading…` | `Wird geladen…` |
| a failed read | the reload text of [`web-pages.md`](web-pages.md), for every chart of the page | the German text there |

- A page whose read failed shows the reload text in every chart and reads no chart again until it is loaded again or a
  new range is chosen; a notice of the server state does not retry it. A cancelled read (the page is disposed, or its
  address or range changed) shows and logs nothing: a read of an address or range the page has left is cancelled at its
  next part, shown nowhere, and only a failure before that logs the warning of *Logging*.
- A chart keeps the series it draws when a new read for the same address arrives, until the browser has answered the
  offset for the new read's as-of time, then replaces it. A change of the address shows `Loading…` until its read has
  answered.

### Live updates, theme and language

- A stored tick of the page's profile gives every chart a new read without a reload, and the range moves with the latest
  tick; a tick of another profile changes nothing. A notice during a read causes one more read afterwards
  ([`web-pages.md`](web-pages.md), *Live updates*). On `Last 7 days` and `All` the new read starts at the hour of the last
  point the chart holds and replaces that hour and the ones after it, keeping the older hours (and dropping those before the
  range start); a tick that arrives late for an older hour shows only when the charts are next read whole (page opened,
  choice made).
- **A choice** of a range re-reads every chart of the page with that range without a reload: each chart shows `Loading…`
  until its points for the new choice are there, the page keeps the profile's view, and a read of an earlier choice is
  cancelled (it stops at its next part and frees the trend gate) and never shown. Of several quick choices only the last is
  shown. After a failed read a new choice reads again. A choice of an axis reads nothing: every chart is redrawn from the
  read it holds, as a language switch does.
- A theme or language switch redraws the charts from the series they hold, without a read: the line names, texts and
  value formats follow the language at once, the colours follow the theme.

### Tooltip, axes and times

- Hovering a point shows a tooltip drawn as SVG text: the point's time as `yyyy-MM-dd HH:mm`, for an hourly point the line
  `Hourly average` / `Stundenmittel`, on `Game time` the line `Game time <h>:<mm>` / `Spielzeit <h>:<mm>` (the game time in
  hours, not limited to a day, and two-digit minutes truncated, digits never culture-formatted; a negative game time has a
  leading `-` before its magnitude; for an hourly mean its largest), then one line per chart line, `<name>: <value>`. Values are formatted as *Number
  formats* of [`web-pages.md`](web-pages.md) says: production and consumption with two decimals, delta with a sign, the
  overview's counts as whole numbers, `–` for no value. MudBlazor draws one hover point per point of a line, which is
  17,280 on a good page with both charts at 2,880 points.
- Times are shown at the UTC offset the browser gives for the receive time of the latest tick, the one the as-of line
  uses ([record 0078](../decisions/0078-times-shown-at-the-browsers-utc-offset.md)); the page asks the browser once for
  each new as-of time of a chart (`censio.utcOffsetMinutes`, [`web-ui.md`](web-ui.md), *Resources*), also for the
  tooltip's time and the axis labels, and a single offset serves the whole range, so a daylight-saving change inside the
  range shifts the hours before it by one hour. An answer that is unusable (a failed call, no number, an offset outside
  -840 to 840 minutes) counts as none: the tooltip's time is then UTC with the suffix ` UTC`, and the axis labels are
  UTC.
- The time axis labels of `Real time` are `HH:mm` at that offset, and `MM-dd HH:mm` when the points span more than one day.
  Their spacing follows the span the points cover: the smallest of 10, 15 and 30 minutes, 1, 2, 3, 6 and 12 hours and 1, 2,
  7, 14, 30, 90 and 365 days that gives at most 8 intervals over the span, and 365 days beyond that (a span of 169 hours: 1
  day); the game-time axis has no labels (*Game-time axis*). One offset serves the whole range, so on `Last 7 days` and
  `All` a daylight-saving change inside the period shows every point before it one hour off, and an offset that is no whole
  hour (+5:30) puts the hourly points between the labels (`:30`). The value axis shows numbers formatted as the
  pages do: two decimals for the good's lines, whole numbers for the overview's counts. The axes carry no title, and the legend sits
  under the chart.
- No number or time is formatted with a culture ([record 0072](../decisions/0072-web-ui-texts-from-an-own-table-without-culture-data.md)).
- **Markup.** MudBlazor writes the labels of its axes as raw markup, so nothing but the number and time texts above ever
  reaches an axis label; the legend and the tooltip pass their texts through the render tree, which escapes them. A name that
  holds markup or script is shown as text on the page, and no name reaches the chart (a unit test pins the axis labels to
  digits, separators and times).

### Colours

- Each chart takes its line colours from the palette of the shown theme ([`web-ui.md`](web-ui.md), *Palette*), which the frame
  hands to the page as it changes: for the good, `Production` is the success colour, `Consumption` the error colour and
  `Delta` the secondary colour; for the overview, `Buildings` is the secondary colour, `Produced goods` the success colour
  and `Goods in deficit` the error colour. The grid and the text take the theme's colours.
- Every line colour reaches 3:1 against the surface in both themes (the pairs of [`web-ui.md`](web-ui.md), *Contrast*, are
  4.5:1), and the three lines of a chart differ in hue by at least 30 degrees, both checked by a unit test. The legend and
  the tooltip name the lines, so the colour is not the only carrier.

### Animation

- A chart's lines are revealed from left to right when the chart is drawn, with a fade from transparent to opaque: the CSS rule
  `.censio-chart path.mud-chart-line` runs the keyframes `censio-chart-reveal` once for 0.8 seconds
  (`clip-path: inset(0 100% 0 0)` and opacity 0 to `inset(0)` and opacity 1) in `wwwroot/css/censio.css`, with no script
  (MudBlazor's chart has none, [record 0086](../decisions/0086-charts-drawn-with-mudblazors-own-time-series-chart.md)).
  A new read re-creates the chart element, so the reveal runs again whenever the points change, which for an open page is
  at every stored tick; a theme or a language switch keeps the element and so does not run it.
- **Reduced motion.** Inside `@media (prefers-reduced-motion: reduce)` the same selector sets `animation: none`, so a
  browser that asks for reduced motion draws the lines at once, fully opaque and without the reveal. This block is the
  only place that honours the browser's reduced-motion setting.

### The reads

- Three reads feed the charts, each of one profile and the range and resolution of the choice: one good on one island (the island chart), one
  good summed per point over the region's islands (the region total), and the figures of one island per point (the overview).
  Each reads the newest 2,880 points and the count of all, never more, and keeps no tick in memory beyond the one it
  folds into a point ([`history.md`](history.md), *The trend reads*). A transaction of a read scans at most 240 ticks or hourly
  points (*Hourly resolution* of that section for the wide ranges), so the ingest waits no longer than before.
- The charts' reads run one at a time across the server, in a gate of their own and not the gate of the pages' profile reader
  ([record 0074](../decisions/0074-pages-keep-derived-views-and-read-unheld-profiles-one-at-a-time.md)), so a long chart read
  never leaves the other pages on `Loading…`. A good page reads its island chart and then its region chart; the second read is
  skipped when the first failed. A choice cancels the earlier choice's read (*Live updates, theme and language*).
- A page reads its charts only when it is interactive. The prerender of the page shows its charts as `Loading…` and reads
  none, so the response of the first request never waits for a chart; the circuit's page then reads them. A page's reads are
  cancelled with the page.
- A page holds, per chart, the points of its read (at most 2,880, three values each) and the series it draws.
- A chart read never holds the clean-up of a deleted profile back: the clean-up waits for the chart read's part in progress,
  and no part starts while it truncates the write-ahead log ([`history.md`](history.md), *Deleting a profile*).

### Logging

A failed chart read logs one warning, `A chart could not be read from the database.`, in the category `Censio.Server.TrendReader`
with the exception and no other argument: no name, value, address or offset. The charts log nothing else.

## Limits and errors

- A chart holds at most 2,880 points; a range with more shows the newest and the cap line.
- **A wide-range read scans every tick of its range.** `Last 7 days` and `All` read the ticks of the range to form each
  hour's mean, so the time of such a read grows with the profile's tick rate and the length of the unfolded range (at most
  the 7 days of full resolution), not with the cap; at the contract's bounds a region read over a week scans about 5,040 ticks.
  The times transaction of an hourly read scans the index of ticks and hourly points of the range once.
- **Time of the reads.** Measured on the sandbox of [`history.md`](history.md) (*Measurements*, a scratch program, three
  runs, an order of magnitude and not a guarantee), at the contract's bounds (256 islands of 512 entries per tick) and
  scaled to 2,880 points: the island chart's read takes 0.2 s, the region total's 3.5 to 4.4 s and the overview's 1.3 to 2.0 s,
  within the limit of 10 seconds, where the pages of the index are in the page cache. With the page cache dropped before the
  read, the region read at the bounds takes 68 to 74 s, the island read 2.9 to 5.4 s and the figures read 3.4 to 4.5 s; the
  region read over 2,880 of 5,040 ticks of the fixture (24 islands) takes 0.17 to 0.20 s warm and 1.0 s with the page
  cache dropped. A profile with a region of 256 islands whose index pages are not cached can therefore wait longer than
  10 seconds for its region chart, which shows `Loading…` meanwhile; the reads of the other pages are not held, because the
  chart reads have their own gate.
- **Time of the wide-range reads** (measured on the sandbox of [`history.md`](history.md), three runs, an order of magnitude
  and not a guarantee, by a scratch program at the contract's bounds of 256 islands of 512 entries per tick). Region read,
  60 ticks from a file database: 77 to 125 ms warm and 1.36 to 1.52 s with the page cache dropped for the hourly read (the
  tick-by-tick read took 75 to 143 ms warm and 1.32 to 1.47 s cold), about 23 to 25 ms a tick cold, so a week of 5,040 ticks
  takes about two minutes cold (114 to 128 s, scaled, not measured whole); the figures read of one island 39 to 62 ms warm and
  71 to 103 ms cold for the same 60 ticks. The `Hours` times transaction on `All` over 8,684 hourly points and 5,040 ticks took
  6.2 to 29.0 ms, over 50,000 ticks 28.3 to 49.1 ms. A region chart over a week of a region of 256 islands with a cold cache
  can therefore keep `Loading…` for about two minutes while the other chart reads wait in the trend gate.
- On the in-memory database every read takes the write gate ([`server-data.md`](server-data.md)); a chart read takes it for one
  part of at most 240 points at a time, measured at 0.6 s at most at the bounds (a read of 20 ticks scaled to 240); an hourly part
  of 240 ticks at the bounds held the write gate for 0.27 to 0.30 s (region) and 0.16 to 0.20 s (figures) in three runs on the
  in-memory database, against about 3.9 s of a fold step, so the ingest acknowledges within the Companion's 30 seconds
  ([`replay-sending.md`](replay-sending.md), *Failures*).
- **Memory and size of a page.** Measured with a scratch program that renders the page's two charts through the server's own
  chart component and a Blazor renderer, with 2,880 points a line and the browser's offset answered at once: the managed heap
  holds about 53.3 MB for the two charts after a collection (0.9 MB of it the points of the two reads, the rest the render
  tree, the series and the chart's components). The render batch that draws a chart is 3,353,385 bytes (about 3.2 MiB), one
  for each of the two charts; with 100 points a chart's batch is 120,576 bytes. The memory
  of open pages is proportional to their number, which is not limited, as for the pages of [`web-pages.md`](web-pages.md).
- A failed read, a lost connection and a browser that never answers the offset are the cases of *States of a chart* and
  *Tooltip, axes and times*; none shows an exception text.

## Related decisions

- [`decisions/0086-charts-drawn-with-mudblazors-own-time-series-chart.md`](../decisions/0086-charts-drawn-with-mudblazors-own-time-series-chart.md) — the charts are MudBlazor's own time-series chart, with a reveal of our own style rule for the animation.
- [`decisions/0087-chart-reads-aggregate-per-point-bounded-in-parts-with-a-product-index.md`](../decisions/0087-chart-reads-aggregate-per-point-bounded-in-parts-with-a-product-index.md) — the reads aggregate per point, read the newest 2,880 points in parts and use an index of tick entries by good.
- [`decisions/0088-charts-show-the-day-up-to-the-latest-tick-with-gaps-after-15-minutes.md`](../decisions/0088-charts-show-the-day-up-to-the-latest-tick-with-gaps-after-15-minutes.md) — the default range is the day up to the latest tick, and a line breaks after 15 minutes without a point.
- [`decisions/0089-chart-ranges-wide-ones-as-hourly-means-read-at-request.md`](../decisions/0089-chart-ranges-wide-ones-as-hourly-means-read-at-request.md) — four ranges kept in the browser; the wide ones show hourly means computed when read, and a new tick reads only the newest hour.
- [`decisions/0090-game-time-axis-places-points-without-time-labels.md`](../decisions/0090-game-time-axis-places-points-without-time-labels.md) — the game-time axis places points at their game time without time labels.

## Not here

- The pages' frame, routes, number and time formats, the as-of line and the reload text: [`web-pages.md`](web-pages.md), [`web-ui.md`](web-ui.md).
- The stored history, the trend reads' SQL-level rules, the trend gate and the measurements of the reads: [`history.md`](history.md), [`server-data.md`](server-data.md).
- Sparklines and trend arrows (#38); a range of the user's own choosing, a range per chart and time labels on the game-time axis; other values, a region's goods list and deficits (#47, #49 to #51); the chart toolbar (zoom, download) and several goods in one chart; the phone layout (#53).
