# Spec: Trend charts per good, island and region

Source: Issue #36
Status: Approved by Lead

## Problem

The server stores every profile's history (#35, [`history.md`](../../docs/areas/history.md)), but the pages show only
the latest tick, so a player cannot see how a good, an island or a region develops. Checked: `HistoryStore.ReadSeriesAsync`
has no caller outside the tests, and no chart package is referenced.

## Scenarios

- S1: Given a profile with stored ticks, when the user clicks a good's name in the goods table of an island
  (`/profiles/{profile}/islands/{session}/{island}/{area}`), then the good's page opens at
  `/profiles/{profile}/islands/{session}/{island}/{area}/goods/{good}`: heading the good's shown name, under it
  `<island> · <region> · <profile>` (island a link to the island detail, region to its island list), the as-of line and
  banner as on every data page, the line `Last 24 hours up to the latest tick` / `Letzte 24 Stunden bis zum letzten
  Tick`, and the chart `This island` / `Diese Insel` with the lines `Production`, `Consumption`, `Delta` /
  `Produktion`, `Verbrauch`, `Delta` over real time (receive time).
- S2: Below it the same page shows the chart `All islands of the region` / `Alle Inseln der Region`: the same three lines,
  each value the sum over the islands of the island's region (record 0073) that carry a value at that point.
- S3: The island detail shows, above its goods table, the chart `Overview` / `Übersicht` with the lines `Buildings`,
  `Produced goods`, `Goods in deficit` (German as the table columns). A tick's point is computed as *Key figures* of
  `web-pages.md` computes them for the latest tick; an hourly point's from the hour's means of each good: buildings the
  sum of the mean building counts rounded to a whole number (midpoint away from zero), produced goods the goods with a
  mean building count above 0, goods in deficit the goods whose mean delta, rounded as *Number formats* says, is below 0.
- S4: Given an open chart page, when a tick of its profile is stored, then every chart shows the new point without a
  reload, and the range moves with it.
- S5: The charts animate when first drawn and when their points change, and take the colours of the shown theme; a theme
  or language switch recolours or retranslates them at once, without a reload.
- S6: Hovering a point shows a tooltip: the time as `yyyy-MM-dd HH:mm` at the browser's UTC offset for the latest
  tick (the as-of line's offset; without a usable answer in UTC with ` UTC`), `Hourly average` /
  `Stundenmittel` for an hourly point, and each line's value formatted as *Number formats* of `web-pages.md` (delta
  signed, the overview's counts whole), `–` for no value. The time axis's labels, `HH:mm`, use the same offset.
- S7: Where data is missing the line is interrupted, never drawn to zero: at a point without the line's value, and
  between two points when the later starts more than 15 minutes after the earlier ends (a tick ends at its time, an
  hourly point one hour after its start). In the fixture as stored, before any fold, the 18-minute pause is such a gap
  and the 9-minute session change is not.
- S8: Given a replayed recording older than 7 days, once the retention job has run, its charts show the folded hours as
  hourly means joined to each other and to the ticks of the latest hour; a pause inside an hour no longer shows.

## Errors and edge cases

- E1: An address whose profile, island or good segment is malformed, or whose profile or island is unknown, shows
  `Not found.` as the other data pages do; the good segment is parsed like the session segment (an integer of at most
  ten ASCII digits, optional leading minus). A well-formed good the island never carried opens its page with the
  empty-chart text (E2), named `Good <GUID>` / `Ware <GUID>` unless the catalog names it.
- E2: A chart without any value in the range shows `No values in this period.` / `Keine Werte in diesem Zeitraum.`
  instead of the chart.
- E3: A profile without a stored tick shows `No data yet. …`; a read waiting behind another shows `Loading…`; a failed
  read replaces the charts with the reload text of `web-pages.md`, not retried until the page is loaded again; a
  cancelled read shows and logs nothing.
- E4: A profile idle for more than 7 days has hourly points in its range: they are drawn as the hourly means the series
  read returns; two consecutive hours are joined, a missing hour is a gap (S7); an hour with an hourly point and
  late ticks shows both, the hourly point first.
- E5: In the region total an island without the value at a point adds nothing; a point no island carries the value
  for is no value (a gap). The region's islands are those the island list shows today.
- E6: A chart whose range holds more than 2,880 points shows the newest 2,880 and the line `Showing the newest 2,880 of
  <n> points.` / `Die neuesten 2.880 von <n> Punkten angezeigt.` under it.
- E7: Island, region, profile and good names holding markup or script (`<img src=x onerror=…>`) are shown as text
  everywhere on the page, the chart's tooltip, legend and labels included.
- E8: A profile deleted while its chart page is open shows `Not found.`, as the other data pages do.
- E9: An island the latest tick lacks still shows its charts from the history in the range.

## Non-functional requirements

- N1: Range: from the start of the UTC hour that holds the time 24 hours before the receive time of the profile's
  latest tick, up to and including that tick, so the hourly point of that hour is shown whole and a replayed or idle
  profile shows its last day. No setting.
- N2: Reads: three reads, each of one profile and the range — one good on one island (the island chart), one good summed
  per point over the region's islands (the region total), the overview figures per point of one island (the overview).
  Each reads at most the newest 2,880 points and the count of all, never more (E6), and keeps no tick in memory beyond
  the one it folds into a point. The charts' reads run one at a time across the server, through a gate of their own,
  not the pages' profile reader (record 0074), so a long chart read never leaves the other pages on `Loading…`; a
  page's reads are cancelled with the page.
- N3: Time: at the contract's bounds (256 islands of 512 entries per tick) each read of N2 over 2,880 points finishes
  within 10 seconds, measured as `history.md` (*Measurements*) measures on ticks at the bounds and scaled to 2,880. On the in-memory database, where every read
  takes the write gate, a chart read holds that gate no longer than a fold step at the bounds does (about 4 seconds),
  so the ingest still acknowledges within the Companion's 30 seconds (record 0068).
- N4: Safety: no name reaches the chart as markup (E7); line names, headings and notes are fixed texts of `UiTexts`.
- N5: Library and resources: the charts are MudBlazor's own (Q7), so no chart package, script or style beyond MudBlazor's
  is added and nothing is loaded from another site ([`web-ui.md`](../../docs/areas/web-ui.md), *Resources*). The gap of
  S7 and the animation of S5 stay required; the plan verifies that MudBlazor's chart draws both and, if it does not,
  returns the question to the Product Manager before building (record 0025's chart part superseded, record 0028).
- N6: Languages: every chart text in English and German from `UiTexts`; no number or time formatted with .NET culture
  data (record 0072); times at the browser's UTC offset for the latest tick (record 0078).
- N7: Look: the chart's text, grid and lines take the theme's palette; each line colour reaches 3:1 against the surface
  in both themes (the *Lines* level of `web-ui.md`, *Contrast*). Desktop browsers only.
- N8: Logging: the charts log nothing of their own; a failed read is logged without a name or value, as
  `web-pages.md` (*Logging*) says.

## Acceptance criteria

AC1 to AC3 check the history as stored by the replay, before any retention run.

- AC1: With `long-session.jsonl` replayed into the server, the island chart of each good of an island holds one point
  per stored tick with the tick's receive time and the good's production, consumption and delta of that tick (no value
  where the tick lacks it); it is interrupted at the 18-minute pause and at points without a value, and joined across
  the session change (S1, S7).
- AC2: With the same replay, each region-total point equals the sum over the region's islands that carry the value,
  and is no value where none does (S2, E5).
- AC3: With the same replay, each overview point of an island equals the key figures of `web-pages.md` computed for
  that tick, no value where the tick lacks the island; for an hourly point built in the test, the figures follow S3's
  hourly rules (S3, Q1).
- AC4: With the same replay after a retention run on a clock past 7 days, the island chart holds the hourly points of
  22:00 and 23:00 UTC with the means of their ticks, then the ticks from 00:00, all joined (S8, E4).
- AC5: The good page answers its address with the heading, the line under it with its two links, the tab title
  `Censio – <good>`, the range line and both chart headings; every good name in the goods table links to its page;
  malformed and unknown addresses show `Not found.` (S1, E1, E8).
- AC6: With ticks and hourly points built in the test, the range holds exactly the points of N1 (the hour that holds the
  time 24 hours before the latest tick included, the hour before not); hourly points are drawn as means, joined across
  consecutive hours and interrupted at a missing hour (N1, E4, E9).
- AC7: `No values in this period.`, `No data yet. …`, `Loading…`, the reload text and the cap line of E6 appear as E2,
  E3 and E6 say, in both languages (E2, E3, E6).
- AC8: A stored tick of the page's profile updates every chart without a reload; a tick of another profile changes
  nothing; a notice during a read causes one more read; a theme or language switch redraws without a read (S4, S5).
- AC9: The tooltip and the axis labels show the time and values as S6 says, in both languages (S6, N6).
- AC10: The chart options carry the theme's palette, with every line colour at 3:1 or more against the surface in both
  themes (checked by a unit test), and animation switched on (S5, N7).
- AC11: A name holding markup appears on the page and in the chart as text only, checked with a name of E7 (E7, N4).
- AC12: The server adds no chart package, script or style beyond MudBlazor's, and a chart page loads resources from
  the server's own paths only (N5).
- AC13: Each read of N2 reads at most 2,880 points; the reads run one at a time on their own gate and end when the page
  is disposed; their time at the contract's bounds and the write gate's hold on the in-memory database are measured
  against N3 (N2, N3).
- AC14: manual (Product Manager, on a build of the branch, the fixture replayed after the server's first retention run
  and checked before the next, the log line `History reduced …` absent since the replay): in the light and the dark
  theme the charts look finished, animate, show the pause as a gap and the tooltip; a page logs nothing (S5, S6, S7, N8).
- AC15: README and user guide, English first, then German, describe the good page, the overview chart, the range, the
  gaps, the hourly means and that an old recording shows as hourly means after the next retention run, and the README's
  "no trend charts" line is gone.

## Out of scope

- Range picker and the game-time axis: #37 (record 0022). Sparklines and trend arrows: #38.
- Other values (perfect generation, productivity, income, maintenance, workforce): #49 to #51; a region's goods list
  and deficits: #47; the phone layout: #53.
- The chart toolbar (zoom, download), several goods in one chart, and honouring the browser's reduced-motion setting.

## Open questions

- Q1: What does the "island overview" show? Answered: the island's key figures (S3), by the Product Manager.
- Q2: Which range before #37? Answered: N1 (Lead); it works for replays and idle profiles.
- Q3: When is a gap a gap? Answered: S7 (Lead); ticks come about every 2 minutes, so 15 minutes is seven missed.
- Q4: Do the charts update live? Answered: yes (S4), like every data page of #32.
- Q5: Where are region totals reached? Answered: on the good's page (S2), no goods list before #47.
- Q6: Which values? Answered: production, consumption, delta, as the issue names them.
- Q7: Which chart library, now that ApexCharts is no longer MIT (Challenge M1)? Answered: MudBlazor's own charts, by
  the Product Manager, instead of the issue's Blazor-ApexCharts; a new record supersedes record 0025's chart part (N5).
- Q8: MudBlazor's chart has no animation (plan, fact 4). Options: (a) a style rule of our own reveals each line from
  left to right when a chart is drawn and on each new read, no script; (b) no animation, S5 and AC10 lose it; (c) (a)
  on the first drawing only. Escalated to the Product Manager; recommendation (a).

## Area documents

- `web-pages.md` (or a new `web-charts.md` that #37 and #38 extend, the plan decides): the good page, its links and
  states, the overview chart, the range, the gap rule, hourly means, the region total, the cap, the reads and bounds.
- `web-ui.md`: the chart texts, colours and contrast; *Resources* unchanged but for the charts' mention.
- `history.md`: *Scope* and *Not here* point to the charts; the new reads join *The series read*.

## Challenge

- M1 (ApexCharts no longer MIT) — accepted, escalated as Q7. Verified in the packages: wrapper 8.0.0 ships ApexCharts
  8.8.1 under the "ApexCharts License" (free only under $2M revenue, OEM licence for embedding in a product used by
  others) and calls `ApexCharts.setLicense`; wrapper 6.0.2 ships the MIT 4.7.0 for net8.0 and net9.0 only. N5 and AC12
  now use MudBlazor's charts (Q7).
- M2 (retention folds the fixture) — accepted. Verified: receive times 22:54:38 to 00:21:36 UTC, pause inside hour 23.
  AC1 to AC3 check the history before any fold; S8 and AC4 add the folded replay; AC14 and AC15 follow.
- M3 (reads unbounded) — accepted; verified `HistorySeriesQuery` names one good and `StoreAccess.ReadAsync` takes the
  write gate in memory. N2 names and bounds the three reads, N3 their time and gate hold, AC13 checks both.
- Minor 1 — accepted: S3 defines the hourly figures, AC3 covers one. Minor 2 — accepted: own gate (N2). Minor 3 —
  accepted: axis labels (S6, N6). Minor 4 — accepted: threshold 15 minutes, clear of 9.4 and 18.3; AC1 asserts the
  join. Minor 5 — accepted: AC6 builds its data. Minor 6 — accepted: N1 starts at the hour 24 hours back.
