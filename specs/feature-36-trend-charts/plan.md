# Plan: Trend charts per good, island and region

Source: Issue #36 | [spec.md](spec.md)
Tier: standard — new pages, reads and an index that change behavior and create, change or remove no security control.
Security areas touched: Network exposure (a new data page under `/profiles/…`); Server database (two history reads, an index migration in `src/Censio.Server.Data`); Parsing of external input (`PageAddress` good segment, the browser's offset moved from `DataStatus` to `BrowserOffset`); Logging and showing external data (names in charts, one new log line)
Plan size: three task groups over two projects, a migration, a new page, a component and their measurements.

## Problem / root cause

Feature: [spec.md](spec.md), AC1 to AC15.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | MudBlazor 9.13.0 has a time-series chart (`MudChart`, `ChartType.Timeseries`, `TimeSeriesChartOptions`) | MudBlazor `v9.13.0` source, `Components/Chart/Charts/TimeSeries.razor(.cs)`, `MudChart.razor:107` | confirmed |
| 2 | It breaks a line at a point whose `ChartPoint.HasValue` is false (`ConnectNullPoints` default false) | `Base/MudAxisLineChartBase.cs:296-303`, `Base/DefaultAxisLineChartOptions.cs:60` | confirmed |
| 3 | A point without value still counts for the value axis range (`Y` of every point) | `Charts/TimeSeries.razor.cs` `GetYRangeWithPadding` | confirmed |
| 4 | It has no animation; the only transition is a 0.2 s stroke colour change | `Styles/components/_charts.scss:140-143`, no `animat` in `Components/Chart` | confirmed |
| 5 | Axis labels are `DateTime.ToString(TimeLabelFormat)`, tooltip labels `ToString(TooltipTimeLabelFormat)` in the current culture; value labels take `YAxisToStringFunc` | `TimeSeries.razor.cs:345-365`, `Base/DefaultAxisChartOptions.cs:69` | confirmed |
| 6 | Tooltip, legend and labels are Blazor render output (text escaped); `TooltipTemplate` takes `RenderFragment<(SvgPath, string)>` | `TimeSeries.razor:160-215`, `Base/MudChartBase.cs:72` | confirmed |
| 7 | `ChartData<T>.Points` holds mutable `ChartPoint<T>` with a settable `HasValue` | `Models/ChartData.cs:52`, `Models/ChartPoint.cs` | confirmed |
| 8 | ApexCharts licence facts of the spec's M1 | spec *Challenge* M1, packages read | confirmed |
| 9 | `ReadSeriesAsync` has no caller outside tests; one good on one island | `src/Censio.Server.Data/HistoryStore.cs:237`, grep | confirmed |
| 10 | Tick entries are indexed only by (island, position); hourly entries by (island, good) | `CensioDbContext.cs` `TickEntryRow`, `HourlyEntryRow` | confirmed |
| 11 | On the in-memory database every read takes `ServerDatabase.Gate` | `StoreAccess.cs:127-140` | confirmed |
| 12 | The fixture's ticks end 22:54:38 to 00:21:36 UTC (2026-06-10/11), pause 23:08:43 to 23:27:03, session change 00:12:12 to 00:21:36 | scratch script over `long-session.jsonl`; tick receive time is its last island frame (`pipe.md`) | confirmed |
| 13 | The page knows no theme today; `MainLayout` cascades only language and texts | `MainLayout.razor:38-42` | confirmed |
| 14 | `PageFormat` formats `float?` only and rounds with `Math.Round(x, 2, AwayFromZero)` | `PageFormat.cs` | confirmed |
| 15 | A CSS reveal of the chart's line paths (`clip-path: inset` keyframes) runs in current desktop browsers | to verify: Product Manager in AC14 on the branch build | unverified |
| 16 | The trend reads meet N3 at the bounds; the index costs per stored entry and the migration's build time | to verify: Dev, scratch program in step 6, as `history.md` *Measurements* | unverified |

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `TrendReaderFixtureIslandGoodMatchesStoredTicks`, `TrendSeriesBuilderFixturePauseBreaksSessionChangeJoins` | `tests/Censio.Server.Tests/TrendReaderTests.cs`, `tests/Censio.Server.Tests/TrendSeriesBuilderTests.cs` |
| AC2 | `TrendReaderFixtureRegionTotalIsSumOfCarryingIslands`, `HistoryStoreGoodTrendPointWithoutCarrierHasNoValue` | `TrendReaderTests.cs`, `tests/Censio.Server.Data.Tests/HistoryStoreTests.cs` |
| AC3 | `TrendReaderFixtureFiguresMatchKeyFiguresPerTick`, `HistoryStoreFiguresTrendHourlyPointFollowsHourlyRules` | `TrendReaderTests.cs`, `HistoryStoreTests.cs` |
| AC4 | `TrendReaderFixtureAfterRetentionShowsJoinedHourlyMeans` | `TrendReaderTests.cs` |
| AC5 | `GoodTrendShowsHeadingLinksAndChartHeadings`, `GoodTrendMalformedOrUnknownAddressShowsNotFound`, `IslandDetailGoodNameLinksToGoodPage`, `PageAddressTryParseGoodAcceptsSessionForm`, `PageAddressGoodLinkCarriesProfileIslandAndGood` | `tests/Censio.Server.Tests/GoodTrendTests.cs`, `IslandDetailTests.cs`, `PageAddressTests.cs` |
| AC6 | `TrendRangeBeforeStartsAtHourOf24HoursBack`, `HistoryStoreGoodTrendReadsRangeOnly`, `TrendSeriesBuilderHourlyPointsJoinAndMissingHourBreaks` | `tests/Censio.Server.Tests/TrendRangeTests.cs`, `HistoryStoreTests.cs`, `TrendSeriesBuilderTests.cs` |
| AC7 | `GoodTrendStatesShowTexts`, `TrendChartOverCapShowsNewestAndCapLine`, `UiTextsChartTextsInBothLanguages` | `GoodTrendTests.cs`, `tests/Censio.Server.Tests/TrendChartTests.cs`, `UiTextsTests.cs` |
| AC8 | `GoodTrendTickOfProfileRedrawsCharts`, `GoodTrendTickOfOtherProfileChangesNothing`, `GoodTrendThemeSwitchRedrawsWithoutRead` | `GoodTrendTests.cs` |
| AC9 | `TrendChartTooltipShowsTimeHourlyMarkAndValues`, `TrendSeriesBuilderUsesOffsetOrUtc` | `TrendChartTests.cs`, `TrendSeriesBuilderTests.cs` |
| AC10 | `TrendPaletteLinesReachThreeToOneOnSurface`, `TrendChartCarriesThemePaletteAndAnimationClass` | `tests/Censio.Server.Tests/TrendPaletteTests.cs`, `TrendChartTests.cs` |
| AC11 | `TrendChartMarkupInNamesIsText`, `GoodTrendMarkupInNamesIsText` | `TrendChartTests.cs`, `GoodTrendTests.cs` |
| AC12 | `GoodTrendLoadsResourcesFromServerOnly`; check: Reviewer, no package added to `Directory.Packages.props` or a project file | `GoodTrendTests.cs` |
| AC13 | `HistoryStoreGoodTrendReadsNewestMaxPointsAndCount`, `HistoryStoreTrendPartsGiveSamePointsAsOneRead`, `TrendReaderReadsOneAtATime`, `TrendReaderDisposedPageCancelsRead`, `TrendReaderFailedReadLogsWithoutValues`; manual: Dev, the N3 measurement in step 6 | `HistoryStoreTests.cs`, `TrendReaderTests.cs` |
| AC14 | manual: Product Manager, on a local build of the branch head (user guide *Build the server image* or `dotnet run --project src/Censio.Server`, the Companion from *Build the Companion*), `long-session.jsonl` replayed with `--server` after the server's start run, before its next run | — |
| AC15 | check: Reviewer reads README and user guide against the spec | — |

## Approach

**Data reads** (`Censio.Server.Data`, record 0087). `HistoryStore` implements a new `IHistoryTrends`:

- `ReadGoodTrendAsync`: per tick of the range, the first entry (in position) of the good on each island of `Islands`,
  generation, consumption and delta each summed over the islands that carry it, no value where none does; per hourly
  point the islands' means summed the same way. `ReadFiguresTrendAsync`: per tick, the island's entries as
  `ProfileViewBuilder.Figure` counts them (no value when the tick lacks the island or it has no entries); per hourly
  point the rules of spec S3 (buildings `Math.Round(sum of BuildingCountSum / EntryCount, AwayFromZero)`, produced
  `BuildingCountSum > 0`, deficit `Math.Round(DeltaSum / DeltaCount, 2, AwayFromZero) < 0` with `DeltaCount > 0`).
- First one read transaction takes the times of the newest `MaxPoints` points (ticks by `(ReceiveTicks, Number)`
  descending, hourly points by hour) and the count of all; then the values in parts of at most 240 consecutive points,
  oldest first, one read transaction each. A part keeps only its points; ticks are never materialized.
- A migration (`dotnet ef migrations add AddTickEntryProductIndex`) adds the index (`TickIslandId`, `ProductGuid`,
  `Position`) on `TickEntries`; the reads use it, the series read keeps its SQL.

**Server**:

- `TrendRange.Before(latestTick)`: start = UTC hour start of `latestTick − 24 h`, end = `latestTick` + 1 tick (end
  exclusive). `TrendRange.MaxPoints` = 2,880.
- `TrendReader` (singleton): its own `SemaphoreSlim(1, 1)`, never `PageReader`'s; maps islands to `HistoryIslandKey`,
  calls `IHistoryTrends`, converts to `TrendView` (values as `double?` in line order: production, consumption, delta /
  buildings, produced goods, goods in deficit). A cancelled read rethrows; any other exception logs
  `UiLog.ChartReadFailed` (Warning, the exception, no name or value) and returns `IsFailed`. The bound's seam is
  `IHistoryTrends` (`FakeHistoryTrends` blocks and counts concurrent calls).
- `DataPageBase` gains `ReadDetailsAsync(view, token)`, called after `Show(PageMode.Ready, view)` inside `ReadOnceAsync`,
  so the notice coalescing and cancellation of the page cover the chart reads. The pages keep a `TrendRead` and a read
  number.
- `GoodTrend` page, route of spec S1: parses the good with `PageAddress.TryParseGood` (the session rule); known when the
  island is (as `IslandDetail`); reads the island chart (`[island]`) then the region chart (the region's island keys
  of the view). Name: `PageNames.Good(productGuid, catalog)`.
- `IslandDetail`: the good name cell becomes `<MudLink Href="@PageAddress.GoodLink(...)">`; the overview chart reads
  `ReadFiguresAsync`.
- `TrendChart` component: parameters heading, kind, read, as-of, read number. Shows `Loading…` while the read is
  null, the reload text when failed, `No values in this period.` when no point has a value, else the chart once the
  browser's offset is settled (`BrowserOffset.AskAsync` for the as-of instant, shared with `DataStatus`, which keeps its
  behavior). The chart is keyed by the read number, so a new read re-creates it (the animation restarts) and a theme
  or language switch does not.
- `TrendSeriesBuilder.Build`: one `ChartSeries<double>` per line; X = `time.ToOffset(offset).DateTime` (UTC without a
  usable offset); a no-value point where the line lacks a value, and one at the earlier point's end where
  `BreaksBefore` (later start > earlier end + 15 min; tick end = its time, hourly end = start + 1 h). A no-value
  point's `Y` is the line's first value, so it never stretches the axis (fact 3).
- Options: `TimeLabelFormat` `HH\:mm`, `TimeLabelSpacing` 3 h with rounding; `TooltipTimeLabelFormat`
  `yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffffff` (culture-free); `YAxisToStringFunc` from `PageFormat`; `ShowDataMarkers`
  false; `ChartPalette` from `TrendPalette` for the cascaded theme. `TooltipTemplate` finds the hovered point by series
  and X label and writes SVG text: the time (`PageFormat.Time` with the offset), `Hourly average` for an hourly point,
  one line per series `<name>: <value>` (rates by `PageFormat.Rate`/`Delta` with new `double?` overloads, counts by
  `Count`).
- `TrendPalette`: good lines production `Success`, consumption `Error`, delta `Secondary`; figures buildings
  `Primary`, produced `Success`, deficit `Error` — values of `CensioTheme`'s palettes, which already reach 4.5:1 on
  surface.
- `MainLayout` cascades its `UiTheme`. `censio.css`: `.censio-chart path.mud-chart-line` animates once per element,
  0.8 s, `clip-path: inset(0 100% 0 0)` to `inset(0)` with opacity 0 to 1 (spec Q8).

Layout of the good page, top to bottom: `h1` good name; `<p>` island link · region link · profile; banner and as-of
line; `<p>` range line; per chart an `h2` (`mud-typography-h6`) heading, the chart (height 300 px, legend below), then
the cap line when E6 applies. The overview sits on the island detail between the as-of line and the table, with its
`h2`, the range line under it. Tooltip layout free for the Tester, approved in step 9.

## Options considered

- Chart library: record 0086 (ApexCharts 8, pinned 6.0.2, own SVG, MudBlazor — the Product Manager chose MudBlazor).
- Reads: record 0087 (series read per island, aggregate reads without index, with index, pre-computed series).
- Range and gap: record 0088.
- Offset per point (one browser call per point, exact across a daylight-saving change) — lost: up to 2,880 answers per
  read for an hour's error once a season; one offset for the latest tick, as the as-of line uses.
- Line colours as CSS variables in the SVG `stroke` attribute — lost: presentation attributes do not resolve `var()`
  reliably; the cascaded theme is certain.
- Animation by a script — lost: a new script for a style effect; a CSS rule needs none (spec Q8, the Product Manager's choice).

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| Censio.Server.Data | `IHistoryTrends`, `HistoryIslandKey`, `HistoryGoodTrendQuery`, `HistoryFiguresTrendQuery`, `HistoryGoodPoint`, `HistoryFiguresPoint`, `HistoryTrend` | new |
| Censio.Server.Data | `HistoryStore`, `CensioDbContext`, `Migrations/*_AddTickEntryProductIndex.cs`, snapshot | the two reads, the index |
| Censio.Server | `TrendRange`, `TrendView`, `TrendPoint`, `TrendRead`, `TrendReader`, `TrendKind`, `TrendSeriesBuilder`, `TrendPalette`, `BrowserOffset` | new |
| Censio.Server | `Components/TrendChart.razor(.cs)`, `Components/Pages/GoodTrend.razor(.cs)` | new |
| Censio.Server | `DataPageBase`, `IslandDetail.razor(.cs)`, `DataStatus.razor.cs`, `MainLayout.razor`, `PageAddress`, `PageFormat`, `UiTexts`, `UiLog`, `Program.cs`, `wwwroot/css/censio.css` | changed |

## Signatures (for the Dev's skeleton)

Censio.Server.Data, one type per file:

- `public interface IHistoryTrends` with `Task<HistoryTrend<HistoryGoodPoint>> ReadGoodTrendAsync(HistoryGoodTrendQuery query, CancellationToken cancellationToken);` and `Task<HistoryTrend<HistoryFiguresPoint>> ReadFiguresTrendAsync(HistoryFiguresTrendQuery query, CancellationToken cancellationToken);`, implemented by `public sealed class HistoryStore : IHistoryTrends`, which adds `public const int TrendPartSize = 240;`
- `public readonly record struct HistoryIslandKey(int SessionGuid, byte IslandId, byte AreaIndex);`
- `public sealed class HistoryGoodTrendQuery` with `public required Guid ProfileId { get; init; }`, `public required IReadOnlyList<HistoryIslandKey> Islands { get; init; }`, `public required int ProductGuid { get; init; }`, `public required DateTimeOffset RangeStart { get; init; }`, `public required DateTimeOffset RangeEnd { get; init; }`, `public required int MaxPoints { get; init; }`
- `public sealed class HistoryFiguresTrendQuery` with the same profile, range and maximum properties and `public required HistoryIslandKey Island { get; init; }`
- `public sealed class HistoryGoodPoint` with `public required DateTimeOffset Time { get; init; }`, `public required bool IsHourly { get; init; }`, `public required double? Generation { get; init; }`, `public required double? Consumption { get; init; }`, `public required double? Delta { get; init; }`
- `public sealed class HistoryFiguresPoint` with the same time and hourly flag and `public required long? Buildings { get; init; }`, `public required int? ProducedGoods { get; init; }`, `public required int? GoodsInDeficit { get; init; }`
- `public sealed class HistoryTrend<TPoint>` with `public required IReadOnlyList<TPoint> Points { get; init; }`, `public required int TotalCount { get; init; }`

Censio.Server:

- `public sealed class TrendRange` with `public const int MaxPoints = 2880;`, `public required DateTimeOffset Start { get; init; }`, `public required DateTimeOffset End { get; init; }`, `public static TrendRange Before(DateTimeOffset latestTick)`
- `public sealed class TrendPoint` with the time and hourly flag as above and `public required IReadOnlyList<double?> Values { get; init; }`
- `public sealed class TrendView` with `public required IReadOnlyList<TrendPoint> Points { get; init; }` and the total count as above
- `public sealed class TrendRead` with `public required TrendView? View { get; init; }`, `public required bool IsFailed { get; init; }`
- `public enum TrendKind` with the members Good and Figures
- `public sealed class TrendReader` with `public TrendReader(IHistoryTrends trends, ILogger<TrendReader> logger)`, `public Task<TrendRead> ReadGoodAsync(Guid profileId, IReadOnlyList<IslandKey> islands, int productGuid, DateTimeOffset latestTick, CancellationToken cancellationToken)`, `public Task<TrendRead> ReadFiguresAsync(Guid profileId, IslandKey island, DateTimeOffset latestTick, CancellationToken cancellationToken)`
- `public static class TrendSeriesBuilder` with `public static readonly TimeSpan GapAfter;` (15 minutes), `public static IReadOnlyList<ChartSeries<double>> Build(TrendView view, IReadOnlyList<string> lineNames, int? offsetMinutes)`, `public static bool BreaksBefore(TrendPoint earlier, TrendPoint later)`
- `public static class TrendPalette` with `public static IReadOnlyList<string> For(TrendKind kind, UiTheme theme)`
- `public static class BrowserOffset` with `public static Task<int?> AskAsync(IJSRuntime jsRuntime, DateTimeOffset instant, CancellationToken cancellationToken)`
- In `PageAddress`: `public static bool TryParseGood(string? text, out int productGuid)`, `public static string GoodLink(Guid profileId, IslandKey island, int productGuid)`
- In `PageFormat`: `public static string Rate(double? value, UiLanguage language)`, `public static string Delta(double? value, UiLanguage language)`
- In `UiLog`: `public static partial void ChartReadFailed(ILogger logger, Exception exception);`
- In `UiTexts`, each with the required-init pattern of its neighbours, the texts of spec S1, S2, S3, E2, E6 and S6: `public required string ChartRange { get; init; }`, `public required string ChartThisIsland { get; init; }`, `public required string ChartRegionTotal { get; init; }`, `public required string ChartOverview { get; init; }`, `public required string ChartNoValues { get; init; }`, `public required string ChartCapFormat { get; init; }`, `public required string HourlyAverage { get; init; }`
- In `DataPageBase`: `protected virtual Task ReadDetailsAsync(ProfileView view, CancellationToken cancellationToken)`
- `public sealed partial class TrendChart` (component, in Components), each a component parameter: `public string Heading { get; set; }`, `public TrendKind Kind { get; set; }`, `public TrendRead? Read { get; set; }`, `public DateTimeOffset? AsOf { get; set; }`, `public int ReadNumber { get; set; }`
- `public partial class GoodTrend` (page, in Components/Pages, base DataPageBase), each a route parameter: `public string? Profile { get; set; }`, `public string? Session { get; set; }`, `public string? Island { get; set; }`, `public string? Area { get; set; }`, `public string? Good { get; set; }`

Files the skeleton rewrites: none whole; the types named "In …" get members.

## Test files

`tests/Censio.Server.Data.Tests/HistoryStoreTests.cs`; `tests/Censio.Server.Tests/` `TrendReaderTests.cs`,
`TrendSeriesBuilderTests.cs`, `TrendRangeTests.cs`, `TrendChartTests.cs`, `TrendPaletteTests.cs`, `GoodTrendTests.cs`,
`IslandDetailTests.cs`, `PageAddressTests.cs`, `UiTextsTests.cs`; new double `tests/Censio.Server.Tests/FakeHistoryTrends.cs`.

Existing call sites: `PageTestHost` registers `IHistoryTrends` (a `HistoryStore` over its database) and `TrendReader`,
and cascades a theme where it hosts pages — the Tester, step 5. `DataStatusTests` stay unchanged (no input handled
differently). `UiTextsTests` that enumerate every text: the Tester adds the new ones.

Files no unit test can cover: none (`censio.css` is no measured code; its effect is AC14).

## Areas

- New area `web-charts` ("The charts of the data pages: the good page's charts, the island overview, the range, gaps,
  hourly means, region totals, the cap, tooltips, colours, animation, the chart reads' bounds and failures, texts";
  not here: the pages' frame and routes (web-pages), the history reads (history)).
- `web-pages`, `web-ui`, `history`, `server-data`: *Documentation updates*.

## Documentation updates

All by the Dev, against the code as built:

- `docs/areas/web-charts.md` (new, from `_template.md`) and its row in `docs/areas/README.md`; links records 0086, 0088.
- `docs/areas/web-pages.md`: *Pages and routes* "Four routes" → five, with the good page's row and heading; *Island
  detail*: good name links, the overview chart (pointer); *Logging*: the chart read failure line; *Reads and memory*:
  pointer to the chart reads; *Not here*: drop #36.
- `docs/areas/web-ui.md`: *Texts* sentence on where data page texts live adds `web-charts.md`; *Contrast*: chart line
  colours reuse the listed pairs.
- `docs/areas/history.md`: *Scope* and *Not here* (#36 now `web-charts.md`); *The series read*: the two trend reads,
  their points, bound, parts and the fold-between-parts case; *Measurements*: the index's bytes per entry, store time
  at the bounds with it, the migration's build over 7 days of fixture ticks, the trend reads' times (fact 16).
- `docs/areas/server-data.md`: the tick entries' new index where the tables are described.
- `README.md` (English and German): the "no trend charts" line becomes what #37 and #38 still add; the web
  application paragraph names the good page, region total and overview.
- `docs/USER_GUIDE.md` (English and German): *Web interface* gains the charts (range, gaps, hourly average, tooltip,
  old recordings as hourly means after the next run, "Not yet tried" for AC14 until done).
- `.squad/project.md` (exact text reported, the orchestrator writes it): *Logging and showing external data* adds
  `TrendChart`, `TrendSeriesBuilder`, `TrendReader`; *Parsing* adds `BrowserOffset`; *Test doubles* adds
  `FakeHistoryTrends` (the Tester's text).

## Architecture check

No guarantee of `.squad/project.md` changes: pages stay open without login (record 0006); gaps stay gaps (record 0031)
— the charts never draw a missing value as zero; history retention and deletion are untouched (a deleted profile's
page shows `Not found.` as before). `docs/ARCHITECTURE.md` names no chart library; no update.

## Security considerations

- [x] No limits on parsed input beyond the good segment, which is the session rule (ten ASCII digits, sign) on the
  decoded segment.
- [x] Exceptions: a failed chart read shows the reload text of `web-pages.md`; a JavaScript or timeout failure of the
  offset shows UTC with ` UTC`; no exception text reaches a page.
- Names reach the chart only as Blazor text (fact 6); the legend shows `UiTexts` only. The reads are bounded by
  points (2,880) and gated one at a time; the in-memory write gate is held per part (N3, fact 16).

## Decision records

- `docs/decisions/0086-charts-drawn-with-mudblazors-own-time-series-chart.md` (Proposed; amends 0025)
- `docs/decisions/0087-chart-reads-aggregate-per-point-bounded-in-parts-with-a-product-index.md` (Proposed)
- `docs/decisions/0088-charts-show-the-day-up-to-the-latest-tick-with-gaps-after-15-minutes.md` (Proposed)

`decision-check.py` fails on the area `web-charts` until the Dev adds the area document and its index row (G3).

## Out of scope / follow-ups

The spec's *Out of scope*; a second value axis for the overview (MudBlazor has one) if AC14 finds it unreadable.
