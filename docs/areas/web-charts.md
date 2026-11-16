# Web charts

## Scope

The trend charts of the data pages: the two charts of a good's page and the overview chart of an island's page, the
range they show, the points and their gaps, hourly means, the region total, the cap of 2,880 points, the tooltip and the
axes, the colours and the animation, the reads that feed them with their bounds and failures, and the chart texts in
English and German. Users of the web application rely on it; the pages around the charts are in
[`web-pages.md`](web-pages.md), the history the charts read in [`history.md`](history.md).

## Behavior

### Where the charts are

- **The good's page** opens at `/profiles/{profile}/islands/{session}/{island}/{area}/goods/{good}` (the island's address
  of [`web-pages.md`](web-pages.md) and the good's product GUID). Every good name in the goods table of an island is a
  link to it. The heading is the good's shown name (a catalog name in the shown language, else in the other language,
  else `Good <GUID>` / `Ware <GUID>`); the tab title is `Censio – <heading>`. Under it, `<island> · <region> · <profile>`
  with the island a link to its detail and the region a link to its island list, then the banner and the as-of line as on
  every data page, the range line (below) and two charts:

  | Chart | English | German | Lines |
  | ----- | ------- | ------ | ----- |
  | The island's good | `This island` | `Diese Insel` | `Production`, `Consumption`, `Delta` / `Produktion`, `Verbrauch`, `Delta` |
  | The good in the region | `All islands of the region` | `Alle Inseln der Region` | the same three lines, each value the sum over the islands of the island's region ([record 0073](../decisions/0073-islands-grouped-by-the-catalog-region-of-their-session.md)) that carry a value at that point |

  The region's islands are those its island list shows ([`web-pages.md`](web-pages.md), *Regions*).
- **The island's overview** is the chart `Overview` / `Übersicht` on the island's page, between the as-of line and the
  goods table, with the range line under its heading. Its lines are `Buildings`, `Produced goods` and `Goods in deficit`
  (German `Gebäude`, `Produzierte Waren`, `Waren im Defizit`, the table columns). It shows also for an island the latest
  tick lacks, from the history in the range.
- The good segment of the address is parsed like the session segment: an integer of at most ten ASCII digits with an
  optional leading minus. An address whose profile, island or good segment is malformed, or whose profile or island is
  unknown, shows `Not found.` as the other data pages do; so does a profile deleted while the page is open. A well-formed
  good the island never carried opens its page with the empty-chart text (*States of a chart*).
- The range line is `Last 24 hours up to the latest tick` / `Letzte 24 Stunden bis zum letzten Tick`. Every chart text is
  a fixed text of `UiTexts`; no name is a line name, heading or note ([`web-ui.md`](web-ui.md), *Texts*).

### Range

- A chart shows the points from the start of the UTC hour that holds the time 24 hours before the receive time of the
  profile's latest tick, up to and including that tick. So the hourly point of that hour is shown whole, and a replayed or
  idle profile shows its last day, whatever the server's clock says ([record 0088](../decisions/0088-charts-show-the-day-up-to-the-latest-tick-with-gaps-after-15-minutes.md)).
  There is no setting.
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

### States of a chart

| Case | English | German |
| ---- | ------- | ------ |
| a chart without any value in the range | `No values in this period.` | `Keine Werte in diesem Zeitraum.` |
| a profile without a stored tick | `No data yet. …` of [`web-pages.md`](web-pages.md), instead of the charts | the German text there |
| the charts' read has not answered, or the page is prerendered, or the browser's offset has not answered for the first drawing | `Loading…` | `Wird geladen…` |
| a failed read | the reload text of [`web-pages.md`](web-pages.md), for every chart of the page | the German text there |

- A page whose read failed shows the reload text in every chart and reads no chart again until it is loaded again; a
  notice of the server state does not retry it. A cancelled read (the page is disposed or its address changes) shows and
  logs nothing.
- A chart keeps the series it draws when a new read for the same address arrives, until the browser has answered the
  offset for the new read's as-of time, then replaces it. A change of the address shows `Loading…` until its read has
  answered.

### Live updates, theme and language

- A stored tick of the page's profile gives every chart a new read without a reload, and the range moves with the latest
  tick; a tick of another profile changes nothing. A notice during a read causes one more read afterwards
  ([`web-pages.md`](web-pages.md), *Live updates*).
- A theme or language switch redraws the charts from the series they hold, without a read: the line names, texts and
  value formats follow the language at once, the colours follow the theme.

### Tooltip, axes and times

- Hovering a point shows a tooltip drawn as SVG text: the point's time as `yyyy-MM-dd HH:mm`, for an hourly point the line
  `Hourly average` / `Stundenmittel`, then one line per chart line, `<name>: <value>`. Values are formatted as *Number
  formats* of [`web-pages.md`](web-pages.md) says: production and consumption with two decimals, delta with a sign, the
  overview's counts as whole numbers, `–` for no value. MudBlazor draws one hover point per point of a line, which is
  17,280 on a good page with both charts at 2,880 points.
- Times are shown at the UTC offset the browser gives for the receive time of the latest tick, the one the as-of line uses
  ([record 0078](../decisions/0078-times-shown-at-the-browsers-utc-offset.md)); the page asks the browser once for each new
  as-of time of a chart (`censio.utcOffsetMinutes`, [`web-ui.md`](web-ui.md), *Resources*), also for the tooltip's time and
  the axis labels, and a single offset serves the whole range, so a daylight-saving change inside the 24 hours shifts the
  hours before it by one hour. An answer that is unusable (a failed call, no number, an offset outside -840 to 840 minutes)
  counts as none: the tooltip's time is then UTC with the suffix ` UTC`, and the axis labels are UTC.
- The time axis labels are `HH:mm` at that offset. Their spacing follows the span the points cover: the smallest of 10, 15
  and 30 minutes and 1, 2 and 3 hours that gives at most 8 intervals, else 3 hours. The value axis shows numbers formatted as the
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
  browser that asks for reduced motion draws the lines at once, fully opaque and without the reveal. The browser's
  reduced-motion setting is honoured only by this one block; the spec of the feature listed it as out of scope, and the block
  was added with the animation.

### The reads

- Three reads feed the charts, each of one profile and the range of its chart: one good on one island (the island chart), one
  good summed per point over the region's islands (the region total), and the figures of one island per point (the overview).
  Each reads the newest 2,880 points and the count of all, never more, and keeps no tick in memory beyond the one it
  folds into a point ([`history.md`](history.md), *The trend reads*).
- The charts' reads run one at a time across the server, in a gate of their own and not the gate of the pages' profile reader
  ([record 0074](../decisions/0074-pages-keep-derived-views-and-read-unheld-profiles-one-at-a-time.md)), so a long chart read
  never leaves the other pages on `Loading…`. A good page reads its island chart and then its region chart; the second read is
  skipped when the first failed.
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
- **Time of the reads.** Measured on the sandbox of [`history.md`](history.md) (*Measurements*, a scratch program, three
  runs, an order of magnitude and not a guarantee), at the contract's bounds (256 islands of 512 entries per tick) and
  scaled to 2,880 points: the island chart's read takes 0.2 s, the region total's 3.5 to 4.4 s and the overview's 1.3 to 2.0 s,
  within the limit of 10 seconds, where the pages of the index are in the page cache. With the page cache dropped before the
  read, the region read at the bounds takes 68 to 74 s, the island read 2.9 to 5.4 s and the figures read 3.4 to 4.5 s; the
  region read over 2,880 of 5,040 ticks of the fixture (24 islands) takes 0.17 to 0.20 s warm and 1.0 s with the page
  cache dropped. A profile with a region of 256 islands whose index pages are not cached can therefore wait longer than
  10 seconds for its region chart, which shows `Loading…` meanwhile; the reads of the other pages are not held, because the
  chart reads have their own gate.
- On the in-memory database every read takes the write gate ([`server-data.md`](server-data.md)); a chart read takes it for one
  part of at most 240 points at a time, measured at 0.6 s at most at the bounds (a read of 20 ticks scaled to 240), against about
  3.9 s of a fold step, so the ingest acknowledges within the Companion's 30 seconds
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
- [`decisions/0088-charts-show-the-day-up-to-the-latest-tick-with-gaps-after-15-minutes.md`](../decisions/0088-charts-show-the-day-up-to-the-latest-tick-with-gaps-after-15-minutes.md) — the range is the day up to the latest tick, and a line breaks after 15 minutes without a point.

## Not here

- The pages' frame, routes, number and time formats, the as-of line and the reload text: [`web-pages.md`](web-pages.md), [`web-ui.md`](web-ui.md).
- The stored history, the trend reads' SQL-level rules, the trend gate and the measurements of the reads: [`history.md`](history.md), [`server-data.md`](server-data.md).
- A range picker, the game-time axis, sparklines and trend arrows (#37, #38); other values, a region's goods list and deficits (#47, #49 to #51); the chart toolbar (zoom, download) and several goods in one chart; the phone layout (#53).
