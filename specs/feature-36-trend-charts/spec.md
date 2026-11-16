# Spec: Trend charts per good, island and region

Source: Issue #36
Status: Draft

## Problem

The server stores every profile's history (#35, [`history.md`](../../docs/areas/history.md)), but the pages show only
the latest tick ([`web-pages.md`](../../docs/areas/web-pages.md), *Key figures*; README: "there are no trend charts").
A player cannot see whether a good's production keeps up with consumption over time, how an island develops, or what a
whole region makes of a good. Checked against the code: `HistoryStore.ReadSeriesAsync` (one good on one island) has no
caller outside the tests, and no chart package is referenced (`Directory.Packages.props` holds MudBlazor only).

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
  `Produced goods`, `Goods in deficit` (German as the table columns), each point computed as *Key figures* of
  `web-pages.md` computes it for the latest tick (Q1).
- S4: Given an open chart page, when a tick of its profile is stored, then every chart shows the new point without a
  reload, and the 24-hour range moves with it.
- S5: The charts animate when first drawn and when their points change, and take the colours of the shown theme; a theme
  or language switch recolours or retranslates them at once, without a reload.
- S6: Hovering a point shows a tooltip: the time as `yyyy-MM-dd HH:mm` in the browser's time zone, `Hourly average` /
  `Stundenmittel` for an hourly point, and each line's value formatted as *Number formats* of `web-pages.md` (delta
  signed, counts whole), `–` for no value.
- S7: Where data is missing the line is interrupted, never drawn to zero: at a point without the line's value, and
  between two points when the later starts more than 10 minutes after the earlier ends (a tick ends at its time, an
  hourly point one hour after its start). The fixture's 18-minute pause shows as such a gap.

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

- N1: Range: the points whose time lies from 24 hours before the receive time of the profile's latest tick up to and
  including that tick, so a replayed or idle profile shows its last day, not an empty chart. No setting.
- N2: Reads: the charts read the history only, bounded by N1 and E6; their history reads run one at a time across the
  server, like the reads of profiles that are not held (record 0074), and a page's reads are cancelled with the page.
  A page keeps only the points it draws.
- N3: Safety: no name reaches the chart as markup (E7); line names, headings and notes are fixed texts of `UiTexts`.
- N4: Resources: the chart's script and styles are served by the server itself; no CDN or other external request
  ([`web-ui.md`](../../docs/areas/web-ui.md), *Resources*). The chart package's licence allows its use in this MIT
  project (record 0025).
- N5: Languages: every chart text in English and German from `UiTexts`, numbers without .NET culture data (record 0072);
  times in the browser's time zone (record 0078).
- N6: Look: the chart's text, grid and lines take the theme's palette; each line colour reaches 3:1 against the surface
  in both themes (the *Lines* level of `web-ui.md`, *Contrast*). Desktop browsers only.
- N7: Logging: the charts log nothing of their own; a failed read is logged without a name or value, as
  `web-pages.md` (*Logging*) says.

## Acceptance criteria

- AC1: With `long-session.jsonl` replayed into the server, the island chart of each good of an island holds one point
  per stored tick with the tick's receive time and the good's production, consumption and delta of that tick (no value
  where the tick lacks it), and is interrupted exactly at the 18-minute pause and at points without a value (S1, S7).
- AC2: With the same replay, each region-total point equals the sum over the region's islands that carry the value,
  and is no value where none does (S2, E5).
- AC3: With the same replay, each overview point of an island equals the key figures of `web-pages.md` computed for
  that tick, no value where the tick lacks the island (S3, Q1).
- AC4: The good page answers its address with the heading, the line under it with its two links, the tab title
  `Censio – <good>`, the range line and both chart headings; every good name in the goods table links to its page;
  malformed and unknown addresses show `Not found.` (S1, E1, E8).
- AC5: The range holds exactly the points of N1, the tick 24 hours before the latest included; hourly points are drawn
  as means, joined across consecutive hours and interrupted at a missing hour (N1, E4, E9).
- AC6: The page states `No values in this period.`, `No data yet. …`, `Loading…`, the reload text and the cap line of
  E6 appear as E2, E3 and E6 say, in both languages (E2, E3, E6).
- AC7: A stored tick of the page's profile updates every chart without a reload; a tick of another profile changes
  nothing; a notice during a read causes one more read; a theme or language switch redraws without a read (S4, S5).
- AC8: The tooltip shows the time and values as S6 says, in both languages (S6, N5).
- AC9: The chart options carry the theme's palette, with every line colour at 3:1 or more against the surface in both
  themes (checked by a unit test), and animation switched on (S5, N6).
- AC10: A name holding markup appears on the page and in the chart as text only, checked with a name of E7 (E7, N3).
- AC11: The page loads the chart's script and styles from the server's own paths only, and the image serves them
  (N4).
- AC12: The charts' history reads run one at a time across the server and end when the page is disposed (N2).
- AC13: manual (Product Manager, on a build of the branch with the fixture replayed): in the light and the dark theme
  the charts look finished, animate, show the pause as a gap and the tooltip; a page logs nothing (S5, S6, S7, N7).
- AC14: README and user guide, English first, then German, describe the good page, the overview chart, the range, the
  gaps and the hourly means, and the README's "no trend charts" line is gone.

## Out of scope

- Range picker and the game-time axis: #37 (record 0022). Sparklines and trend arrows: #38.
- Other values (perfect generation, productivity, income, maintenance, workforce): #49 to #51; a region's goods list
  and deficits: #47; the phone layout: #53.
- The chart toolbar (zoom, download), several goods in one chart, and honouring the browser's reduced-motion setting.

## Open questions

- Q1: What does the issue's "island overview" chart show? Options: (a) the island's key figures over time (buildings,
  produced goods, goods in deficit) on the island detail, as S3 and AC3 assume; (b) one line per island of the region
  for one key figure on the island list, to compare islands; (c) a small chart per good in the goods table, which #38's
  sparklines cover; (d) none in #36. Answered: (a), by the Product Manager ("Kennzahlen der Insel") — S3 and AC3.
- Q2: Which range without #37's picker? Answered: N1, by the Lead — a fixed 24 hours up to the latest tick shows the
  recent detail and works for replays and idle profiles; #37 replaces it.
- Q3: When is a gap a gap? Answered: S7, by the Lead — ticks come about every 2 minutes (in the fixture all but three
  spacings lie from 1.8 to 2.8 minutes), so 10 minutes is five missed ticks; the fixture's 18-minute pause is one, its 8-minute session
  change is not.
- Q4: Do the charts update live? Answered: yes (S4), like every data page of #32.
- Q5: Where are region totals reached? Answered: on the good's page under the island chart (S2), so no region goods
  list is needed before #47.
- Q6: Which values? Answered: production, consumption and delta, the issue's "generation vs consumption and delta".

## Area documents

- `web-pages.md`: the good page's route, heading, links and states; the link on each good name; the overview chart;
  the range, the gap rule, hourly means, the region total, the cap and the bounded reads; *Not here* no longer names
  #36. The plan decides whether the chart rules go into a new area `web-charts.md` that #37 and #38 extend.
- `web-ui.md`: the chart texts in *Texts*, the chart colours under *Palette* and *Contrast*, the chart script under
  *Resources*.
- `history.md`: *Scope* and *Not here* point to the charts' document for how time without points is drawn.
